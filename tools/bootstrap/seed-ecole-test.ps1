#Requires -Version 5.1
<#
.SYNOPSIS
  Phase 8 — Seed / upsert ECOLE TEST dans le registre Bootstrap SQL via API relay.

.DESCRIPTION
  N'utilise PAS Bootstrap__Schools__*.
  Appelle POST /registry/schools/upsert avec X-Bootstrap-Relay-Key.

  Credential : fournir -CredentialId / -SecretHash / -CredentialVersion
  (valeurs du credential actif local SchoolEstablishmentCredentials),
  OU omettre le credential pour ne mettre à jour que les URLs du registre.

.EXAMPLE
  .\seed-ecole-test.ps1 -RelayApiKey $env:BOOTSTRAP_RELAY_API_KEY `
    -CredentialId '...' -SecretHash '...' -CredentialVersion 1
#>
param(
    [string]$BootstrapBaseUrl = "https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io",
    [Parameter(Mandatory = $true)]
    [string]$RelayApiKey,
    [Guid]$SchoolId = [Guid]"71635f62-b975-479d-9e6e-fbacd05e4996",
    [string]$SchoolName = "ECOLE TEST",
    [string]$ActivationBaseUrl = "http://169.58.93.203:1804",
    [string]$CloudBaseUrl = "http://169.58.93.203:1804",
    [Guid]$ServerInstanceId = [Guid]::Empty,
    [Guid]$CredentialId = [Guid]::Empty,
    [string]$SecretHash = "",
    [int]$CredentialVersion = 1
)

$ErrorActionPreference = "Stop"
$BootstrapBaseUrl = $BootstrapBaseUrl.TrimEnd("/")

Write-Host "=== Phase 8 seed ECOLE TEST ==="
Write-Host "Bootstrap = $BootstrapBaseUrl"
Write-Host "SchoolId  = $SchoolId"

$health = Invoke-RestMethod -Uri "$BootstrapBaseUrl/health" -Method Get
Write-Host ("Health: " + ($health | ConvertTo-Json -Compress))

$body = @{
    schoolId          = $SchoolId
    schoolName        = $SchoolName
    activationBaseUrl = $ActivationBaseUrl
    cloudBaseUrl      = $CloudBaseUrl
}
if ($ServerInstanceId -ne [Guid]::Empty) {
    $body.serverInstanceId = $ServerInstanceId
}
if ($CredentialId -ne [Guid]::Empty -and -not [string]::IsNullOrWhiteSpace($SecretHash)) {
    $body.credential = @{
        credentialId      = $CredentialId
        credentialVersion = $CredentialVersion
        secretHash        = $SecretHash
        tokenType         = "school_establishment"
        createdBy         = "phase8-seed"
    }
}

$headers = @{ "X-Bootstrap-Relay-Key" = $RelayApiKey; "Content-Type" = "application/json" }
$json = $body | ConvertTo-Json -Depth 5
Write-Host "POST /registry/schools/upsert ..."
$resp = Invoke-RestMethod -Uri "$BootstrapBaseUrl/registry/schools/upsert" -Method Post -Headers $headers -Body $json
Write-Host ("Upsert OK: " + ($resp | ConvertTo-Json -Compress))

$health2 = Invoke-RestMethod -Uri "$BootstrapBaseUrl/health" -Method Get
Write-Host ("Health après upsert: " + ($health2 | ConvertTo-Json -Compress))

if ($health2.PSObject.Properties.Name -contains "ecoleTestPresent" -and -not $health2.ecoleTestPresent) {
    Write-Warning "ecoleTestPresent=false — déployer le binaire Phase 8 Bootstrap puis re-seed."
}

Write-Host "Retirer ensuite Bootstrap__Schools__0__* de Coolify et redéployer (AllowLegacy=false)."
