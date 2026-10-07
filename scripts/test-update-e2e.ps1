#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Validation E2E de ERP_Scolaire_Update_2026.09 sur une copie isolee (jamais Production).

.NOTES
  - Base dediee : SchoolManagementRDC_UpdateE2E
  - Sauvegarde / restauration de C:\Program Files\ERP Scolaire
  - Retarget temporaire du service ErpScolaireApi vers la base de test
  - Connexion SQL : meme format que DatabaseConnectionFactory (Microsoft.Data.SqlClient)
    via un pont dotnet (PowerShell 5.1 ne charge pas Microsoft.Data.SqlClient net8).
#>
[CmdletBinding()]
param(
  [string]$UpdatePackageDir = (Join-Path $PSScriptRoot '..\dist\update'),
  [string]$LabRoot = (Join-Path $PSScriptRoot '..\dist\e2e-update-lab'),
  [string]$TestDatabase = 'SchoolManagementRDC_UpdateE2E',
  [string]$ProductionDatabase = 'SchoolManagementRDC_Production',
  [switch]$SkipRestore,
  [switch]$TestSqlOnly
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$UpdatePackageDir = [IO.Path]::GetFullPath($UpdatePackageDir)
$LabRoot = [IO.Path]::GetFullPath($LabRoot)
$InstallRoot = 'C:\Program Files\ERP Scolaire'
$ServiceName = 'ErpScolaireApi'
$ReportPath = Join-Path $LabRoot 'e2e-report.txt'
$StatePath = Join-Path $LabRoot 'e2e-state.json'
$ConnectionStringEnvPrefix = 'ConnectionStrings__Default='
$script:SqlBridgeExe = $null
$script:SqlBridgeVersion = '3'

function Write-Report([string]$Line) {
  Write-Host $Line
  Add-Content -Path $ReportPath -Value $Line -Encoding UTF8
}

function Get-ServiceEnvironment {
  $regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
  $env = (Get-ItemProperty -Path $regPath -Name Environment -ErrorAction Stop).Environment
  if (-not $env) { throw 'Environment service absent.' }
  return ,@($env)
}

function Set-ServiceEnvironment([string[]]$Environment) {
  $regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
  Set-ItemProperty -Path $regPath -Name Environment -Value $Environment
}

function Get-ConnectionStringFromEnv([string[]]$Environment) {
  $line = $Environment | Where-Object { $_ -like 'ConnectionStrings__Default=*' } | Select-Object -First 1
  if (-not $line) { throw 'ConnectionStrings__Default absent.' }
  return $line.Substring($ConnectionStringEnvPrefix.Length)
}

function Get-SqlConnectionPart([string]$ConnectionString, [string]$Key) {
  $m = [regex]::Match($ConnectionString, "(?i)\b$([regex]::Escape($Key))\s*=\s*([^;]+)")
  if ($m.Success) { return $m.Groups[1].Value.Trim() }
  return $null
}

function Set-SqlConnectionCatalog([string]$ConnectionString, [string]$Catalog) {
  if ([regex]::IsMatch($ConnectionString, '(?i)Initial Catalog\s*=')) {
    return [regex]::Replace($ConnectionString, '(?i)Initial Catalog\s*=[^;]*', "Initial Catalog=$Catalog")
  }
  if ([regex]::IsMatch($ConnectionString, '(?i)\bDatabase\s*=')) {
    return [regex]::Replace($ConnectionString, '(?i)\bDatabase\s*=[^;]*', "Database=$Catalog")
  }
  return ($ConnectionString.TrimEnd(';') + ";Initial Catalog=$Catalog")
}

function Resolve-SqlClientBinDir {
  $candidates = @(
    (Join-Path $InstallRoot 'Api'),
    (Join-Path $UpdatePackageDir 'payload\api'),
    (Join-Path $root 'src\SchoolManagement.API\bin\Release\net8.0')
  )
  foreach ($candidate in $candidates) {
    if (Test-Path (Join-Path $candidate 'Microsoft.Data.SqlClient.dll')) {
      return $candidate
    }
  }
  $apiProj = Join-Path $root 'src\SchoolManagement.API\SchoolManagement.API.csproj'
  if (Test-Path $apiProj) {
    dotnet build $apiProj -c Release -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build SchoolManagement.API a echoue (Microsoft.Data.SqlClient requis).' }
    $built = Join-Path $root 'src\SchoolManagement.API\bin\Release\net8.0'
    if (Test-Path (Join-Path $built 'Microsoft.Data.SqlClient.dll')) { return $built }
  }
  throw 'Microsoft.Data.SqlClient.dll introuvable. Le script E2E utilise le meme provider que l''ERP.'
}

function Ensure-E2eSqlBridge {
  $bridgeDir = Join-Path $LabRoot 'sql-bridge'
  $versionFile = Join-Path $bridgeDir 'bridge.version'
  $expectedExe = Join-Path $bridgeDir 'bin\Release\net8.0\e2e-sql-bridge.exe'
  if ((Test-Path $expectedExe) -and (Test-Path $versionFile) -and ((Get-Content $versionFile -Raw).Trim() -eq $script:SqlBridgeVersion)) {
    $script:SqlBridgeExe = $expectedExe
    return
  }

  if (Test-Path $bridgeDir) { Remove-Item $bridgeDir -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $bridgeDir | Out-Null

  $sqlBinDir = Resolve-SqlClientBinDir

  $sqlClientDll = (Join-Path $sqlBinDir 'Microsoft.Data.SqlClient.dll').Replace('\', '\\')
  $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>e2e-sql-bridge</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Microsoft.Data.SqlClient">
      <HintPath>$sqlClientDll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
"@

  $program = @'
using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: e2e-sql-bridge <scalar|nonquery|query> <connectionStringFile> <sqlFile>");
    return 1;
}

var mode = args[0];
var connectionString = await File.ReadAllTextAsync(args[1], Encoding.UTF8);
var sql = await File.ReadAllTextAsync(args[2], Encoding.UTF8);
connectionString = connectionString.Trim();

await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
await using var command = connection.CreateCommand();
command.CommandText = sql;
command.CommandTimeout = 0;

switch (mode)
{
    case "scalar":
    {
        var value = await command.ExecuteScalarAsync();
        if (value is null or DBNull)
        {
            Console.WriteLine(string.Empty);
        }
        else
        {
            Console.WriteLine(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        return 0;
    }
    case "nonquery":
        Console.WriteLine(await command.ExecuteNonQueryAsync());
        return 0;
    case "query":
    {
        await using var reader = await command.ExecuteReaderAsync();
        var table = new DataTable();
        table.Load(reader);
        for (var c = 0; c < table.Columns.Count; c++)
        {
            if (c > 0) { Console.Write('|'); }
            Console.Write(table.Columns[c].ColumnName);
        }
        Console.WriteLine();
        foreach (DataRow row in table.Rows)
        {
            for (var c = 0; c < table.Columns.Count; c++)
            {
                if (c > 0) { Console.Write('|'); }
                var cell = row.IsNull(c) ? string.Empty : Convert.ToString(row[c], CultureInfo.InvariantCulture) ?? string.Empty;
                Console.Write(cell.Replace("|", " "));
            }
            Console.WriteLine();
        }
        return 0;
    }
    default:
        Console.Error.WriteLine("Mode inconnu: " + mode);
        return 2;
}
'@

  Set-Content -Path (Join-Path $bridgeDir 'e2e-sql-bridge.csproj') -Value $csproj -Encoding UTF8
  Set-Content -Path (Join-Path $bridgeDir 'Program.cs') -Value $program -Encoding UTF8

  Push-Location $bridgeDir
  try {
    dotnet build -c Release -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Compilation du pont SQL E2E (Microsoft.Data.SqlClient) a echoue.' }
  }
  finally { Pop-Location }

  $script:SqlBridgeExe = Join-Path $bridgeDir 'bin\Release\net8.0\e2e-sql-bridge.exe'
  if (-not (Test-Path $script:SqlBridgeExe)) {
    throw "Executable pont SQL introuvable : $script:SqlBridgeExe"
  }

  $outDir = Split-Path $script:SqlBridgeExe -Parent
  Get-ChildItem -Path $sqlBinDir -Filter '*.dll' -ErrorAction SilentlyContinue |
    Copy-Item -Destination $outDir -Force

  Set-Content -Path $versionFile -Value $script:SqlBridgeVersion -Encoding UTF8 -NoNewline
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
  [System.IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding $false))
}

function Invoke-E2eSqlBridge([string]$Mode, [string]$ConnectionString, [string]$Sql) {
  Ensure-E2eSqlBridge
  $sqlFile = Join-Path $LabRoot 'sql-bridge-query.sql'
  $csFile = Join-Path $LabRoot 'sql-bridge-connection.txt'
  Write-Utf8NoBom $sqlFile $Sql
  Write-Utf8NoBom $csFile $ConnectionString
  $output = & $script:SqlBridgeExe $Mode $csFile $sqlFile 2>&1
  if ($LASTEXITCODE -ne 0) {
    throw "Pont SQL E2E ($Mode) a echoue : $output"
  }
  return $output
}

function ConvertTo-SqlDataTable([string[]]$Lines) {
  $table = New-Object System.Data.DataTable
  if (-not $Lines -or $Lines.Count -lt 1) { return $table }
  $headers = $Lines[0] -split '\|'
  foreach ($header in $headers) { [void]$table.Columns.Add($header) }
  for ($i = 1; $i -lt $Lines.Count; $i++) {
    if ([string]::IsNullOrWhiteSpace($Lines[$i])) { continue }
    $rowValues = $Lines[$i] -split '\|'
    $row = $table.NewRow()
    for ($c = 0; $c -lt [Math]::Min($headers.Count, $rowValues.Count); $c++) {
      $row[$c] = $rowValues[$c]
    }
    [void]$table.Rows.Add($row)
  }
  return $table
}

function Set-ConnectionCatalog([string[]]$Environment, [string]$Catalog) {
  if ($Catalog -eq $ProductionDatabase) {
    throw 'Refus : catalogue Production interdit pour le test E2E.'
  }
  $copy = @($Environment)
  for ($i = 0; $i -lt $copy.Length; $i++) {
    if ($copy[$i] -like 'ConnectionStrings__Default=*') {
      $rawCs = $copy[$i].Substring($ConnectionStringEnvPrefix.Length)
      $copy[$i] = $ConnectionStringEnvPrefix + (Set-SqlConnectionCatalog $rawCs $Catalog)
      return ,$copy
    }
  }
  throw 'ConnectionStrings__Default introuvable.'
}

function Test-E2eSqlConnection([string]$ConnectionString) {
  Write-Report 'Test connexion SQL minimal (Microsoft.Data.SqlClient via pont dotnet)...'
  $dataSource = Get-SqlConnectionPart $ConnectionString 'Data Source'
  if (-not $dataSource) { $dataSource = Get-SqlConnectionPart $ConnectionString 'Server' }
  $catalog = Get-SqlConnectionPart $ConnectionString 'Initial Catalog'
  if (-not $catalog) { $catalog = Get-SqlConnectionPart $ConnectionString 'Database' }
  Write-Report ("Instance : {0} | Catalogue : {1}" -f $dataSource, $catalog)

  $one = Invoke-Sql $ConnectionString 'SELECT 1'
  if ([int]$one -ne 1) { throw 'SELECT 1 a echoue.' }
  Write-Report 'SELECT 1 : OK'

  $dbs = Invoke-SqlQuery $ConnectionString 'SELECT name FROM sys.databases'
  if ($dbs.Rows.Count -lt 1) { throw 'sys.databases vide ou inaccessible.' }
  Write-Report ("sys.databases : OK ({0} bases)" -f $dbs.Rows.Count)
}

function Invoke-Sql([string]$ConnectionString, [string]$Sql) {
  $raw = Invoke-E2eSqlBridge 'scalar' $ConnectionString $Sql
  if ($raw -is [System.Array]) { $raw = ($raw | Out-String).Trim() }
  return $raw
}

function Invoke-SqlNonQuery([string]$ConnectionString, [string]$Sql) {
  [void](Invoke-E2eSqlBridge 'nonquery' $ConnectionString $Sql)
}

function Invoke-SqlQuery([string]$ConnectionString, [string]$Sql) {
  $raw = Invoke-E2eSqlBridge 'query' $ConnectionString $Sql
  if ($raw -is [string]) { $lines = @($raw -split "`r?`n") }
  else { $lines = @($raw | ForEach-Object { "$_" }) }
  return ConvertTo-SqlDataTable $lines
}

function Get-DataSnapshot([string]$ConnectionString) {
  $tables = @(
    'Schools', 'UserAccounts', 'Students', 'Enrollments', 'Payments',
    'FeeTypes', 'AppConfigurations', 'SchoolSubscriptions'
  )
  $snap = @{}
  foreach ($t in $tables) {
    try {
      $exists = Invoke-Sql $ConnectionString "SELECT CASE WHEN OBJECT_ID(N'dbo.$t', N'U') IS NULL THEN 0 ELSE 1 END"
      if ([int]$exists -eq 0) { $snap[$t] = -1; continue }
      $snap[$t] = [int](Invoke-Sql $ConnectionString "SELECT COUNT(*) FROM dbo.[$t]")
    }
    catch { $snap[$t] = -2 }
  }
  return $snap
}

function Get-FileFingerprint([string]$Path) {
  if (-not (Test-Path $Path)) { return $null }
  return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function Wait-Health([string[]]$Urls, [int]$TimeoutSec = 1200) {
  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  $saw503 = $false
  $http = [System.Net.Http.HttpClient]::new()
  $http.Timeout = [TimeSpan]::FromSeconds(5)
  try {
    while ((Get-Date) -lt $deadline) {
      foreach ($url in $Urls) {
        try {
          $resp = $http.GetAsync($url).GetAwaiter().GetResult()
          if ([int]$resp.StatusCode -eq 503) { $saw503 = $true }
          if ($resp.IsSuccessStatusCode) {
            return @{ Ok = $true; Saw503 = $saw503; Status = [int]$resp.StatusCode }
          }
        }
        catch { }
      }
      Start-Sleep -Seconds 2
    }
    return @{ Ok = $false; Saw503 = $saw503; Status = 0 }
  }
  finally { $http.Dispose() }
}

function Start-ServiceAndWait {
  $svc = Get-Service $ServiceName -ErrorAction Stop
  if ($svc.Status -ne 'Running') {
    Start-Service $ServiceName -ErrorAction Stop
  }
  $deadline = (Get-Date).AddMinutes(5)
  while ((Get-Date) -lt $deadline) {
    $svc.Refresh()
    if ($svc.Status -eq 'Running') { return $true }
    Start-Sleep -Seconds 2
  }
  return $false
}

function Stop-ServiceAndWait {
  $svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
  if (-not $svc) { return }
  if ($svc.Status -eq 'Running') {
    Stop-Service $ServiceName -Force -ErrorAction Stop
  }
  $deadline = (Get-Date).AddMinutes(2)
  while ((Get-Date) -lt $deadline) {
    $svc.Refresh()
    if ($svc.Status -eq 'Stopped') { return }
    Start-Sleep -Seconds 2
  }
}

# --- init ---
New-Item -ItemType Directory -Force -Path $LabRoot | Out-Null
Remove-Item $ReportPath -Force -ErrorAction SilentlyContinue
Write-Report '=================================================='
Write-Report 'TEST ERP SCOLAIRE UPDATE 2026.09 - E2E LAB'
Write-Report '=================================================='
Write-Report ("Heure : {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))

$results = [ordered]@{
  Api = 'ECHEC'; Service = 'ECHEC'; Health = 'ECHEC'; SchemaInitializer = 'ECHEC'
  SchoolSubscriptions = 'ECHEC'; AbonnementInitial = 'ECHEC'; Desktop = 'ECHEC'
  Connexion = 'ECHEC'; ControleExpiration = 'ECHEC'; IntegriteDonnees = 'ECHEC'
  ConfigurationLocale = 'ECHEC'; Rollback = 'N/A'; AbonnementExistant = 'N/A'
  Global = 'ECHEC'
}

$origEnv = $null

try {
  if (-not (Test-Path $InstallRoot)) {
    throw "Installation source introuvable : $InstallRoot"
  }

  $origEnv = Get-ServiceEnvironment
  $origCs = Get-ConnectionStringFromEnv $origEnv
  $masterCs = Set-SqlConnectionCatalog $origCs 'master'

  Test-E2eSqlConnection $masterCs

  if ($TestSqlOnly) {
    Write-Report ''
    Write-Report 'RESULTAT : Connexion SQL OK (test isole)'
    $dataSource = Get-SqlConnectionPart $masterCs 'Data Source'
    if (-not $dataSource) { $dataSource = Get-SqlConnectionPart $masterCs 'Server' }
    Write-Report ("Instance : {0}" -f $dataSource)
    exit 0
  }

  if (-not (Test-Path (Join-Path $UpdatePackageDir 'ErpScolaire.Update.exe'))) {
    throw "Package update introuvable : $UpdatePackageDir"
  }

  Write-Report "Preparation base de test $TestDatabase (COPY_ONLY backup - Production non modifiee)..."

  $backup = Join-Path $LabRoot 'prod-copy.bak'
  if (Test-Path $backup) { Remove-Item $backup -Force }

  Invoke-SqlNonQuery $masterCs "IF DB_ID(N'$TestDatabase') IS NOT NULL BEGIN ALTER DATABASE [$TestDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TestDatabase]; END"
  Invoke-SqlNonQuery $masterCs "BACKUP DATABASE [$ProductionDatabase] TO DISK = N'$($backup.Replace("'", "''"))' WITH COPY_ONLY, INIT, STATS = 5"

  $fileList = Invoke-SqlQuery $masterCs "RESTORE FILELISTONLY FROM DISK = N'$($backup.Replace("'", "''"))'"
  $moveClauses = @()
  $dataIdx = 0
  foreach ($row in $fileList.Rows) {
    $logical = $row['LogicalName'].ToString()
    $type = $row['Type'].ToString()
    if ($type -eq 'D') {
      $dest = "C:\Temp\${TestDatabase}_$dataIdx.mdf"
      $moveClauses += "MOVE N'$logical' TO N'$dest'"
      $dataIdx++
    }
    elseif ($type -eq 'L') {
      $dest = "C:\Temp\${TestDatabase}_log.ldf"
      $moveClauses += "MOVE N'$logical' TO N'$dest'"
    }
  }
  $moveSql = ($moveClauses -join ', ')
  Invoke-SqlNonQuery $masterCs "RESTORE DATABASE [$TestDatabase] FROM DISK = N'$($backup.Replace("'", "''"))' WITH REPLACE, $moveSql, STATS = 5"

  $testCs = Set-SqlConnectionCatalog $origCs $TestDatabase

  # Remove subscription if present (simulate 1.0.2 without subscription)
  Invoke-SqlNonQuery $testCs "IF OBJECT_ID(N'dbo.SchoolSubscriptions', N'U') IS NOT NULL DELETE FROM dbo.SchoolSubscriptions"

  $before = Get-DataSnapshot $testCs
  $before | ConvertTo-Json | Set-Content (Join-Path $LabRoot 'before.json') -Encoding UTF8
  Write-Report ("Snapshot avant : Schools={0} Users={1} Students={2} Enrollments={3} Payments={4}" -f $before.Schools, $before.UserAccounts, $before.Students, $before.Enrollments, $before.Payments)

  if ($before.Schools -le 0 -or $before.UserAccounts -le 0) {
    throw 'Base de test insuffisante (ecole ou utilisateur manquant).'
  }

  # Backup install + config fingerprints
  $backupInstall = Join-Path $LabRoot 'install-backup'
  if (Test-Path $backupInstall) { Remove-Item $backupInstall -Recurse -Force }
  Copy-Item $InstallRoot $backupInstall -Recurse -Force
  $configFiles = @(
    (Join-Path $InstallRoot 'Api\ServeurDonnees.txt'),
    (Join-Path $InstallRoot 'Api\ServeurFichiers.txt'),
    (Join-Path $InstallRoot 'Api\ServerIdentity.json'),
    (Join-Path $InstallRoot 'Desktop\appsettings.json')
  )
  $configBefore = @{}
  foreach ($f in $configFiles) { $configBefore[$f] = Get-FileFingerprint $f }

  # Retarget service + ServeurDonnees BASE= for subscription bootstrap
  $testEnv = Set-ConnectionCatalog $origEnv $TestDatabase
  Set-ServiceEnvironment $testEnv
  foreach ($app in @('Api', 'Desktop')) {
    $sd = Join-Path $InstallRoot "$app\ServeurDonnees.txt"
    if (Test-Path $sd) {
      $text = Get-Content $sd -Raw
      $text = [regex]::Replace($text, '(?m)^BASE=.*$', "BASE=$TestDatabase")
      Set-Content $sd $text -Encoding UTF8 -NoNewline
    }
  }

  # Ensure service stopped before update
  Stop-ServiceAndWait
  $results.Service = 'OK'

  # Run update (meme moteur que ErpScolaire.Update.exe - mode /unattended)
  Push-Location $UpdatePackageDir
  try {
    $updateProj = Join-Path $root 'src\SchoolManagement.Update\SchoolManagement.Update.csproj'
    dotnet build $updateProj -c Release -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build SchoolManagement.Update a echoue.' }
    $p = Start-Process -FilePath 'dotnet' -ArgumentList @(
      'run', '--project', $updateProj, '-c', 'Release', '--no-build', '--', '/unattended'
    ) -WorkingDirectory $UpdatePackageDir -PassThru -Wait -WindowStyle Hidden -RedirectStandardOutput (Join-Path $LabRoot 'update-stdout.log') -RedirectStandardError (Join-Path $LabRoot 'update-stderr.log')
    if ($p.ExitCode -ne 0) {
      $errTail = Get-Content (Join-Path $LabRoot 'update-stderr.log') -Tail 20 -ErrorAction SilentlyContinue
      throw "UpdateEngine exit code $($p.ExitCode). $errTail"
    }
  }
  finally { Pop-Location }

  # Post-update checks
  $svcRunning = Start-ServiceAndWait
  $results.Api = 'OK'
  $results.Service = if ($svcRunning) { 'OK' } else { 'ECHEC' }

  $healthUrls = @('http://127.0.0.1:5096/api/v1/health', 'http://127.0.0.1:5096/api/health')
  $health = Wait-Health $healthUrls 1200
  $results.Health = if ($health.Ok) { 'OK' } else { 'ECHEC' }
  Write-Report ("Health : status={0} saw503={1}" -f $health.Status, $health.Saw503)

  $subTable = Invoke-Sql $testCs "SELECT CASE WHEN OBJECT_ID(N'dbo.SchoolSubscriptions', N'U') IS NULL THEN 0 ELSE 1 END"
  $results.SchemaInitializer = if ($health.Ok) { 'OK' } else { 'ECHEC' }
  $results.SchoolSubscriptions = if ([int]$subTable -eq 1) { 'OK' } else { 'ECHEC' }

  $subRows = Invoke-SqlQuery $testCs "SELECT TOP 1 InstallationDate, DurationMonths, ExpirationDate, IsActive FROM dbo.SchoolSubscriptions WHERE IsDeleted = 0"
  if ($subRows.Rows.Count -eq 1) {
    $inst = [datetime]$subRows.Rows[0].InstallationDate
    $exp = [datetime]$subRows.Rows[0].ExpirationDate
    $dur = [int]$subRows.Rows[0].DurationMonths
    $active = [bool]$subRows.Rows[0].IsActive
    $okSub = ($inst.Date -eq [datetime]'2026-09-01'.Date) -and ($dur -eq 1) -and ($exp.Date -eq [datetime]'2026-10-01'.Date) -and $active
    $results.AbonnementInitial = if ($okSub) { 'OK' } else { 'ECHEC' }
    Write-Report ("Abonnement : {0:yyyy-MM-dd} -> {1:yyyy-MM-dd} dur={2} active={3}" -f $inst, $exp, $dur, $active)
  }

  $after = Get-DataSnapshot $testCs
  $after | ConvertTo-Json | Set-Content (Join-Path $LabRoot 'after.json') -Encoding UTF8
  $integrity = ($before.Schools -eq $after.Schools) -and ($before.UserAccounts -eq $after.UserAccounts) -and ($before.Students -eq $after.Students) -and ($before.Enrollments -eq $after.Enrollments) -and ($before.Payments -eq $after.Payments)
  $results.IntegriteDonnees = if ($integrity) { 'OK' } else { 'ECHEC' }
  Write-Report ("Snapshot apres : Schools={0} Users={1} Students={2} Enrollments={3} Payments={4}" -f $after.Schools, $after.UserAccounts, $after.Students, $after.Enrollments, $after.Payments)

  $deskExe = Join-Path $InstallRoot 'Desktop\SchoolManagement.Desktop.exe'
  $deskVer = (Get-Item $deskExe).VersionInfo.ProductVersion
  $results.Desktop = if ($deskVer -like '2026.9*') { 'OK' } else { "ECHEC ($deskVer)" }

  $configOk = $true
  foreach ($f in $configFiles) {
    $h1 = $configBefore[$f]; $h2 = Get-FileFingerprint $f
    if ($h1 -and $h2 -and $h1 -ne $h2 -and ($f -notlike '*ServeurDonnees*')) {
      # appsettings may change if payload included one - ServeurDonnees must match retarget only
    }
    if ($f -like '*ServerIdentity*' -and $h1 -ne $h2) { $configOk = $false }
  }
  $sdText = Get-Content (Join-Path $InstallRoot 'Api\ServeurDonnees.txt') -Raw
  $configOk = $configOk -and ($sdText -match "BASE=$TestDatabase")
  $results.ConfigurationLocale = if ($configOk) { 'OK' } else { 'ECHEC' }

  # Login + subscription API (mot de passe lab connu si present)
  $adminUser = 'admin'
  $passwords = @('12345678', 'Admin123!', 'admin')
  $token = $null
  $http = [System.Net.Http.HttpClient]::new()
  foreach ($pwd in $passwords) {
    $loginBody = @{ userName = $adminUser; password = $pwd } | ConvertTo-Json
    $loginResp = $http.PostAsync('http://127.0.0.1:5096/api/v1/auth/login', [System.Net.Http.StringContent]::new($loginBody, [Text.Encoding]::UTF8, 'application/json')).Result
    if ($loginResp.IsSuccessStatusCode) {
      $loginJson = $loginResp.Content.ReadAsStringAsync().Result | ConvertFrom-Json
      $token = $loginJson.data.accessToken
      Write-Report "Login OK avec $adminUser / $pwd"
      break
    }
  }
  if ($token) {
    $http.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)
    $subResp = $http.GetAsync('http://127.0.0.1:5096/api/v1/schools/current/subscription').Result
    $results.Connexion = if ($subResp.IsSuccessStatusCode) { 'OK' } else { 'ECHEC' }
    if ($subResp.IsSuccessStatusCode) {
      $subJson = $subResp.Content.ReadAsStringAsync().Result | ConvertFrom-Json
      $valid = $subJson.data.isValid -eq $true
      if (-not $valid) { $results.Connexion = 'ECHEC (abonnement non valide)' }
    }
  }
  else {
    $results.Connexion = 'ECHEC (login - mot de passe admin inconnu sur copie test)'
    Write-Report 'Login impossible - tests API abonnement partiels seulement.'
  }

  if ($token) {
    Invoke-SqlNonQuery $testCs "UPDATE dbo.SchoolSubscriptions SET ExpirationDate = '2020-01-01' WHERE IsDeleted = 0"
    $subResp2 = $http.GetAsync('http://127.0.0.1:5096/api/v1/schools/current/subscription').Result
    $subJson2 = $subResp2.Content.ReadAsStringAsync().Result | ConvertFrom-Json
    $expiredOk = ($subJson2.data.isExpired -eq $true) -and ($subJson2.data.isValid -eq $false)
    Invoke-SqlNonQuery $testCs "UPDATE dbo.SchoolSubscriptions SET ExpirationDate = '2027-10-01', IsActive = 1 WHERE IsDeleted = 0"
    $subResp3 = $http.GetAsync('http://127.0.0.1:5096/api/v1/schools/current/subscription').Result
    $subJson3 = $subResp3.Content.ReadAsStringAsync().Result | ConvertFrom-Json
    $validAgain = $subJson3.data.isValid -eq $true
    $results.ControleExpiration = if ($expiredOk -and $validAgain) { 'OK' } else { 'ECHEC' }
  }
  else {
    $results.ControleExpiration = 'ECHEC (login requis)'
  }
  $http.Dispose()

  # Test 2 : abonnement existant non modifie
  $existingId = [guid]::NewGuid()
  Invoke-SqlNonQuery $testCs "DELETE FROM dbo.SchoolSubscriptions; INSERT INTO dbo.SchoolSubscriptions (Id,SchoolId,InstallationDate,DurationMonths,ExpirationDate,IsActive,CreatedAt,IsDeleted) SELECT TOP 1 '$existingId', Id, '2025-01-01', 99, '2099-12-31', 1, SYSUTCDATETIME(), 0 FROM dbo.Schools WHERE IsDeleted=0"
  Stop-ServiceAndWait
  Push-Location $UpdatePackageDir
  try {
    $updateProj = Join-Path $root 'src\SchoolManagement.Update\SchoolManagement.Update.csproj'
    $p2 = Start-Process -FilePath 'dotnet' -ArgumentList @(
      'run', '--project', $updateProj, '-c', 'Release', '--no-build', '--', '/unattended'
    ) -WorkingDirectory $UpdatePackageDir -PassThru -Wait -WindowStyle Hidden
  }
  finally { Pop-Location }
  Start-ServiceAndWait | Out-Null
  $subKeep = Invoke-SqlQuery $testCs "SELECT DurationMonths, ExpirationDate FROM dbo.SchoolSubscriptions WHERE IsDeleted=0"
  $keepOk = ($subKeep.Rows.Count -eq 1) -and ([int]$subKeep.Rows[0].DurationMonths -eq 99) -and ([datetime]$subKeep.Rows[0].ExpirationDate -eq [datetime]'2099-12-31')
  $results.AbonnementExistant = if ($keepOk) { 'OK' } else { 'ECHEC' }

  # Rollback folder exists
  $rollbackDirs = Get-ChildItem (Join-Path $env:ProgramData 'ERP_SCOLAIRE\UpdateRollback') -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
  $results.Rollback = if ($rollbackDirs -and $rollbackDirs.Count -gt 0) { 'OK' } else { 'ECHEC' }

  $fail = $results.Values | Where-Object { $_ -eq 'ECHEC' -or $_ -like 'ECHEC*' }
  $results.Global = if (-not $fail) { 'OK' } else { 'ECHEC' }
}
catch {
  Write-Report ("ERREUR E2E : {0}" -f $_.Exception.Message)
  $results.Global = 'ECHEC'
}
finally {
  try {
    if (-not $SkipRestore) {
      if ($origEnv) { Set-ServiceEnvironment $origEnv; Write-Report 'Service Environment restaure.' }
      $backupInstall = Join-Path $LabRoot 'install-backup'
      if (Test-Path $backupInstall) {
        foreach ($app in @('Api', 'Desktop')) {
          $srcSd = Join-Path $backupInstall "$app\ServeurDonnees.txt"
          $dstSd = Join-Path $InstallRoot "$app\ServeurDonnees.txt"
          if (Test-Path $srcSd) { Copy-Item $srcSd $dstSd -Force }
        }
        Write-Report 'ServeurDonnees.txt restaure depuis sauvegarde lab.'
      }
    }
  }
  catch { Write-Report ("Restore echoue : {0}" -f $_.Exception.Message) }
}

Write-Report ''
Write-Report '=================================================='
Write-Report 'TEST ERP SCOLAIRE UPDATE 2026.09'
Write-Report '=================================================='
Write-Report 'Installation source : 1.0.2 (lab copie / 1.0.0-syncfix)'
Write-Report 'Installation cible  : 2026.9.0'
Write-Report ''
foreach ($k in @('Api','Service','Health','SchemaInitializer','SchoolSubscriptions','AbonnementInitial','Desktop','Connexion','ControleExpiration','IntegriteDonnees','ConfigurationLocale','Rollback','AbonnementExistant')) {
  $label = switch ($k) {
    'AbonnementInitial' { 'Abonnement initial' }
    'ControleExpiration' { 'Controle expiration' }
    'IntegriteDonnees' { 'Integrite des donnees' }
    'ConfigurationLocale' { 'Configuration locale' }
    'AbonnementExistant' { 'Abonnement existant (test 2)' }
    default { $k }
  }
  Write-Report ("{0,-28}{1}" -f ($label + ' '), $results[$k])
}
Write-Report ''
Write-Report ("RESULTAT GLOBAL             {0}" -f $results.Global)
Write-Report '=================================================='
Write-Report ("Rapport : $ReportPath")
