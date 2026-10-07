param([int]$Port = 5097)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$runtimeDir = Join-Path $projectRoot '_run\api-development'
$storageDir = Join-Path $projectRoot 'storage\development'
$projectFile = Join-Path $projectRoot 'src\SchoolManagement.API\SchoolManagement.API.csproj'
$buildDir = Join-Path $projectRoot 'src\SchoolManagement.API\bin\Debug\net8.0'

if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) {
    throw "Le port $Port est deja utilise."
}

dotnet build $projectFile --no-restore --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Compilation API echouee.' }
New-Item -ItemType Directory -Path $runtimeDir, $storageDir -Force | Out-Null
Get-ChildItem -LiteralPath $buildDir | Copy-Item -Destination $runtimeDir -Recurse -Force
Set-Content -LiteralPath (Join-Path $runtimeDir 'ServeurDonneesCloud.txt') -Value 'ACTIF=0' -Encoding utf8

# Variables propres a ce lanceur et au processus enfant, sans modification du service installe.
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
$env:SQL_CONNECTION_STRING = 'Server=localhost\HEROS_SQL19;Database=SchoolManagementRDC_Development;Integrated Security=True;TrustServerCertificate=True;Encrypt=True'
$env:FileStorage__Root = $storageDir
$env:FILE_STORAGE_ROOT = $storageDir
$env:LocalServerDiscovery__Advertise = 'false'
$env:Seed__IncludeDemoData = 'false'
$env:ALLOW_DEMO_SEED = 'false'
$env:Deployment__Role = 'Local'
$env:Deployment__ReadOnly = 'false'
$apiProcess = Start-Process -FilePath (Join-Path $runtimeDir 'SchoolManagement.API.exe') -WorkingDirectory $runtimeDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtimeDir 'stdout.log') -RedirectStandardError (Join-Path $runtimeDir 'stderr.log') -PassThru
Set-Content -LiteralPath (Join-Path $runtimeDir 'api.pid') -Value $apiProcess.Id
Write-Output "API developpement : http://127.0.0.1:$Port (PID $($apiProcess.Id))"
