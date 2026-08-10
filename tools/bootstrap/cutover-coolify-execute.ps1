#Requires -Version 5.1
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$BootstrapUrl = "https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io",
    [string]$EcoleTestId = "71635f62-b975-479d-9e6e-fbacd05e4996",
    [switch]$CreateBootstrapDbIfMissing
)

$ErrorActionPreference = "Stop"
$BootstrapUrl = $BootstrapUrl.TrimEnd("/")
$report = [ordered]@{
    startedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    bootstrapUrl = $BootstrapUrl
    ecoleTestId = $EcoleTestId
}

function Get-DotEnvValue([string]$path, [string]$key) {
    if (-not (Test-Path $path)) { return $null }
    foreach ($line in Get-Content $path) {
        if ($line -match '^\s*#' -or $line -match '^\s*$') { continue }
        $idx = $line.IndexOf('=')
        if ($idx -lt 1) { continue }
        $k = $line.Substring(0, $idx).Trim()
        if ($k -ne $key) { continue }
        return $line.Substring($idx + 1).Trim().Trim('"').Trim("'")
    }
    return $null
}

function Invoke-Sql([string]$cs, [string]$query) {
    Add-Type -AssemblyName System.Data
    $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query
        $cmd.CommandTimeout = 60
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $table = New-Object System.Data.DataTable
        [void]$adapter.Fill($table)
        return $table
    }
    finally { $conn.Close() }
}

function Invoke-SqlNonQuery([string]$cs, [string]$query) {
    Add-Type -AssemblyName System.Data
    $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query
        $cmd.CommandTimeout = 120
        return $cmd.ExecuteNonQuery()
    }
    finally { $conn.Close() }
}

Write-Host "=== Phase 8 cutover diagnostics ==="

$envPath = Join-Path $RepoRoot ".env"
$sqlCs = Get-DotEnvValue $envPath "SQL_CONNECTION_STRING"
if ([string]::IsNullOrWhiteSpace($sqlCs)) {
    throw "SQL_CONNECTION_STRING missing from .env"
}
$report.sqlConnectionConfigured = $true
$report.sqlConnectionSource = ".env:SQL_CONNECTION_STRING"

$masterCs = [regex]::Replace($sqlCs, "Database=[^;]+", "Database=master", "IgnoreCase")
if ($masterCs -eq $sqlCs -and $sqlCs -notmatch "(?i)Database=") {
    $masterCs = $sqlCs.TrimEnd(";") + ";Database=master"
}

Write-Host "[1] Check DB SchoolManagementBootstrap..."
$dbRow = Invoke-Sql $masterCs "SELECT name FROM sys.databases WHERE name = N'SchoolManagementBootstrap';"
$dbExists = $dbRow.Rows.Count -gt 0
$report.schoolManagementBootstrapExists = $dbExists
Write-Host ("    Exists=" + $dbExists)

if (-not $dbExists) {
    if ($CreateBootstrapDbIfMissing) {
        Write-Host "    Creating SchoolManagementBootstrap..."
        [void](Invoke-SqlNonQuery $masterCs "CREATE DATABASE [SchoolManagementBootstrap];")
        $dbExists = $true
        $report.schoolManagementBootstrapCreated = $true
        Write-Host "    Created."
    }
    else {
        $report.blocker = "DB SchoolManagementBootstrap missing. Re-run with -CreateBootstrapDbIfMissing."
        Write-Host ("BLOCKER: " + $report.blocker)
    }
}

$bootstrapCs = [regex]::Replace($sqlCs, "Database=[^;]+", "Database=SchoolManagementBootstrap", "IgnoreCase")
$report.bootstrapConnectionStringDerived = $true
$report.bootstrapConnectionStringNote = "Derive Bootstrap__ConnectionString from same SQL server with Database=SchoolManagementBootstrap"

Write-Host "[6a] Local school active credential..."
try {
    $credQ = @"
SELECT TOP 1 Id, CredentialVersion, SecretHash, Status, TokenType, BootstrapSyncPending
FROM dbo.SchoolEstablishmentCredentials
WHERE SchoolId = '$EcoleTestId' AND IsDeleted = 0 AND Status = 'Active'
ORDER BY CredentialVersion DESC;
"@
    $localCred = Invoke-Sql $sqlCs $credQ
    if ($localCred.Rows.Count -eq 0) {
        $report.localActiveCredential = $null
        Write-Host "    No local active credential for ECOLE TEST"
    }
    else {
        $r = $localCred.Rows[0]
        $hash = [string]$r.SecretHash
        $report.localActiveCredential = [ordered]@{
            id = $r.Id.ToString()
            version = [int]$r.CredentialVersion
            status = [string]$r.Status
            tokenType = [string]$r.TokenType
            secretHashPrefix = $hash.Substring(0, [Math]::Min(8, $hash.Length)) + "..."
            bootstrapSyncPending = [bool]$r.BootstrapSyncPending
        }
        Write-Host ("    Local Active Id=" + $r.Id + " ver=" + $r.CredentialVersion + " syncPending=" + $r.BootstrapSyncPending)
    }
}
catch {
    $report.localActiveCredentialError = $_.Exception.Message
    Write-Host ("    ERR local credential: " + $_.Exception.Message)
}

