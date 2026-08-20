<#
.SYNOPSIS
  Ajoute la configuration Bootstrap manquante sur une API locale déjà installée (mode Production).

.NOTES
  Nécessite une session PowerShell élevée (Administrateur).
  Ne modifie pas les secrets JWT/SQL existants : ajoute uniquement Bootstrap + Activation.
#>
#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$ApiDir = 'C:\Program Files\ERP Scolaire\Api',
    [string]$ServiceName = 'ErpScolaireApi',
    [string]$RegistryBaseUrl = 'https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io',
    [string]$CloudBaseUrl = 'http://169.58.93.203:1804',
    [string]$RelayApiKey = 'HdZxzs46bH7GimmUDSMJM-xogmyzGUNm0mscWT-ar2Heu6m11qBzcyvpXnLcM6QR',
    [int]$ApiPort = 5096
)

$ErrorActionPreference = 'Stop'

function Get-PreferredLanIPv4 {
    Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object {
            $_.IPAddress -notlike '127.*' -and
            $_.IPAddress -notlike '169.254.*' -and
            $_.InterfaceAlias -notlike 'vEthernet*' -and
            $_.InterfaceAlias -ne 'Ethernet 2'
        } |
        Sort-Object -Property InterfaceMetric |
        Select-Object -ExpandProperty IPAddress -First 1
}

function Merge-JsonSection {
    param(
        [Parameter(Mandatory = $true)][psobject]$Root,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][hashtable]$Values
    )

    $section = @{}
    if ($Root.PSObject.Properties.Name -contains $Name -and $null -ne $Root.$Name) {
        foreach ($prop in $Root.$Name.PSObject.Properties) {
            $section[$prop.Name] = $prop.Value
        }
    }

    foreach ($key in $Values.Keys) {
        $section[$key] = $Values[$key]
    }

    $Root | Add-Member -NotePropertyName $Name -NotePropertyValue $section -Force
}

$appsettingsPath = Join-Path $ApiDir 'appsettings.Production.json'
if (-not (Test-Path $appsettingsPath)) {
    throw "Fichier introuvable : $appsettingsPath"
}

$lanIp = Get-PreferredLanIPv4
if ([string]::IsNullOrWhiteSpace($lanIp)) {
    $activationBaseUrl = "http://127.0.0.1:$ApiPort"
}
else {
    $activationBaseUrl = "http://${lanIp}:$ApiPort"
}

Write-Host "==> Patch Bootstrap Production" -ForegroundColor Cyan
Write-Host "    ActivationBaseUrl : $activationBaseUrl"
Write-Host "    CloudBaseUrl      : $CloudBaseUrl"
Write-Host "    RegistryBaseUrl   : $RegistryBaseUrl"

$config = Get-Content -Raw -Path $appsettingsPath | ConvertFrom-Json
Merge-JsonSection -Root $config -Name 'Activation' -Values @{
    BootstrapRelayKey = $RelayApiKey
    CloudBaseUrl      = $CloudBaseUrl
}
Merge-JsonSection -Root $config -Name 'Bootstrap' -Values @{
    RegistryBaseUrl   = $RegistryBaseUrl
    RelayApiKey       = $RelayApiKey
    ActivationBaseUrl = $activationBaseUrl
    CloudBaseUrl      = $CloudBaseUrl
}

$config | ConvertTo-Json -Depth 8 | Set-Content -Path $appsettingsPath -Encoding UTF8

Write-Host "==> Redémarrage service $ServiceName..." -ForegroundColor Cyan
Restart-Service -Name $ServiceName
Start-Sleep -Seconds 5

$health = Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/api/v1/health" -TimeoutSec 15
Write-Host "API health : $($health.data.status)" -ForegroundColor Green
Write-Host ""
Write-Host "Prochaine étape : Desktop > QR établissement > 'Réessayer la sync Bootstrap'" -ForegroundColor Yellow
