# Build APK release — validation architecture connexion v2 (appareil Android réel).
# Usage :
#   .\build-apk-validation.ps1
#   .\build-apk-validation.ps1 -LocalPort 5041 -BootstrapPort 5050
#   .\build-apk-validation.ps1 -CloudApiUrl "http://169.58.93.203:1804"
param(
    [int]$LocalPort = 5096,
    [int]$BootstrapPort = 5050,
    [string]$CloudApiUrl = "http://169.58.93.203:1804",
    [string]$MigrationEndUtc = "2026-12-31T23:59:59Z",
    [switch]$StrictDiscovery
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$mobileDir = $PSScriptRoot
$outDir = Join-Path $projectRoot "_run\mobile-validation-apk"

$flutterRoot = if (Test-Path "D:\flutter\bin\flutter.bat") {
    "D:\flutter"
} elseif (Test-Path "$env:LOCALAPPDATA\flutter\bin\flutter.bat") {
    "$env:LOCALAPPDATA\flutter"
} else {
    $null
}

if (-not $flutterRoot) {
    Write-Error 'Flutter introuvable (D:\flutter ou %LOCALAPPDATA%\flutter).'
}

$pubCache = "D:\pub_cache"
$buildHome = "D:\build_home"
foreach ($dir in @($pubCache, $buildHome, $outDir)) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

subst A: "$env:LOCALAPPDATA\Android\Sdk" 2>$null | Out-Null
$env:USERPROFILE = $buildHome
$env:HOME = $buildHome
$env:PUB_CACHE = $pubCache
$env:ANDROID_HOME = "A:\"
$env:ANDROID_SDK_ROOT = "A:\"
$env:Path = "$flutterRoot\bin;A:\platform-tools;" + $env:Path

function Get-LanIpv4List {
    @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike "127.*" -and
            $_.IPAddress -notlike "169.254.*" -and
            $_.InterfaceAlias -notmatch "WSL|vEthernet|Default Switch|Virtual|Loopback|Bluetooth|VirtualBox"
        } |
        Sort-Object {
            if ($_.InterfaceAlias -match "Wi-?Fi|WLAN" -and $_.IPAddress -like "192.168.*") { 0 }
            elseif ($_.IPAddress -like "192.168.*") { 1 }
            elseif ($_.IPAddress -like "10.*" -and $_.InterfaceAlias -match "Ethernet") { 2 }
            elseif ($_.IPAddress -like "10.*") { 3 }
            else { 4 }
        } |
        Select-Object -ExpandProperty IPAddress -Unique)
}

$ips = Get-LanIpv4List
if (-not $ips -or $ips.Count -eq 0) {
    Write-Warning 'Aucune IP LAN detectee — rebuild avec IP manuelle si besoin.'
    $ips = @("192.168.1.1")
}

$lanLocal = "http://$($ips[0]):$LocalPort"
$lanCandidates = (($ips | ForEach-Object { "http://${_}:$LocalPort" }) -join ",")
$bootstrapUrl = "http://$($ips[0]):$BootstrapPort"

Write-Host "=== Build APK validation connexion v2 ==="
Write-Host "LOCAL_API_BASE_URL      = $lanLocal"
Write-Host "LOCAL_API_CANDIDATES    = $lanCandidates"
Write-Host "CLOUD_API_BASE_URL      = $CloudApiUrl"
Write-Host "BOOTSTRAP_API_BASE_URL  = $bootstrapUrl"
Write-Host "JWT_BINDING_MIGRATION_END_UTC = $MigrationEndUtc"
Write-Host "STRICT_SCHOOL_DISCOVERY = $StrictDiscovery"

Set-Location $mobileDir
& "$flutterRoot\bin\flutter.bat" pub get

$defines = @(
    "--dart-define=LOCAL_API_BASE_URL=$lanLocal",
    "--dart-define=LOCAL_API_CANDIDATES=$lanCandidates",
    "--dart-define=CLOUD_API_BASE_URL=$CloudApiUrl",
    "--dart-define=BOOTSTRAP_API_BASE_URL=$bootstrapUrl",
    "--dart-define=JWT_BINDING_MIGRATION_END_UTC=$MigrationEndUtc"
)
if ($StrictDiscovery) {
    $defines += "--dart-define=STRICT_SCHOOL_DISCOVERY=true"
}

& "$flutterRoot\bin\flutter.bat" test test/foundations
& "$flutterRoot\bin\flutter.bat" build apk --release @defines

$apkSrc = Join-Path $mobileDir "build\app\outputs\flutter-apk\app-release.apk"
if (-not (Test-Path $apkSrc)) {
    Write-Error "APK introuvable : $apkSrc"
}

$stamp = Get-Date -Format "yyyyMMdd-HHmm"
$apkDst = Join-Path $outDir "erp-connexion-v2-validation-$stamp.apk"
Copy-Item $apkSrc $apkDst -Force

Write-Host ""
Write-Host "APK prêt : $apkDst"
Get-Item $apkDst | Format-List FullName, Length, LastWriteTime
Write-Host "Install : adb install -r $apkDst"
