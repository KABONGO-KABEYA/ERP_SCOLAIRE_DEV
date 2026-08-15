<#
.SYNOPSIS
  Produit les artifacts ZIP API + Migration d'une release (Lot 2B-2).

.DESCRIPTION
  Ne deploie rien. Ne cree pas d'Update Agent.
  Sortie immuable : dist/releases/{channel}/{version}/
  Le catalogue Bootstrap recoit ensuite release-bundle.json (POST /api/v1/releases).

.EXAMPLE
  .\scripts\pack-release-artifacts.ps1 -Version 1.2.0
  .\scripts\pack-release-artifacts.ps1 -Version 1.2.0 -BaseUrl 'https://example.com/releases'
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$Version,
  [ValidateSet('DEV', 'PROD')]
  [string]$Channel = 'PROD',
  [string]$OutputRoot = '',
  [string]$BaseUrl = '',
  [string]$Runtime = 'win-x64',
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release',
  [switch]$SelfContained,
  [switch]$SkipBuild,
  [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
if (-not $OutputRoot) {
  $OutputRoot = Join-Path $root 'dist\releases'
}

function Read-CsConst([string]$path, [string]$name) {
  if (-not (Test-Path $path)) { throw "Fichier introuvable : $path" }
  $text = Get-Content -Raw -Path $path
  if ($text -notmatch "$name\s*=\s*(\d+)") {
    throw "Constante $name introuvable dans $path"
  }
  return [int]$Matches[1]
}

function Get-NormalizedProductVersion([string]$dllPath) {
  $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dllPath)
  $raw = $info.ProductVersion
  if ([string]::IsNullOrWhiteSpace($raw)) {
    $raw = $info.FileVersion
  }
  if ($raw -match '\+') { $raw = $raw.Split('+')[0] }
  return $raw.Trim()
}

