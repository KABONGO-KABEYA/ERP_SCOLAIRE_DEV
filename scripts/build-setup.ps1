<#
.SYNOPSIS
  Publie Desktop + API et compile ErpScolaire.Setup dans un package pret a deployer.

.NOTES
  Par defaut: framework-dependent (plus leger). Installez .NET 8 Desktop Runtime sur les clients.
  Utilisez -SelfContained si les machines n'ont pas le runtime (package beaucoup plus lourd).
  Sortie par defaut sur C:\Temp pour eviter de saturer le disque projet (D:).

.EXAMPLE
  .\scripts\build-setup.ps1
  .\scripts\build-setup.ps1 -SelfContained
  .\scripts\build-setup.ps1 -OutputRoot "D:\Mes Projet\ERP_Administration_Scolaire_2026\dist\setup"
#>
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release',
  [string]$OutputRoot = 'C:\Temp\ERP_Scolaire_Setup',
  [switch]$SelfContained,
  [switch]$SkipBuild,
  [switch]$TryInnoSetup
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dist = $OutputRoot
$payload = Join-Path $dist 'payload'
$desktopOut = Join-Path $payload 'desktop'
$apiOut = Join-Path $payload 'api'
$setupProj = Join-Path $root 'src\SchoolManagement.Setup\SchoolManagement.Setup.csproj'
$desktopProj = Join-Path $root 'src\SchoolManagement.Desktop\SchoolManagement.Desktop.csproj'
$apiProj = Join-Path $root 'src\SchoolManagement.API\SchoolManagement.API.csproj'

$sc = if ($SelfContained) { 'true' } else { 'false' }
Write-Host ("==> Sortie : {0} (SelfContained={1})" -f $dist, $SelfContained) -ForegroundColor Cyan

if (-not $SkipBuild) {
  if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
  }
  New-Item -ItemType Directory -Path $desktopOut, $apiOut -Force | Out-Null

  Write-Host '==> Publish Desktop...' -ForegroundColor Cyan
  dotnet publish $desktopProj `
    -c $Configuration `
    -r win-x64 `
    --self-contained $sc `
    -p:PublishSingleFile=false `
    -o $desktopOut
  if ($LASTEXITCODE -ne 0) { throw "Publish Desktop echoue ($LASTEXITCODE)" }

  Write-Host '==> Publish API...' -ForegroundColor Cyan
  dotnet publish $apiProj `
    -c $Configuration `
    -r win-x64 `
    --self-contained $sc `
    -p:PublishSingleFile=false `
    -o $apiOut
  if ($LASTEXITCODE -ne 0) { throw "Publish API echoue ($LASTEXITCODE)" }

  $deskSettings = Join-Path $desktopOut 'appsettings.json'
  if (Test-Path $deskSettings) {
    try {
      $json = Get-Content $deskSettings -Raw | ConvertFrom-Json
      if ($json.Dev) {
        $json.Dev.AutoLogin = $false
        $json.Dev.UserName = ''
        $json.Dev.Password = ''
        $json | ConvertTo-Json -Depth 20 | Set-Content $deskSettings -Encoding UTF8
        Write-Host '  appsettings Desktop : AutoLogin desactive.' -ForegroundColor DarkGray
      }
    } catch {
      Write-Warning "Impossible de neutraliser Dev.AutoLogin : $_"
    }
  }
}

# Copier script SQL virgin dans le payload
$sqlSrc = Join-Path $root 'database\scripts\010_Purge_Production_Virgin.sql'
$sqlOut = Join-Path $payload 'sql'
New-Item -ItemType Directory -Force -Path $sqlOut | Out-Null
if (Test-Path $sqlSrc) {
  Copy-Item $sqlSrc (Join-Path $sqlOut '010_Purge_Production_Virgin.sql') -Force
}

Write-Host '==> Build Setup wizard...' -ForegroundColor Cyan
# IMPORTANT: pas de PublishSingleFile — SqlClient (TdsParser/SNI) plante sinon.
$setupOut = Join-Path $dist '_setup_build'
if (Test-Path $setupOut) { Remove-Item $setupOut -Recurse -Force }
dotnet publish $setupProj `
  -c $Configuration `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=false `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $setupOut
if ($LASTEXITCODE -ne 0) { throw "Publish Setup echoue ($LASTEXITCODE)" }

$setupExe = Join-Path $setupOut 'ErpScolaire.Setup.exe'
if (-not (Test-Path $setupExe)) {
  throw "ErpScolaire.Setup.exe introuvable dans $setupOut"
}
# Copier exe + DLLs + runtimes/SNI a la racine du package (a cote de payload\)
Get-ChildItem $setupOut | ForEach-Object {
  Copy-Item $_.FullName -Destination $dist -Recurse -Force
}
Remove-Item $setupOut -Recurse -Force -ErrorAction SilentlyContinue

$generatedAt = Get-Date -Format 'yyyy-MM-dd HH:mm'
$runtimeNote = if ($SelfContained) {
  'Runtime: inclus (self-contained).'
} else {
  'Runtime: .NET 8 Desktop Runtime requis sur chaque PC (https://dotnet.microsoft.com/download/dotnet/8.0).'
}
$readmeLines = @(
  'ERP Scolaire - Package d installation',
  '=====================================',
  '',
  $runtimeNote,
  '',
  '1. Copiez TOUT ce dossier sur la machine cible (USB / reseau).',
  '2. Clic droit sur ErpScolaire.Setup.exe -> Executer en tant qu administrateur.',
  '3. Choisissez le role :',
  '   - Poste client  -> Desktop seulement (decouverte API ecole si possible)',
  '   - Serveur ecole -> API (service Windows) + Desktop + pare-feu + SQL',
  '',
  'Pre-requis serveur :',
  '- SQL Server installe (Express ou superieur)',
  '- Ports 5096 (et 5041) libres',
  '',
  'Structure :',
  '  ErpScolaire.Setup.exe',
  '  payload\desktop\',
  '  payload\api\',
  '',
  "Genere le : $generatedAt"
)
Set-Content -Path (Join-Path $dist 'LISEZMOI.txt') -Value $readmeLines -Encoding UTF8

if ($TryInnoSetup) {
  $pf86 = ${env:ProgramFiles(x86)}
  $candidates = @(
    (Join-Path $pf86 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
  )
  $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

  if ($iscc) {
    Write-Host '==> Compilation Inno Setup...' -ForegroundColor Cyan
    & $iscc (Join-Path $root 'scripts\erp-scolaire-setup.iss')
  } else {
    Write-Warning 'ISCC.exe introuvable - package dossier uniquement (OK pour deploiement).'
  }
}

$sizeMb = [math]::Round(((Get-ChildItem $dist -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host ''
Write-Host ("OK - Package pret : {0} ({1} Mo)" -f $dist, $sizeMb) -ForegroundColor Green
Write-Host ("  Lancer : {0}\ErpScolaire.Setup.exe (Admin)" -f $dist) -ForegroundColor Green
