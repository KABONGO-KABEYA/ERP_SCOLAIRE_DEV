<#
.SYNOPSIS
  Prépare le déploiement Coolify (API Cloud + SQL sur le même VPS) et la sync depuis le PC école.

.EXAMPLE
  .\scripts\setup-coolify-new-server.ps1 `
    -PublicHost "203.0.113.50" `
    -SqlSaPassword "VotreMotDePasseSA" `
    -ApiPublicPort 1804
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$PublicHost,

    [Parameter(Mandatory = $true)]
    [string]$SqlSaPassword,

    [string]$SqlDockerHost = "sqlserver",
    [int]$SqlPort = 1433,
    [string]$Database = "SchoolManagementRDC",
    [string]$SqlUser = "sa",
    [int]$ApiPublicPort = 1804,

    [string]$JwtSecret = "ErpScolaireCloudJwtSecretKey_2026_ChangeMe_Min32Chars",

    [switch]$UpdateMobileCloudUrl,
    [switch]$ConfigureLocalSync
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publicHostClean = $PublicHost.Trim().TrimEnd('/')
$cloudApiUrl = "http://${publicHostClean}:${ApiPublicPort}"

$artifacts = Join-Path $root "artifacts"
if (-not (Test-Path $artifacts)) {
    New-Item -ItemType Directory -Path $artifacts | Out-Null
}

$coolifyEnv = @"
# Coller dans Coolify → Application API → Environment Variables (RUNTIME)
PORT=$ApiPublicPort
ASPNETCORE_URLS=http://0.0.0.0:$ApiPublicPort
ASPNETCORE_ENVIRONMENT=Production
Deployment__Role=Cloud
Deployment__ReadOnly=true
FILE_STORAGE_ROOT=/app/data/files

SQL_CONNECTION_STRING=Server=$SqlDockerHost,$SqlPort;Database=$Database;User Id=$SqlUser;Password=$SqlSaPassword;TrustServerCertificate=True;Encrypt=True

Jwt__SecretKey=$JwtSecret
Jwt__Issuer=SchoolManagementRDC
Jwt__Audience=SchoolManagementClients

Cors__AllowedOrigins__0=*
ERP_CONFIG_ENCRYPTION_KEY=ErpScolaireDockerAesKey_ChangeMe_32Chars
"@

$coolifyEnvPath = Join-Path $artifacts "coolify-api.env"
Set-Content -Path $coolifyEnvPath -Value $coolifyEnv -Encoding UTF8

Write-Host ""
Write-Host "=== Coolify — Application API ===" -ForegroundColor Cyan
Write-Host '1. Repo Git : bil-hids/adsco-monol ou votre repo lie a Coolify'
Write-Host '2. Build Pack Dockerfile, Base Directory /, Port' $ApiPublicPort
Write-Host "3. Variables runtime : $coolifyEnvPath"
Write-Host '4. Redeploy apres creation de la base SchoolManagementRDC'
Write-Host ""
Write-Host "=== Coolify — SQL Server ===" -ForegroundColor Cyan
Write-Host "Service nom interne : $SqlDockerHost (reseau Docker Coolify)"
Write-Host 'MSSQL_SA_PASSWORD = meme valeur que le mot de passe SA SQL'
Write-Host 'Exposer le port 1433 publiquement si sync depuis le PC ecole'
Write-Host ""
Write-Host "Création base (terminal conteneur SQL) :"
Write-Host "  IF DB_ID('$Database') IS NULL CREATE DATABASE [$Database];"
Write-Host ""
Write-Host "Test API après deploy : curl http://${publicHostClean}:${ApiPublicPort}/api/v1/health"
Write-Host ""

if ($ConfigureLocalSync) {
    Write-Host "Configuration sync PC ecole vers SQL cloud ($publicHostClean)..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "configure-cloud-sync.ps1") `
        -Server $publicHostClean `
        -Port $SqlPort `
        -Database $Database `
        -User $SqlUser `
        -Password $SqlSaPassword `
        -Actif 1

    $apiSource = Join-Path $root "src\SchoolManagement.API\ServeurDonneesCloud.txt"
    $apiBin = Join-Path $root "src\SchoolManagement.API\bin\Debug\net8.0\ServeurDonneesCloud.txt"
    if ((Test-Path $apiSource) -and (Test-Path (Split-Path $apiBin -Parent))) {
        Copy-Item $apiSource $apiBin -Force
    }
    Write-Host 'Redemarrez l API locale puis Parametres, Sync cloud, Synchroniser'
}

if ($UpdateMobileCloudUrl) {
    $mobileFiles = @(
        (Join-Path $root "mobile\school_management_mobile\lib\core\config\api_config.dart"),
        (Join-Path $root "mobile\school_management_mobile\run-on-phone.ps1")
    )
    foreach ($file in $mobileFiles) {
        if (-not (Test-Path $file)) { continue }
        $text = Get-Content $file -Raw -Encoding UTF8
        $text = $text -replace 'http://161\.97\.105\.22:1804', $cloudApiUrl
        $text = $text -replace 'http://169\.58\.93\.203:1804', $cloudApiUrl
        Set-Content -Path $file -Value $text -Encoding UTF8 -NoNewline
        Write-Host "Mis a jour : $file CLOUD $cloudApiUrl"
    }
}

Write-Host ""
Write-Host "URL mobile (4G) : $cloudApiUrl" -ForegroundColor Green
Write-Host "Fichier Coolify env : $coolifyEnvPath" -ForegroundColor Green
