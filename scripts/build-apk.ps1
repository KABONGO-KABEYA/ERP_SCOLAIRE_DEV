<#
.SYNOPSIS
  Genere l'APK release de l'app mobile ERP Scolaire.

.NOTES
  Pre-requis:
  - Smart App Control DESACTIVE (sinon gen_snapshot.exe est bloque)
  - Android SDK + cmdline-tools
  - Flutter dans .tools\flutter ou D:\flutter

.EXAMPLE
  .\scripts\build-apk.ps1
#>
[CmdletBinding()]
param(
  [string]$OutputDir = 'C:\Temp\erp-apk'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$proj = Join-Path $root 'mobile\school_management_mobile'

$flutterCandidates = @(
  (Join-Path $root '.tools\flutter\bin\flutter.bat'),
  'D:\flutter\bin\flutter.bat',
  "$env:LOCALAPPDATA\flutter\bin\flutter.bat"
)
$flutter = $flutterCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $flutter) { throw 'Flutter introuvable.' }

$sdk = "$env:LOCALAPPDATA\Android\Sdk"
if (-not (Test-Path $sdk)) { throw "Android SDK introuvable: $sdk" }

# Test rapide Smart App Control
$gs = Join-Path (Split-Path $flutter) 'cache\artifacts\engine\android-arm-release\windows-x64\gen_snapshot.EXE'
if (Test-Path $gs) {
  try {
    $null = Start-Process -FilePath $gs -ArgumentList '--help' -Wait -PassThru -NoNewWindow `
      -RedirectStandardOutput "$env:TEMP\gs-out.txt" -RedirectStandardError "$env:TEMP\gs-err.txt" -ErrorAction Stop
  } catch {
    throw @"
Smart App Control bloque Flutter (gen_snapshot.exe).

Desactivez-le puis redemarrez le PC:
  Parametres > Confidentialite et securite > Securite Windows
  > Controle des applications et du navigateur
  > Controle d'applications intelligent > Desactive

Ensuite relancez: .\scripts\build-apk.ps1
"@
  }
}

$env:ANDROID_HOME = $sdk
$env:ANDROID_SDK_ROOT = $sdk
$env:JAVA_HOME = 'C:\Program Files\Android\Android Studio\jbr'
$env:PUB_CACHE = 'C:\Temp\pub-cache'
$env:GRADLE_USER_HOME = 'C:\Temp\gradle-home'
New-Item -ItemType Directory -Force -Path $OutputDir, $env:PUB_CACHE, $env:GRADLE_USER_HOME | Out-Null

Set-Location $proj
Write-Host "==> flutter pub get" -ForegroundColor Cyan
& $flutter pub get
Write-Host "==> flutter build apk --release" -ForegroundColor Cyan
& $flutter build apk --release
if ($LASTEXITCODE -ne 0) { throw "build apk echoue ($LASTEXITCODE)" }

$apk = Get-ChildItem (Join-Path $proj 'build\app\outputs\flutter-apk') -Filter 'app-release.apk' | Select-Object -First 1
if (-not $apk) { throw 'app-release.apk introuvable' }

$dest = Join-Path $OutputDir 'ERP-Scolaire-Mobile-1.0.1.apk'
Copy-Item $apk.FullName $dest -Force
Write-Host ("OK - APK: {0} ({1:N1} Mo)" -f $dest, ($apk.Length/1MB)) -ForegroundColor Green
