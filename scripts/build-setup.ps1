<#
.SYNOPSIS
  Publie Desktop + API et compile ErpScolaire.Setup dans un package pret a deployer.

.NOTES
  Par defaut: framework-dependent (plus leger). Installez .NET 8 Desktop Runtime sur les clients.
  Utilisez -SelfContained si les machines n'ont pas le runtime (package beaucoup plus lourd).
  Sortie par defaut sur C:\Temp pour eviter de saturer le disque projet (D:).
  -Version (optionnel) : stamp MSBuild temporaire (Version / InformationalVersion) au publish,
  sans modifier les .csproj ni version.json sur disque.

.EXAMPLE
  .\scripts\build-setup.ps1
  .\scripts\build-setup.ps1 -SelfContained
  .\scripts\build-setup.ps1 -OutputRoot "D:\Mes Projet\ERP_Administration_Scolaire_2026\dist\setup"
  .\scripts\build-setup.ps1 -Version 1.2.0 -OutputRoot "dist\setup" -TryInnoSetup
#>
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release',
  [string]$OutputRoot = 'C:\Temp\ERP_Scolaire_Setup',
  [string]$Version = '',
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
$payload = Join-Path $dist 'payload'
$desktopOut = Join-Path $payload 'desktop'
$apiOut = Join-Path $payload 'api'
$setupProj = Join-Path $root 'src\SchoolManagement.Setup\SchoolManagement.Setup.csproj'
$desktopProj = Join-Path $root 'src\SchoolManagement.Desktop\SchoolManagement.Desktop.csproj'
$apiProj = Join-Path $root 'src\SchoolManagement.API\SchoolManagement.API.csproj'

$sc = if ($SelfContained) { 'true' } else { 'false' }
$hasVersion = -not [string]::IsNullOrWhiteSpace($Version)
if ($hasVersion) {
  $Version = $Version.Trim()
  if ($Version -notmatch '^\d+\.\d+\.\d+([.-].+)?$') {
    throw "Version SemVer invalide: $Version (attendu X.Y.Z)"
  }
}

Write-Host ("==> Sortie : {0} (SelfContained={1}; Version={2})" -f $dist, $SelfContained, $(if ($hasVersion) { $Version } else { '(csproj)' })) -ForegroundColor Cyan

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
    '-p:PublishSingleFile=false'
  ) + $ExtraArgs

  if ($hasVersion) {
    $args += "-p:Version=$Version"
    $args += "-p:InformationalVersion=$Version"
  }

  $args += @('-o', $Output)
  & dotnet @args
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish echoue ($LASTEXITCODE) : $Project"
  }
}

# Fichiers runtime locaux (machine de build) : le Setup les ecrit a l'installation.
# dotnet publish les recopie via CopyToOutputDirectory — ils ne doivent jamais partir dans le payload.
$script:PayloadRuntimeConfigFiles = @(
  'ServeurDonneesCloud.txt',
  'ServeurFichiers.txt',
  'ServeurDonnees.txt',
  'appsettings.Development.json',
  'appsettings.Local.json',
  'secrets.json'
)

function Remove-InstallerPayloadRuntimeConfig {
  param([Parameter(Mandatory = $true)][string]$PayloadRoot)

  if (-not (Test-Path $PayloadRoot)) {
    throw "Payload introuvable pour sanitisation : $PayloadRoot"
  }

  $removed = New-Object System.Collections.Generic.List[string]
  foreach ($app in @('api', 'desktop')) {
    $dir = Join-Path $PayloadRoot $app
    if (-not (Test-Path $dir)) { continue }

    Get-ChildItem -LiteralPath $dir -File -Force | Where-Object {
      ($script:PayloadRuntimeConfigFiles -contains $_.Name) -or
      ($_.Name -like 'appsettings.Development*.json')
    } | ForEach-Object {
      Remove-Item -LiteralPath $_.FullName -Force
      $removed.Add("$app/$($_.Name)")
    }

    $logs = Join-Path $dir 'logs'
    if (Test-Path $logs) {
      Remove-Item -LiteralPath $logs -Recurse -Force
      $removed.Add("$app/logs/")
    }
  }

  if ($removed.Count -gt 0) {
    Write-Host ('  Payload : configuration runtime locale retiree ({0})' -f ($removed -join ', ')) -ForegroundColor DarkGray
  }

  foreach ($app in @('api', 'desktop')) {
    $dir = Join-Path $PayloadRoot $app
    if (-not (Test-Path $dir)) { continue }
    foreach ($name in $script:PayloadRuntimeConfigFiles) {
      $path = Join-Path $dir $name
      if (Test-Path $path) {
        throw "Le payload contient encore $app/$name : build refuse."
      }
    }
  }

  $danger = @(
    'MOTDEPASSE=',
    'HEROS_SQL19',
    'Desktop-ct9vndv',
    'SchoolManagementRDC_Development',
    'CHRISTIAN KABONGO'
  )
  $hits = New-Object System.Collections.Generic.List[string]
  foreach ($app in @('api', 'desktop')) {
    $dir = Join-Path $PayloadRoot $app
    if (-not (Test-Path $dir)) { continue }
    Get-ChildItem -LiteralPath $dir -File -Force | Where-Object {
      $_.Extension -in '.txt', '.json', '.config' -and
      $_.Name -notmatch '\.(deps|runtimeconfig)\.json$'
    } | ForEach-Object {
      $text = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue
      if ([string]::IsNullOrWhiteSpace($text)) { return }
      foreach ($token in $danger) {
        if ($text.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
          $hits.Add("$app/$($_.Name) ($token)")
        }
      }
    }
  }
  if ($hits.Count -gt 0) {
    throw ("Le payload contient encore une configuration locale/secrete : " + ($hits -join '; '))
  }
}

