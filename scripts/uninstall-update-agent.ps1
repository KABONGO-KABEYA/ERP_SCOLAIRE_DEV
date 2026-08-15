<#
.SYNOPSIS
  Desinstalle le service Windows ErpScolaireUpdateAgent.
#>
[CmdletBinding()]
param(
  [switch]$RemoveAccount,
  [string]$DataRoot = "$env:ProgramData\ERP_SCOLAIRE\UpdateAgent"
)

$ErrorActionPreference = 'Stop'
$serviceName = 'ErpScolaireUpdateAgent'
$account = 'ErpScolaireUpdateAgent'
$sc = Join-Path $env:SystemRoot 'System32\sc.exe'

$svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($svc) {
  Stop-Service $serviceName -Force -ErrorAction SilentlyContinue
  & $sc delete $serviceName | Out-Null
}

if ($RemoveAccount) {
  Remove-LocalUser -Name $account -ErrorAction SilentlyContinue
}

Write-Host "Service $serviceName supprime. Dossier $DataRoot conserve (credential / state)."
