# Lance l'app Flutter sur le telephone USB (contourne les espaces dans les chemins Windows).
# Usage :
#   .\run-on-phone.ps1
#   .\run-on-phone.ps1 -LocalApiUrl "http://10.10.10.112:5041"
#   .\run-on-phone.ps1 -CloudApiUrl "https://api.votredomaine.com"
#
# Defaut USB : Local=127.0.0.1:5041 + Cloud=127.0.0.1:8080 (tunnels adb).
# Pour la 4G sans USB : deployer l'API Cloud sur un VPS public, puis -CloudApiUrl "http(s)://IP_OU_DOMAINE:8080".
param(
    [string]$LocalApiUrl = "",
    [string]$CloudApiUrl = "",
    [string]$DeviceId = "1627425639012915",
    [switch]$SkipClean
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

# Tunnel USB : le telephone atteint l'API PC via 127.0.0.1 meme sans Wi‑Fi.
& "A:\platform-tools\adb.exe" -s $DeviceId reverse tcp:5041 tcp:5041
# API Cloud Docker locale (port 8080) — utile pour tester la bascule hors API Locale.
& "A:\platform-tools\adb.exe" -s $DeviceId reverse tcp:8080 tcp:8080

$defines = @()
if ($LocalApiUrl) {
    $defines += "--dart-define=LOCAL_API_BASE_URL=$LocalApiUrl"
} else {
    # Defaut : tunnel adb (fonctionne sans Wi‑Fi tant que le USB est branche).
    $defines += "--dart-define=LOCAL_API_BASE_URL=http://127.0.0.1:5041"
}
if ($CloudApiUrl) {
    $defines += "--dart-define=CLOUD_API_BASE_URL=$CloudApiUrl"
} elseif ($env:CLOUD_API_BASE_URL) {
    $defines += "--dart-define=CLOUD_API_BASE_URL=$($env:CLOUD_API_BASE_URL)"
} else {
    # Defaut : API Cloud Docker sur le PC (compose :8080), via tunnel USB.
    $defines += "--dart-define=CLOUD_API_BASE_URL=http://127.0.0.1:8080"
}

Set-Location $mobileDir
if (-not $SkipClean) {
    & "$flutterRoot\bin\flutter.bat" clean
}
& "$flutterRoot\bin\flutter.bat" pub get
& "$flutterRoot\bin\flutter.bat" run -d $DeviceId @defines