if (-not $SkipBuild) {
  if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
  }
  New-Item -ItemType Directory -Path $desktopOut, $apiOut -Force | Out-Null

  Write-Host '==> Publish Desktop...' -ForegroundColor Cyan
  Invoke-DotnetPublish -Project $desktopProj -Output $desktopOut -Runtime 'win-x64' -SelfContainedValue $sc

  Write-Host '==> Publish API...' -ForegroundColor Cyan
  Invoke-DotnetPublish -Project $apiProj -Output $apiOut -Runtime 'win-x64' -SelfContainedValue $sc

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

Remove-InstallerPayloadRuntimeConfig -PayloadRoot $payload

# Scripts SQL du payload Setup : baseline obligatoire + purge optionnelle (réinstall).
$sqlOut = Join-Path $payload 'sql'
New-Item -ItemType Directory -Force -Path $sqlOut | Out-Null
$baselineSrc = Join-Path $root 'database\scripts\001_InitialCreate_EF.sql'
if (-not (Test-Path $baselineSrc)) {
  throw "Baseline SQL introuvable : $baselineSrc"
}
Copy-Item $baselineSrc (Join-Path $sqlOut '001_InitialCreate_EF.sql') -Force
$sqlVirginSrc = Join-Path $root 'database\scripts\010_Purge_Production_Virgin.sql'
if (Test-Path $sqlVirginSrc) {
  Copy-Item $sqlVirginSrc (Join-Path $sqlOut '010_Purge_Production_Virgin.sql') -Force
}

Write-Host '==> Build Setup wizard...' -ForegroundColor Cyan
# IMPORTANT: pas de PublishSingleFile — SqlClient (TdsParser/SNI) plante sinon.
$setupOut = Join-Path $dist '_setup_build'
if (Test-Path $setupOut) { Remove-Item $setupOut -Recurse -Force }
Invoke-DotnetPublish `
  -Project $setupProj `
  -Output $setupOut `
  -Runtime 'win-x64' `
  -SelfContainedValue 'false' `
  -ExtraArgs @('-p:IncludeNativeLibrariesForSelfExtract=true')

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
$versionNote = if ($hasVersion) { "Version package : $Version" } else { 'Version package : (csproj / assembly)' }
$readmeLines = @(
  'ERP Scolaire - Package d installation',
  '=====================================',
  '',
  $runtimeNote,
  $versionNote,
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
    $iss = Join-Path $root 'scripts\erp-scolaire-setup.iss'
    $innoOut = Join-Path $root 'dist\inno'
    New-Item -ItemType Directory -Force -Path $innoOut | Out-Null

    $isccArgs = @(
      "/DSetupSourceDir=$dist",
      "/DInnoOutputDir=$innoOut"
    )
    if ($hasVersion) {
      $isccArgs += "/DMyAppVersion=$Version"
    }
    $isccArgs += $iss

    & $iscc @isccArgs
    if ($LASTEXITCODE -ne 0) {
      throw "ISCC.exe a echoue ($LASTEXITCODE)"
    }

    $expectedName = if ($hasVersion) { "DesktopSetup-$Version.exe" } else { 'DesktopSetup-1.0.0.exe' }
    $expectedPath = Join-Path $innoOut $expectedName
    if (-not (Test-Path $expectedPath)) {
      throw "Inno Setup n'a pas produit $expectedPath"
    }
    Write-Host ("  Inno OK : {0}" -f $expectedPath) -ForegroundColor Green
  } else {
    Write-Warning 'ISCC.exe introuvable - package dossier uniquement (OK pour deploiement).'
  }
}

$sizeMb = [math]::Round(((Get-ChildItem $dist -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host ''
Write-Host ("OK - Package pret : {0} ({1} Mo)" -f $dist, $sizeMb) -ForegroundColor Green
Write-Host ("  Lancer : {0}\ErpScolaire.Setup.exe (Admin)" -f $dist) -ForegroundColor Green
