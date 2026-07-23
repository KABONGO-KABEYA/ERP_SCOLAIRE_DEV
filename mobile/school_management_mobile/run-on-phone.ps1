# Lance l'app Flutter sur le telephone USB (contourne les espaces dans les chemins Windows).
# Usage :
#   .\run-on-phone.ps1
#   .\run-on-phone.ps1 -LocalApiUrl "http://10.10.10.112:5041"
#   .\run-on-phone.ps1 -CloudApiUrl "https://api.votredomaine.com"
#   .\run-on-phone.ps1 -UsbLocalTunnel   # debug only : force Local via USB meme hors Wi-Fi
#
# Comportement normal :
#   - Local = IP LAN du PC (meme Wi-Fi que l'ecole)
#   - Hors Wi-Fi ecole => bascule Cloud (lecture seule)
#   - Cloud par defaut = Docker local :8080 via tunnel USB (tant que le VPS public n'est pas pret)
#   - 4G sans USB : deployer l'API Cloud sur VPS, puis -CloudApiUrl "http(s)://IP_OU_DOMAINE:8080"
param(
    [string]$LocalApiUrl = "",
    [string]$CloudApiUrl = "",
    [string]$DeviceId = "1627425639012915",
    [switch]$SkipClean,
    [switch]$UsbLocalTunnel
)

$ErrorActionPreference = "Stop"

$projectRoot = "D:\Mes Projet\ERP_Administration_Scolaire_2026"
$mobileDir = Join-Path $projectRoot "mobile\school_management_mobile"
$flutterRoot = "D:\flutter"
$pubCache = "D:\pub_cache"
$buildHome = "D:\build_home"

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

function Get-LanApiBaseUrl {
    $ip = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike "127.*" -and
            $_.IPAddress -notlike "169.254.*" -and
            $_.InterfaceAlias -notmatch "WSL|vEthernet|Default Switch|Virtual|Loopback|Bluetooth"
        } |
        Sort-Object {
            if ($_.IPAddress -like "10.*") { 0 }
            elseif ($_.IPAddress -like "192.168.*") { 1 }
            else { 2 }
        } |
        Select-Object -First 1 -ExpandProperty IPAddress

    if (-not $ip) { return "http://10.10.10.112:5041" }
    return "http://${ip}:5041"
}

# Cloud Docker local (test bascule) via USB. Ne force PAS le mode Local hors Wi-Fi.
& $adb -s $DeviceId reverse --remove tcp:5041 2>$null | Out-Null
& $adb -s $DeviceId reverse tcp:8080 tcp:8080

if ($UsbLocalTunnel) {
    & $adb -s $DeviceId reverse tcp:5041 tcp:5041
    Write-Host "UsbLocalTunnel ON : Local via USB (127.0.0.1:5041) - debug only."
}

$defines = @()
if ($LocalApiUrl) {
    $defines += "--dart-define=LOCAL_API_BASE_URL=$LocalApiUrl"
} elseif ($UsbLocalTunnel) {
    $defines += "--dart-define=LOCAL_API_BASE_URL=http://127.0.0.1:5041"
} else {
    $lanLocal = Get-LanApiBaseUrl
    Write-Host "Local API (Wi-Fi ecole) : $lanLocal"
    $defines += "--dart-define=LOCAL_API_BASE_URL=$lanLocal"
}

if ($CloudApiUrl) {
    $defines += "--dart-define=CLOUD_API_BASE_URL=$CloudApiUrl"
} elseif ($env:CLOUD_API_BASE_URL) {
    $defines += "--dart-define=CLOUD_API_BASE_URL=$($env:CLOUD_API_BASE_URL)"
} else {
    # Docker Cloud sur le PC, joignable hors Wi-Fi tant que le USB est branche.
    $defines += "--dart-define=CLOUD_API_BASE_URL=http://127.0.0.1:8080"
}

Write-Host "Defines: $($defines -join ' ')"

Set-Location $mobileDir
if (-not $SkipClean) {
    & "$flutterRoot\bin\flutter.bat" clean
}
& "$flutterRoot\bin\flutter.bat" pub get
& "$flutterRoot\bin\flutter.bat" run -d $DeviceId @defines
