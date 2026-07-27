<#
.SYNOPSIS
  Prepare a zip archive to deploy the Cloud API with Docker on the VPS.

.EXAMPLE
  .\scripts\pack-docker-cloud.ps1
#>
param(
    [string]$OutputZip = ""
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

$envFile = Join-Path $root ".env"
if (-not (Test-Path $envFile)) {
    Write-Host "Generating .env..."
    & (Join-Path $PSScriptRoot "prepare-docker-env.ps1")
}

$staging = Join-Path $env:TEMP ("erp-docker-cloud-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $staging | Out-Null

$files = @(
    "docker-compose.yml",
    ".env.example",
    ".dockerignore",
    "Directory.Build.props",
    "docs\deploy-docker-cloud.md"
)

foreach ($rel in $files) {
    $src = Join-Path $root $rel
    if (-not (Test-Path $src)) { continue }
    $dest = Join-Path $staging $rel
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    Copy-Item $src $dest -Force
}

Copy-Item $envFile (Join-Path $staging ".env") -Force

$srcRoot = Join-Path $staging "src"
New-Item -ItemType Directory -Path $srcRoot | Out-Null
$projects = @(
    "SchoolManagement.Shared",
    "SchoolManagement.Domain",
    "SchoolManagement.Application",
    "SchoolManagement.Infrastructure",
    "SchoolManagement.API"
)
foreach ($p in $projects) {
    $from = Join-Path $root "src\$p"
    $to = Join-Path $srcRoot $p
    Write-Host "Copy $p..."
    robocopy $from $to /E /XD bin obj .vs /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed for $p (code $LASTEXITCODE)"
    }
}

$artifacts = Join-Path $root "artifacts"
if (-not (Test-Path $artifacts)) {
    New-Item -ItemType Directory -Path $artifacts | Out-Null
}
if ([string]::IsNullOrWhiteSpace($OutputZip)) {
    $OutputZip = Join-Path $artifacts "erp-api-cloud-docker.zip"
}
if (Test-Path $OutputZip) {
    Remove-Item $OutputZip -Force
}

Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $OutputZip -Force
Remove-Item $staging -Recurse -Force

Write-Host ""
Write-Host "Archive ready: $OutputZip"
Write-Host ""
Write-Host "On the Linux VPS (with Docker):"
Write-Host "  1. Copy the zip (WinSCP / scp)"
Write-Host "  2. unzip erp-api-cloud-docker.zip -d erp-api-cloud"
Write-Host "  3. cd erp-api-cloud"
Write-Host "  4. docker compose up -d --build"
Write-Host "  5. curl http://127.0.0.1:1804/api/v1/health"
Write-Host ""
Write-Host "Open firewall port 1804 (or 80/443 via reverse proxy)."