function Get-FileSha256([string]$path) {
  return (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant()
}

function New-ZipFromDirectory([string]$sourceDir, [string]$zipPath) {
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
  [System.IO.Compression.ZipFile]::CreateFromDirectory(
    $sourceDir,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)
}

$requiredSchema = Read-CsConst (Join-Path $root 'src\SchoolManagement.Updates\AppSchemaContract.cs') 'RequiredSchemaVersion'
$protocolVersion = Read-CsConst (Join-Path $root 'src\SchoolManagement.Application\ServerIdentity\ConnectionProtocolConstants.cs') 'ProtocolVersion'

$migSrc = Join-Path $root 'database\migrations\app'
$manifestPath = Join-Path $migSrc 'manifest.json'
if (-not (Test-Path $manifestPath)) {
  throw "Manifest migrations introuvable : $manifestPath"
}
$migManifest = Get-Content -Raw -Path $manifestPath | ConvertFrom-Json
$fromSchema = [int]$migManifest.fromSchemaVersion
$toSchema = [int]$migManifest.toSchemaVersion
if ($toSchema -ne $requiredSchema) {
  throw "toSchemaVersion $toSchema ≠ AppSchemaContract.RequiredSchemaVersion $requiredSchema"
}
if ($fromSchema -lt 1 -or $toSchema -lt $fromSchema) {
  throw "from/to schema invalides ($fromSchema → $toSchema)"
}

$outDir = Join-Path $OutputRoot (Join-Path $Channel $Version)
$apiZipName = "SchoolManagement.API-$Version-$Runtime.zip"
$migZipName = "SchoolManagement.Migration-$Version-schema-$fromSchema-$toSchema.zip"
$apiZip = Join-Path $outDir $apiZipName
$migZip = Join-Path $outDir $migZipName

if ((Test-Path $apiZip) -or (Test-Path $migZip)) {
  if (-not $Force) {
    throw "Release $Channel $Version existe deja dans $outDir (immuable). Utilisez -Force pour recreer en local uniquement."
  }
}

$staging = Join-Path $outDir '_staging'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
$apiPublish = Join-Path $staging 'api-publish'
$apiStage = Join-Path $staging 'api-zip'
$migStage = Join-Path $staging 'mig-zip'
New-Item -ItemType Directory -Force -Path $outDir, $apiStage, $migStage | Out-Null

$apiProj = Join-Path $root 'src\SchoolManagement.API\SchoolManagement.API.csproj'
$sc = if ($SelfContained) { 'true' } else { 'false' }

if (-not $SkipBuild) {
  Write-Host "==> Publish API $Version ($Runtime)..." -ForegroundColor Cyan
  if (Test-Path $apiPublish) { Remove-Item $apiPublish -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $apiPublish | Out-Null
  dotnet publish $apiProj `
    -c $Configuration `
    -r $Runtime `
    --self-contained $sc `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    -o $apiPublish
  if ($LASTEXITCODE -ne 0) { throw "Publish API echoue ($LASTEXITCODE)" }
} else {
  if (-not (Test-Path $apiPublish)) {
    throw "SkipBuild : dossier publish introuvable ($apiPublish). Lancez sans -SkipBuild."
  }
}

$dll = Join-Path $apiPublish 'SchoolManagement.API.dll'
if (-not (Test-Path $dll)) { throw "SchoolManagement.API.dll introuvable dans $apiPublish" }
$actualVersion = Get-NormalizedProductVersion $dll
if ($actualVersion -ne $Version) {
  throw "Version API $actualVersion ≠ release $Version"
}

$secretNames = @('ServeurDonnees.txt', 'ServeurDonneesCloud.txt', 'ServeurFichiers.txt')
Get-ChildItem $apiPublish | ForEach-Object {
  if ($secretNames -contains $_.Name) { return }
  Copy-Item $_.FullName -Destination (Join-Path $apiStage $_.Name) -Recurse -Force
}

$apiManifest = @{
  artifactType           = 'Api'
  releaseVersion         = $Version
  requiredSchemaVersion  = $requiredSchema
  protocolVersion        = $protocolVersion
  runtime                = $Runtime
} | ConvertTo-Json -Depth 5
Set-Content -Path (Join-Path $apiStage 'api-manifest.json') -Value $apiManifest -Encoding UTF8

Copy-Item (Join-Path $migSrc '*') -Destination $migStage -Force
$files = @()
$listed = @()
if ($migManifest.migrations) {
  foreach ($name in $migManifest.migrations) {
    $sqlPath = Join-Path $migStage $name
    if (-not (Test-Path $sqlPath)) { throw "SQL manquant : $name" }
    $listed += $name
    $files += @{ name = $name; sha256 = (Get-FileSha256 $sqlPath) }
  }
}

$packedManifest = @{
  schemaVersion      = $toSchema
  fromSchemaVersion  = $fromSchema
  toSchemaVersion    = $toSchema
  releaseVersion     = $Version
  migrations         = $listed
  files              = $files
} | ConvertTo-Json -Depth 6
Set-Content -Path (Join-Path $migStage 'manifest.json') -Value $packedManifest -Encoding UTF8

Write-Host '==> Zip...' -ForegroundColor Cyan
New-ZipFromDirectory $apiStage $apiZip
New-ZipFromDirectory $migStage $migZip

$apiSha = Get-FileSha256 $apiZip
$migSha = Get-FileSha256 $migZip
$apiSize = (Get-Item $apiZip).Length
$migSize = (Get-Item $migZip).Length

$base = $BaseUrl.TrimEnd('/')
if ($base) {
  $apiUrl = "$base/$Channel/$Version/$apiZipName"
  $migUrl = "$base/$Channel/$Version/$migZipName"
} else {
  $apiUrl = "https://REPLACE_HOST/releases/$Channel/$Version/$apiZipName"
  $migUrl = "https://REPLACE_HOST/releases/$Channel/$Version/$migZipName"
}

$bundle = @{
  version               = $Version
  channel               = $Channel
  protocolVersion       = $protocolVersion
  fromSchemaVersion     = $fromSchema
  schemaVersion         = $toSchema
  minimumDesktopVersion = '1.0.0'
  minimumApiVersion     = $Version
  artifacts             = @(
    @{ type = 'Api'; version = $Version; url = $apiUrl; size = $apiSize; sha256 = $apiSha }
    @{ type = 'Migration'; version = $Version; url = $migUrl; size = $migSize; sha256 = $migSha }
  )
} | ConvertTo-Json -Depth 8
Set-Content -Path (Join-Path $outDir 'release-bundle.json') -Value $bundle -Encoding UTF8

$sums = @(
  "$apiSha  $apiZipName"
  "$migSha  $migZipName"
)
Set-Content -Path (Join-Path $outDir 'SHA256SUMS') -Value $sums -Encoding ASCII

Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host ("OK - Package release {0} {1}" -f $Channel, $Version) -ForegroundColor Green
Write-Host "  $apiZip"
Write-Host "  $migZip"
Write-Host "  Deposer le dossier sur le VPS (immuable), puis POST /api/v1/releases avec release-bundle.json"
