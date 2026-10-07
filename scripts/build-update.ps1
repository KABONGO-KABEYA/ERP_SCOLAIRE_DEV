<#
.SYNOPSIS
  Publie Desktop + API et compile ErpScolaire.Update dans un package de mise à jour.

.EXAMPLE
  .\scripts\build-update.ps1
  .\scripts\build-update.ps1 -OutputRoot "dist\update" -TryInnoSetup
#>
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release',
  [string]$OutputRoot = 'dist\update',
  [string]$InnoOutputRoot = 'dist\inno',
  [string]$Version = '2026.10.5',
  [switch]$SelfContained,
  [switch]$SkipBuild,
  [switch]$TryInnoSetup
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dist = $OutputRoot
if (-not [System.IO.Path]::IsPathRooted($dist)) {
  $dist = Join-Path $root $dist
}
$dist = [System.IO.Path]::GetFullPath($dist)
$workspacePrefix = [System.IO.Path]::GetFullPath([string]$root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if ($dist.TrimEnd('\', '/') -eq [System.IO.Path]::GetPathRoot($dist).TrimEnd('\', '/')) {
  throw "Le dossier de sortie ne peut pas etre la racine d'un disque : $dist"
}
if (-not $dist.StartsWith($workspacePrefix, [System.StringComparison]::OrdinalIgnoreCase) -and -not $SkipBuild -and (Test-Path -LiteralPath $dist)) {
  throw "Refus de supprimer un dossier externe existant. Utiliser un nouveau dossier de sortie : $dist"
}
if ($dist.TrimEnd('\', '/') -eq ([string]$root).TrimEnd('\', '/')) {
  throw 'Le dossier de sortie ne peut pas etre la racine du projet.'
}
$payload = Join-Path $dist 'payload'
$desktopOut = Join-Path $payload 'desktop'
$apiOut = Join-Path $payload 'api'
$updateProj = Join-Path $root 'src\SchoolManagement.Update\SchoolManagement.Update.csproj'
$desktopProj = Join-Path $root 'src\SchoolManagement.Desktop\SchoolManagement.Desktop.csproj'
$apiProj = Join-Path $root 'src\SchoolManagement.API\SchoolManagement.API.csproj'

$sc = if ($SelfContained) { 'true' } else { 'false' }
$Version = $Version.Trim()
if ($Version -notmatch '^\d+\.\d+\.\d+([.-].+)?$') {
  throw "Version SemVer invalide: $Version"
}

Write-Host ("==> Sortie update : {0} (SelfContained={1}; Version={2})" -f $dist, $SelfContained, $Version) -ForegroundColor Cyan

function Invoke-DotnetPublish {
  param(
    [Parameter(Mandatory = $true)][string]$Project,
    [Parameter(Mandatory = $true)][string]$Output,
    [Parameter(Mandatory = $true)][string]$Runtime,
    [Parameter(Mandatory = $true)][string]$SelfContainedValue,
    [string[]]$ExtraArgs = @()
  )

  $args = @(
    'publish', $Project,
    '-c', $Configuration,
    '-r', $Runtime,
    '--self-contained', $SelfContainedValue,
    '-p:PublishSingleFile=false',
    "-p:Version=$Version",
    "-p:InformationalVersion=$Version",
    "-p:FileVersion=$(($Version -split '[-+]')[0]).0",
    '-o', $Output
  ) + $ExtraArgs
  & dotnet @args
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish echoue ($LASTEXITCODE) : $Project"
  }
}

$PayloadRuntimeConfigFiles = @(
  'ServeurDonneesCloud.txt',
  'ServeurFichiers.txt',
  'ServeurDonnees.txt',
  'appsettings.Development.json',
  'appsettings.Local.json',
  'secrets.json'
)

function Remove-UpdatePayloadRuntimeConfig {
  param([Parameter(Mandatory = $true)][string]$PayloadRoot)
  foreach ($app in @('api', 'desktop')) {
    $dir = Join-Path $PayloadRoot $app
    if (-not (Test-Path $dir)) { continue }
    Get-ChildItem -LiteralPath $dir -File -Force | Where-Object {
      ($PayloadRuntimeConfigFiles -contains $_.Name) -or
      ($_.Name -like 'appsettings.Development*.json')
    } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    $logs = Join-Path $dir 'logs'
    if (Test-Path $logs) { Remove-Item -LiteralPath $logs -Recurse -Force }
  }
}

if (-not $SkipBuild) {
  if (Test-Path -LiteralPath $dist) { Remove-Item -LiteralPath $dist -Recurse -Force }
  New-Item -ItemType Directory -Path $desktopOut, $apiOut -Force | Out-Null

  Write-Host '==> Publish Desktop...' -ForegroundColor Cyan
  Invoke-DotnetPublish -Project $desktopProj -Output $desktopOut -Runtime 'win-x64' -SelfContainedValue $sc

  Write-Host '==> Publish API...' -ForegroundColor Cyan
  Invoke-DotnetPublish -Project $apiProj -Output $apiOut -Runtime 'win-x64' -SelfContainedValue $sc

  $versionJson = Join-Path $desktopOut 'version.json'
  @{ version = $Version } | ConvertTo-Json | Set-Content $versionJson -Encoding UTF8
}

Remove-UpdatePayloadRuntimeConfig -PayloadRoot $payload

Write-Host '==> Build Update wizard...' -ForegroundColor Cyan
$updateOut = Join-Path $dist '_update_build'
if (Test-Path $updateOut) { Remove-Item $updateOut -Recurse -Force }
Invoke-DotnetPublish `
  -Project $updateProj `
  -Output $updateOut `
  -Runtime 'win-x64' `
  -SelfContainedValue 'false' `
  -ExtraArgs @('-p:IncludeNativeLibrariesForSelfExtract=true')

$updateExe = Join-Path $updateOut 'ErpScolaire.Update.exe'
if (-not (Test-Path $updateExe)) {
  throw "ErpScolaire.Update.exe introuvable dans $updateOut"
}

Get-ChildItem $updateOut | ForEach-Object {
  # Setup.dll reste une dépendance ; son lanceur ne doit pas être livré dans une mise à jour.
  if ($_.Name -eq 'ErpScolaire.Setup.exe') { return }
  Copy-Item $_.FullName -Destination $dist -Recurse -Force
}
Remove-Item $updateOut -Recurse -Force -ErrorAction SilentlyContinue

$generatedAt = Get-Date -Format 'yyyy-MM-dd HH:mm'
$readmeLines = @(
  'ERP Scolaire - MISE A JOUR (installation existante)',
  '==================================================',
  '',
  "Version cible : $Version",
  "Reference Setup : 1.0.2 (commit bf2750c)",
  '',
  'IMPORTANT',
  '---------',
  '- Executer ErpScolaire.Update.exe EN ADMINISTRATEUR sur le serveur ecole.',
  '- Ne pas utiliser ErpScolaire.Setup.exe (installation initiale / reinstallation).',
  '- La base SQL existante n est PAS recreee ni purgee.',
  '- Les SchemaInitializers de l API mettent a jour le schema au redemarrage.',
  '',
  'Etapes',
  '------',
  '  1) Copier tout le dossier sur la machine cible',
  '  2) Lancer ErpScolaire.Update.exe (Admin)',
  '  3) Confirmer la mise a jour',
  '  4) Verifier le rapport final (API, schema, Desktop, abonnement)',
  '',
  'Rollback',
  '--------',
  '  Sauvegarde binaires : %ProgramData%\ERP_SCOLAIRE\UpdateRollback\',
  '  (pas de restauration SQL automatique)',
  '',
  "Genere le : $generatedAt"
)
Set-Content -Path (Join-Path $dist 'LISEZMOI.txt') -Value $readmeLines -Encoding UTF8

if ($TryInnoSetup) {
  $pf86 = ${env:ProgramFiles(x86)}
  $candidates = @(
    (Join-Path $pf86 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
  )
  $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

  if ($iscc) {
    Write-Host '==> Compilation Inno Setup (update)...' -ForegroundColor Cyan
    $iss = Join-Path $root 'scripts\erp-scolaire-update.iss'
    $innoOut = $InnoOutputRoot
    if (-not [System.IO.Path]::IsPathRooted($innoOut)) { $innoOut = Join-Path $root $innoOut }
    $innoOut = [System.IO.Path]::GetFullPath($innoOut)
    if ($innoOut.TrimEnd('\', '/') -eq $dist.TrimEnd('\', '/') -or $innoOut.StartsWith($dist.TrimEnd('\', '/') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
      throw 'Le dossier Inno doit etre distinct du dossier source de la mise a jour.'
    }
    New-Item -ItemType Directory -Force -Path $innoOut | Out-Null

    $isccArgs = @(
      "/DUpdateSourceDir=$dist",
      "/DInnoOutputDir=$innoOut",
      "/DMyAppVersion=$Version",
      $iss
    )
    & $iscc @isccArgs
    if ($LASTEXITCODE -ne 0) {
      throw "ISCC.exe a echoue ($LASTEXITCODE)"
    }

    $expectedPath = Join-Path $innoOut "ERP_Scolaire_Update_$Version.exe"
    if (-not (Test-Path $expectedPath)) {
      throw "Inno Setup n'a pas produit $expectedPath"
    }
    Write-Host ("  Inno OK : {0}" -f $expectedPath) -ForegroundColor Green
  } else {
    Write-Warning 'ISCC.exe introuvable - package dossier uniquement.'
  }
}

$sizeMb = [math]::Round(((Get-ChildItem $dist -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host ''
Write-Host ("OK - Package update pret : {0} ({1} Mo)" -f $dist, $sizeMb) -ForegroundColor Green
Write-Host ("  Lancer : {0}\ErpScolaire.Update.exe (Admin)" -f $dist) -ForegroundColor Green
