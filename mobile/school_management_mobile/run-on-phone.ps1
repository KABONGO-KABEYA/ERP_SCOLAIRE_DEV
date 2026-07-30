# Lance l'app Flutter — connexion runtime SANS dependance USB.
#
# Comportement runtime (detecte dans l'app) :
#   1. Meme Wi-Fi que le PC serveur → Mode Local (IP LAN :5041)
#   2. Autre reseau (4G / autre Wi-Fi) → Mode Distant (VPS :1804)
#   3. Pas de connexion → Mode Cache (Hive)
#
# USB / ADB sert uniquement a installer / debugger l'APK, pas a joindre l'API.
#
# Usage :
#   .\run-on-phone.ps1
#   .\run-on-phone.ps1 -LocalApiUrl "http://10.10.10.112:5041"
#   .\run-on-phone.ps1 -CloudApiUrl "http://161.97.105.22:1804"
#   .\run-on-phone.ps1 -UsbLocalTunnel   # debug ONLY : Local via USB
#   .\run-on-phone.ps1 -UsbCloudTunnel   # debug ONLY : Distant via Docker PC + USB
param(
    [string]$LocalApiUrl = "",
    [string]$CloudApiUrl = "",
    [string]$DeviceId = "1627425639012915",
    [switch]$SkipClean,
    [switch]$UsbLocalTunnel,
    [switch]$UsbCloudTunnel
)

$ErrorActionPreference = "Stop"

$projectRoot = "D:\Mes Projet\ERP_Administration_Scolaire_2026"
$mobileDir = Join-Path $projectRoot "mobile\school_management_mobile"
$flutterRoot = "D:\flutter"
$pubCache = "D:\pub_cache"
$buildHome = "D:\build_home"
$defaultCloudUrl = "http://169.58.93.203:1804"

if (-not (Test-Path "$flutterRoot\bin\flutter.bat")) {
    Write-Error "Flutter introuvable dans D:\flutter. Relancez l'installation Flutter."
}

subst A: "$env:LOCALAPPDATA\Android\Sdk" 2>$null | Out-Null

foreach ($dir in @($pubCache, $buildHome)) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

$env:USERPROFILE = $buildHome
$env:HOME = $buildHome
$env:PUB_CACHE = $pubCache
$env:ANDROID_HOME = "A:\"
$env:ANDROID_SDK_ROOT = "A:\"
$env:Path = "$flutterRoot\bin;A:\platform-tools;" + $env:Path

$adb = "A:\platform-tools\adb.exe"

function Get-LanIpv4List {
    @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike "127.*" -and
            $_.IPAddress -notlike "169.254.*" -and
            $_.InterfaceAlias -notmatch "WSL|vEthernet|Default Switch|Virtual|Loopback|Bluetooth|VirtualBox"
        } |
        Sort-Object {
            # Prefer Wi-Fi / hotspot first (phone is often on 192.168.137.x), then Ethernet.
            if ($_.InterfaceAlias -match "Wi-?Fi|WLAN" -and $_.IPAddress -like "192.168.*") { 0 }
            elseif ($_.IPAddress -like "192.168.*") { 1 }
            elseif ($_.IPAddress -like "10.*" -and $_.InterfaceAlias -match "Ethernet") { 2 }
            elseif ($_.IPAddress -like "10.*") { 3 }
            else { 4 }
        } |
        Select-Object -ExpandProperty IPAddress -Unique)
}

function Get-LanApiBaseUrl {
    $ips = Get-LanIpv4List
    if (-not $ips -or $ips.Count -eq 0) { return "http://192.168.137.33:5041" }
    return "http://$($ips[0]):5041"
}

function Get-LanApiCandidates {
    $ips = Get-LanIpv4List
    if (-not $ips -or $ips.Count -eq 0) {
        return "http://192.168.137.33:5041,http://10.10.10.112:5041"
    }
    return (($ips | ForEach-Object { "http://${_}:5041" }) -join ",")
}

# Nettoie les tunnels USB par defaut (runtime Wi-Fi / 4G uniquement).
$prevEap = $ErrorActionPreference
$ErrorActionPreference = 'SilentlyContinue'
foreach ($port in @(5041, 1804, 8080)) {
    & $adb -s $DeviceId reverse --remove "tcp:$port" 2>&1 | Out-Null
}
$ErrorActionPreference = $prevEap

if ($UsbLocalTunnel) {
    & $adb -s $DeviceId reverse tcp:5041 tcp:5041
    Write-Host 'UsbLocalTunnel ON : Local via USB (127.0.0.1:5041) - debug only.'
}

if ($UsbCloudTunnel) {
    & $adb -s $DeviceId reverse tcp:1804 tcp:1804
    Write-Host 'UsbCloudTunnel ON : Distant via USB (127.0.0.1:1804) - debug only.'
}

$defines = @()
if ($LocalApiUrl) {
    $defines += "--dart-define=LOCAL_API_BASE_URL=$LocalApiUrl"
    $defines += "--dart-define=LOCAL_API_CANDIDATES=$LocalApiUrl"
} elseif ($UsbLocalTunnel) {
    $defines += "--dart-define=LOCAL_API_BASE_URL=http://127.0.0.1:5041"
} else {
    $lanLocal = Get-LanApiBaseUrl
    $lanCandidates = Get-LanApiCandidates
    Write-Host "Local API (prioritaire) : $lanLocal"
    Write-Host "Local candidates (toutes IP PC) : $lanCandidates"
    $defines += "--dart-define=LOCAL_API_BASE_URL=$lanLocal"
    $defines += "--dart-define=LOCAL_API_CANDIDATES=$lanCandidates"
}

if ($CloudApiUrl) {
    $defines += "--dart-define=CLOUD_API_BASE_URL=$CloudApiUrl"
} elseif ($env:CLOUD_API_BASE_URL) {
    $defines += "--dart-define=CLOUD_API_BASE_URL=$($env:CLOUD_API_BASE_URL)"
} elseif ($UsbCloudTunnel) {
    $defines += "--dart-define=CLOUD_API_BASE_URL=http://127.0.0.1:1804"
} else {
    Write-Host "Distant API (4G / autre Wi-Fi) : $defaultCloudUrl"
    $defines += "--dart-define=CLOUD_API_BASE_URL=$defaultCloudUrl"
}

Write-Host "Defines: $($defines -join ' ')"
Write-Host "Runtime : Local (meme Wi-Fi) → Distant (autre reseau) → Mode Cache (hors ligne)."

Set-Location $mobileDir
if (-not $SkipClean) {
    & "$flutterRoot\bin\flutter.bat" clean
}
& "$flutterRoot\bin\flutter.bat" pub get
& "$flutterRoot\bin\flutter.bat" run -d $DeviceId @defines
