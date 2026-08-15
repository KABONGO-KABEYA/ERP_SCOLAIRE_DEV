<#
.SYNOPSIS
  Installe le service Windows ErpScolaireUpdateAgent (Lot 2B-3).

.DESCRIPTION
  Compte local ErpScolaireUpdateAgent (pas LocalSystem, pas Administrators).
  Droits : lire le credential, ecrire state/staging/logs, HTTPS sortant vers Bootstrap.
  Aucun droit SQL. Aucune ACL de modification sur le dossier API.

  Exiger une invite Administrateur. Ne demarre pas le service (provision DPAPI d'abord).
#>
[CmdletBinding()]
param(
  [string]$BinPath = '',
  [string]$Password = '',
  [string]$DataRoot = "$env:ProgramData\ERP_SCOLAIRE\UpdateAgent"
)

$ErrorActionPreference = 'Stop'
$serviceName = 'ErpScolaireUpdateAgent'
$account = 'ErpScolaireUpdateAgent'
$display = 'ERP Scolaire Update Agent'
$sc = Join-Path $env:SystemRoot 'System32\sc.exe'

if (-not $BinPath) {
  $root = Resolve-Path (Join-Path $PSScriptRoot '..')
  $candidate = Join-Path $root 'src\SchoolManagement.UpdateAgent\bin\Release\net8.0-windows\SchoolManagement.UpdateAgent.exe'
  if (Test-Path $candidate) { $BinPath = $candidate }
  else { throw 'Specifiez -BinPath vers SchoolManagement.UpdateAgent.exe' }
}

$BinPath = (Resolve-Path $BinPath).Path
if (-not $Password) {
  $Password = -join ((48..57 + 65..90 + 97..122) | Get-Random -Count 28 | ForEach-Object { [char]$_ })
}

Write-Host "Compte $account (hors Administrators, hors LocalSystem)..." -ForegroundColor Cyan
$existing = Get-LocalUser -Name $account -ErrorAction SilentlyContinue
$secure = ConvertTo-SecureString $Password -AsPlainText -Force
if (-not $existing) {
  New-LocalUser -Name $account -Password $secure -PasswordNeverExpires -UserMayNotChangePassword -AccountNeverExpires |
    Out-Null
} else {
  Set-LocalUser -Name $account -Password $secure
}

try { Add-LocalGroupMember -Group 'Users' -Member $account -ErrorAction SilentlyContinue } catch {}
try { Remove-LocalGroupMember -Group 'Administrators' -Member $account -ErrorAction SilentlyContinue } catch {}

function Grant-LogOnAsService {
  param([string]$AccountName)
  $sid = ([System.Security.Principal.NTAccount]$AccountName).Translate([System.Security.Principal.SecurityIdentifier]).Value
  $inf = Join-Path $env:TEMP ("ua_logon_" + [guid]::NewGuid().ToString('N') + '.inf')
  $db = Join-Path $env:TEMP ("ua_logon_" + [guid]::NewGuid().ToString('N') + '.sdb')
  secedit /export /cfg $inf | Out-Null
  $lines = Get-Content $inf
  $found = $false
  $updated = foreach ($line in $lines) {
    if ($line -like 'SeServiceLogonRight*') {
      $found = $true
      if ($line -notlike "*$sid*") { "$line,*$sid" } else { $line }
    } else { $line }
  }
  if (-not $found) {
    $updated = @($updated) + "SeServiceLogonRight = *$sid"
  }
  Set-Content -Path $inf -Value $updated
  secedit /configure /db $db /cfg $inf /areas USER_RIGHTS | Out-Null
}

Grant-LogOnAsService -AccountName $account

New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null
$backups = Join-Path (Split-Path $DataRoot -Parent) 'Backups'
New-Item -ItemType Directory -Force -Path $backups | Out-Null
icacls $DataRoot /inheritance:r | Out-Null
icacls $DataRoot /grant:r "NT AUTHORITY\SYSTEM:(OI)(CI)F" | Out-Null
icacls $DataRoot /grant:r "${account}:(OI)(CI)M" | Out-Null
icacls $backups /inheritance:r | Out-Null
icacls $backups /grant:r "NT AUTHORITY\SYSTEM:(OI)(CI)F" | Out-Null
icacls $backups /grant:r "${account}:(OI)(CI)M" | Out-Null
# Le service SQL devra pouvoir ecrire dans Backups (lot permissions SQL). Aucun GRANT SQL ici.

$svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($svc) {
  Stop-Service $serviceName -Force -ErrorAction SilentlyContinue
  & $sc delete $serviceName | Out-Null
  Start-Sleep -Seconds 2
}

$createCmd = "create $serviceName binPath= `"$BinPath`" start= auto DisplayName= `"$display`" obj= `".\$account`" password= `"$Password`""
cmd.exe /c "`"$sc`" $createCmd"
if ($LASTEXITCODE -ne 0) { throw "sc create echoue ($LASTEXITCODE)" }

& $sc description $serviceName "Check Bootstrap, telechargement verifie et deploiement serveur (AutoDeploy=false par defaut)."
& $sc failure $serviceName reset= 86400 actions= restart/15000/restart/15000/restart/15000 | Out-Null

Write-Host "Service $serviceName cree (arrete). Provisionnez le credential EN TANT QUE $account :" -ForegroundColor Green
Write-Host "  $BinPath provision --client-id <guid> --client-secret <secret> --school-id <guid> --data-root `"$DataRoot`""
Write-Host "Aucun droit SQL ni modification API n'a ete accorde. Le mot de passe du compte n'est pas reaffiche."
