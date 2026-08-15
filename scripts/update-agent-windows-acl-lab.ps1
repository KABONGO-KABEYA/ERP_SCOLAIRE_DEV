<#
.SYNOPSIS
  ACL Windows labo pour ErpScolaireUpdateAgent (Lot 2B-5A).

.DESCRIPTION
  Defaut : -WhatIf (n'applique rien).
  Ne pas pointer vers une install de production sans revue.
  N'accorde PAS Administrators, LocalSystem, ni le share UNC eleves.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
  [string]$DataRoot = "$env:ProgramData\ERP_SCOLAIRE\UpdateAgent",
  [string]$Backups = "$env:ProgramData\ERP_SCOLAIRE\Backups",
  [string]$ApiParent = 'C:\Program Files\ERP Scolaire',
  [string]$AgentAccount = 'ErpScolaireUpdateAgent',
  [string]$SqlServiceAccount = 'NT SERVICE\MSSQL$HEROS_SQL19',
  [string]$ApiServiceName = 'ErpScolaireApi'
)

$ErrorActionPreference = 'Stop'

Write-Host "WhatIf=$WhatIfPreference  DataRoot=$DataRoot  ApiParent=$ApiParent"

function Grant-Dir {
  param([string]$Path, [string]$Account, [string]$Rights)
  if ($PSCmdlet.ShouldProcess($Path, "icacls grant $Account $Rights")) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    icacls $Path /grant:r "${Account}:(OI)(CI)${Rights}" | Out-Null
  }
}

Grant-Dir $DataRoot $AgentAccount 'M'
Grant-Dir $Backups $AgentAccount 'M'
Grant-Dir $Backups $SqlServiceAccount 'M'
Grant-Dir $ApiParent $AgentAccount 'M'

Write-Host @"
SCM (manuel, pas applique ici) : limiter Start/Stop a $ApiServiceName pour $AgentAccount.
  sc.exe sdshow $ApiServiceName
  # Ajouter ACE (RP,WP,DT,DC,LO,CR) pour le SID du compte — ne pas donner CC/LC/WO/WD sur d'autres services.
Interdit : UNC eleves, Desktop, Mobile, Bootstrap, Administrators.
"@
