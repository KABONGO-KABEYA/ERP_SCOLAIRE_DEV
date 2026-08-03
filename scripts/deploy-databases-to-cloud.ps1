# Deploy SchoolManagementRDC_Development + SchoolManagementRDC_Production
# from local HEROS_SQL19 to remote SQL (ServeurDonneesCloud.txt).
param(
    [string]$RemoteServer = "169.58.93.203",
    [int]$RemotePort = 1433,
    [string]$LocalServer = "localhost\HEROS_SQL19",
    [string]$ApiConfigDir = "",
    [switch]$SkipRemoteLegacyBackup
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$backupDir = Join-Path $root "database\backups"
$stageDir = "C:\Temp\erp-bacpac"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"

if ([string]::IsNullOrWhiteSpace($ApiConfigDir)) {
    $ApiConfigDir = Join-Path $root "src\SchoolManagement.API"
}
$cloudFile = Join-Path $ApiConfigDir "ServeurDonneesCloud.txt"
if (-not (Test-Path $cloudFile)) {
    throw "ServeurDonneesCloud.txt not found: $cloudFile"
}

$sqlcmd = "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE"
$sqlpackage = (Get-Command sqlpackage.exe -ErrorAction Stop).Source
$dbs = @("SchoolManagementRDC_Development", "SchoolManagementRDC_Production")

Add-Type -AssemblyName System.Security
$entropy = [Text.Encoding]::UTF8.GetBytes("SchoolManagement.ERP.Scolaire.RDC.v1")
$map = @{}
Get-Content $cloudFile | ForEach-Object {
    $t = $_.Trim()
    if (-not $t -or $t.StartsWith("#")) { return }
    $i = $t.IndexOf("=")
    if ($i -le 0) { return }
    $map[$t.Substring(0, $i).Trim().ToUpperInvariant()] = $t.Substring($i + 1).Trim()
}
$encPwd = $map["MOTDEPASSE"]
if ([string]::IsNullOrWhiteSpace($encPwd) -or -not $encPwd.StartsWith("ENC:", [StringComparison]::OrdinalIgnoreCase)) {
    throw "Cloud MOTDEPASSE missing or not ENC: encrypted."
}
$remoteUser = if ($map.ContainsKey("UTILISATEUR")) { $map["UTILISATEUR"] } else { "sa" }
$protectedBytes = [Convert]::FromBase64String($encPwd.Substring(4))
$plainBytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
    $protectedBytes, $entropy, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
$remotePwd = [Text.Encoding]::UTF8.GetString($plainBytes)
if ($map.ContainsKey("SERVEUR") -and $map["SERVEUR"]) { $RemoteServer = $map["SERVEUR"] }
if ($map.ContainsKey("PORT")) {
    $p = 0
    if ([int]::TryParse($map["PORT"], [ref]$p) -and $p -gt 0) { $RemotePort = $p }
}
Write-Host "Cloud user=$remoteUser server=$RemoteServer,$RemotePort (password OK)"

function New-RemoteCs([string]$database) {
    return "Server=$RemoteServer,$RemotePort;Database=$database;User Id=$remoteUser;Password=$remotePwd;TrustServerCertificate=True;Encrypt=True;Connection Timeout=120"
}
function New-LocalCs([string]$database) {
    return "Server=$LocalServer;Database=$database;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;Connection Timeout=120"
}

Write-Host "=== Test remote connection ==="
& $sqlcmd -S "$RemoteServer,$RemotePort" -U $remoteUser -P $remotePwd -C -l 30 -h -1 -W -Q "SET NOCOUNT ON; SELECT @@SERVERNAME;"
if ($LASTEXITCODE -ne 0) { throw "Remote SQL connection failed." }

Write-Host "=== Remote databases (before) ==="
& $sqlcmd -S "$RemoteServer,$RemotePort" -U $remoteUser -P $remotePwd -C -h -1 -W -Q "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name LIKE 'SchoolManagement%' ORDER BY name;"

if (-not $SkipRemoteLegacyBackup) {
    $legacyBacpac = Join-Path $stageDir "REMOTE_SchoolManagementRDC_$stamp.bacpac"
    Write-Host "=== Export remote legacy SchoolManagementRDC ==="
    $legacyCs = New-RemoteCs "SchoolManagementRDC"
    & $sqlpackage "/Action:Export" "/SourceConnectionString:$legacyCs" "/TargetFile:$legacyBacpac" "/p:VerifyExtraction=false"
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Remote legacy export failed (exit $LASTEXITCODE) - continuing."
    } else {
        Copy-Item $legacyBacpac (Join-Path $backupDir (Split-Path $legacyBacpac -Leaf)) -Force
    }
}