if ($dbExists) {
    Write-Host "[5] Bootstrap tables + ECOLE TEST registry..."
    try {
        $tables = Invoke-Sql $bootstrapCs @"
SELECT t.name FROM sys.tables t
WHERE t.name IN (N'BootstrapSchoolRegistry',N'BootstrapSchoolEstablishmentCredentials',N'BootstrapEstablishmentSessions')
ORDER BY t.name;
"@
        $tableNames = @($tables.Rows | ForEach-Object { $_.name })
        $report.bootstrapTables = $tableNames
        Write-Host ("    Tables: " + ($tableNames -join ", "))

        if ($tableNames.Count -lt 3) {
            $report.bootstrapTablesNote = "Tables missing - deploy Phase 8 binary (auto Migrate) first."
        }
        else {
            $school = Invoke-Sql $bootstrapCs @"
SELECT SchoolId, SchoolName, ActivationBaseUrl, CloudBaseUrl, ServerInstanceId, IsActive
FROM dbo.BootstrapSchoolRegistry
WHERE SchoolId = '$EcoleTestId';
"@
            if ($school.Rows.Count -eq 0) {
                $report.ecoleTestInRegistry = $false
                Write-Host "    ECOLE TEST ABSENT from SQL registry"
            }
            else {
                $s = $school.Rows[0]
                $report.ecoleTestInRegistry = $true
                $sid = $null
                if (-not ($s.ServerInstanceId -is [DBNull])) { $sid = $s.ServerInstanceId.ToString() }
                $report.ecoleTestRegistry = [ordered]@{
                    schoolId = $s.SchoolId.ToString()
                    schoolName = [string]$s.SchoolName
                    activationBaseUrl = [string]$s.ActivationBaseUrl
                    cloudBaseUrl = [string]$s.CloudBaseUrl
                    serverInstanceId = $sid
                    isActive = [bool]$s.IsActive
                }
                Write-Host ("    ECOLE TEST present: " + $s.SchoolName + " cloud=" + $s.CloudBaseUrl)

                $bCred = Invoke-Sql $bootstrapCs @"
SELECT TOP 1 Id, CredentialVersion, SecretHash, Status
FROM dbo.BootstrapSchoolEstablishmentCredentials
WHERE SchoolId = '$EcoleTestId' AND Status = 'Active'
ORDER BY CredentialVersion DESC;
"@
                if ($bCred.Rows.Count -eq 0) {
                    $report.bootstrapActiveCredential = $null
                    Write-Host "    No active Bootstrap credential"
                }
                else {
                    $c = $bCred.Rows[0]
                    $bhash = [string]$c.SecretHash
                    $report.bootstrapActiveCredential = [ordered]@{
                        id = $c.Id.ToString()
                        version = [int]$c.CredentialVersion
                        status = [string]$c.Status
                        secretHashPrefix = $bhash.Substring(0, [Math]::Min(8, $bhash.Length)) + "..."
                    }
                    if ($null -ne $report.localActiveCredential) {
                        $matchId = $report.localActiveCredential.id -eq $c.Id.ToString()
                        $matchVer = $report.localActiveCredential.version -eq [int]$c.CredentialVersion
                        $hashMatch = $false
                        $localHashRows = Invoke-Sql $sqlCs ("SELECT SecretHash FROM dbo.SchoolEstablishmentCredentials WHERE Id='" + $c.Id + "'")
                        if ($localHashRows.Rows.Count -gt 0) {
                            $hashMatch = ([string]$localHashRows.Rows[0].SecretHash) -eq $bhash
                        }
                        $report.credentialMatch = [ordered]@{
                            sameId = $matchId
                            sameVersion = $matchVer
                            sameSecretHash = $hashMatch
                        }
                        Write-Host ("    Credential match id=" + $matchId + " ver=" + $matchVer + " hash=" + $hashMatch)
                    }
                }
            }
        }
    }
    catch {
        $report.bootstrapSqlError = $_.Exception.Message
        Write-Host ("    ERR Bootstrap SQL: " + $_.Exception.Message)
    }
}

Write-Host "[4] GET /health Bootstrap..."
try {
    $health = Invoke-RestMethod -Uri ($BootstrapUrl + "/health") -TimeoutSec 30
    $report.health = $health
    Write-Host ($health | ConvertTo-Json -Compress)
    $propNames = @($health.PSObject.Properties.Name)
    $hasRegistry = $propNames -contains "registry"
    $report.phase8BinaryDeployed = $hasRegistry
    if (-not $hasRegistry) {
        Write-Host "    Phase 8 binary NOT deployed (health missing registry field)."
        $report.blocker = "Deploy Phase 8 commit on Coolify (Rebuild), set Bootstrap__ConnectionString, then re-run."
    }
}
catch {
    $report.healthError = $_.Exception.Message
    Write-Host ("    ERR health: " + $_.Exception.Message)
}

$outDir = Join-Path $RepoRoot "artifacts"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$outPath = Join-Path $outDir "phase8-cutover-status.json"
($report | ConvertTo-Json -Depth 8) | Set-Content -Path $outPath -Encoding UTF8
Write-Host ("Report: " + $outPath)
Write-Host "Do NOT remove Bootstrap__Schools__* until Phase 8 health + establishment succeed."
$report | ConvertTo-Json -Depth 6
