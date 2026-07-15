# Lance l'app Flutter sur le telephone USB (contourne les espaces dans les chemins Windows).
$ErrorActionPreference = "Stop"

$projectRoot = "D:\Mes Projet\ERP_Administration_Scolaire_2026"
$mobileDir = Join-Path $projectRoot "mobile\school_management_mobile"
$flutterRoot = "D:\flutter"
$pubCache = "D:\pub_cache"
$buildHome = "D:\build_home"
$deviceId = "1627425639012915"

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

& "A:\platform-tools\adb.exe" -s $deviceId reverse tcp:5041 tcp:5041

Set-Location $mobileDir
& "$flutterRoot\bin\flutter.bat" clean
& "$flutterRoot\bin\flutter.bat" pub get
& "$flutterRoot\bin\flutter.bat" run -d $deviceId --dart-define=API_BASE_URL=http://127.0.0.1:5041