foreach ($db in $dbs) {
    $bak = Join-Path $backupDir "${db}_FULL_$stamp.bak"
    $bacpac = Join-Path $stageDir "${db}_$stamp.bacpac"

    Write-Host "=== BACKUP local $db ==="
    & $sqlcmd -S $LocalServer -E -Q "BACKUP DATABASE [$db] TO DISK = N'$bak' WITH COPY_ONLY, INIT, COMPRESSION, STATS = 10;"
    if ($LASTEXITCODE -ne 0) { throw "Local backup failed: $db" }

    Write-Host "=== EXPORT BACPAC local $db ==="
    if (Test-Path $bacpac) { Remove-Item $bacpac -Force }
    $localCs = New-LocalCs $db
    & $sqlpackage "/Action:Export" "/SourceConnectionString:$localCs" "/TargetFile:$bacpac" "/p:VerifyExtraction=false"
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $bacpac)) { throw "BACPAC export failed: $db" }
    $mb = [math]::Round((Get-Item $bacpac).Length / 1MB, 1)
    Write-Host "BACPAC OK: $bacpac ($mb MB)"
    Copy-Item $bacpac (Join-Path $backupDir (Split-Path $bacpac -Leaf)) -Force

    Write-Host "=== DROP remote $db if exists ==="
    $dropSql = "IF DB_ID(N'$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END"
    & $sqlcmd -S "$RemoteServer,$RemotePort" -U $remoteUser -P $remotePwd -C -Q $dropSql
    if ($LASTEXITCODE -ne 0) { throw "Remote DROP failed: $db" }

    Write-Host "=== IMPORT BACPAC to remote $db ==="
    $remoteMasterCs = New-RemoteCs "master"
    & $sqlpackage "/Action:Import" "/TargetConnectionString:$remoteMasterCs" "/SourceFile:$bacpac" "/TargetDatabaseName:$db"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Retry import with Database in connection string..."
        $remoteDbCs = New-RemoteCs $db
        & $sqlpackage "/Action:Import" "/TargetConnectionString:$remoteDbCs" "/SourceFile:$bacpac"
    }
    if ($LASTEXITCODE -ne 0) { throw "Remote import failed: $db" }

    Write-Host "=== VERIFY remote $db ==="
    & $sqlcmd -S "$RemoteServer,$RemotePort" -U $remoteUser -P $remotePwd -C -h -1 -W -s "|" -Q "SET NOCOUNT ON; SELECT '$db' AS db, (SELECT COUNT(*) FROM [$db].sys.tables WHERE is_ms_shipped=0) AS tables, (SELECT COUNT(*) FROM [$db].dbo.UserAccounts) AS users, (SELECT COUNT(*) FROM [$db].dbo.Students) AS students, (SELECT COUNT(*) FROM [$db].dbo.Permissions) AS permissions;"
}

Write-Host "=== Remote databases (after) ==="
& $sqlcmd -S "$RemoteServer,$RemotePort" -U $remoteUser -P $remotePwd -C -h -1 -W -Q "SET NOCOUNT ON; SELECT name, state_desc FROM sys.databases WHERE name LIKE 'SchoolManagement%' ORDER BY name;"

$remotePwd = $null
[GC]::Collect()
Write-Host "DONE deploy to $RemoteServer"
