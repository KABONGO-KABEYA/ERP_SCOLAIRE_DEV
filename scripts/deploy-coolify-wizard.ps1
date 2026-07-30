<#
.SYNOPSIS
  Assistant pas à pas Coolify (SQL MSSQL_DEV + API ERP). Aucun secret n'est affiché à l'écran.

.EXAMPLE
  cd "d:\Mes Projet\ERP_Administration_Scolaire_2026"
  .\scripts\deploy-coolify-wizard.ps1
#>
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$hostIp = "169.58.93.203"
$apiPort = 1804
$sqlPort = 1433
$db = "SchoolManagementRDC"

function Write-Step($n, $title) {
    Write-Host ""
    Write-Host "========== ÉTAPE $n : $title ==========" -ForegroundColor Cyan
}

Clear-Host
Write-Host "Assistant déploiement Coolify — ERP Scolaire" -ForegroundColor Green
Write-Host "VPS : $hostIp  |  API : $apiPort  |  SQL : $sqlPort"
Write-Host ""
Write-Host "Vous allez : (1) définir le mot de passe SQL, (2) coller les variables API dans Coolify, (3) tester."
Write-Host "Gardez Coolify ouvert dans le navigateur."
Read-Host "Appuyez sur Entrée quand Coolify est ouvert"

Write-Step 1 "Mot de passe SQL (MSSQL_DEV)"
Write-Host @"
Dans Coolify :
  • Ouvrir la stack **MSSQL_DEV** → menu **Environment Variables**
  • Ajouter ou modifier :
      MSSQL_SA_PASSWORD = (mot de passe fort : 8+ caractères, majuscule, minuscule, chiffre, symbole)
      ACCEPT_EULA = Y   (souvent déjà dans le compose)
  • **Save** puis **Restart** le service Sqlserver

Si MSSQL_SA_PASSWORD est vide dans le compose, Coolify doit le remplir via ces variables.
"@

$secure = Read-Host "Tapez le MÊME mot de passe SA ici (invisible)" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $saPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}
if ([string]::IsNullOrWhiteSpace($saPassword)) {
    throw "Mot de passe vide. Relancez le script."
}

Write-Step 2 "Créer la base SchoolManagementRDC"
Write-Host "Coolify → MSSQL_DEV → Sqlserver → **Terminal** (ou Console)."
Write-Host "Collez cette commande (mot de passe déjà inclus, ne la partagez pas) :"
Write-Host ""

$sqlCmd = "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '$($saPassword.Replace("'", "''"))' -C -Q `"IF DB_ID('$db') IS NULL CREATE DATABASE [$db];`""
$cmdPath = Join-Path $root "artifacts\coolify-create-database.sh"
$artifacts = Split-Path $cmdPath -Parent
if (-not (Test-Path $artifacts)) { New-Item -ItemType Directory -Path $artifacts | Out-Null }
Set-Content -Path $cmdPath -Value $sqlCmd -Encoding UTF8
Write-Host "Commande enregistrée dans : $cmdPath" -ForegroundColor Yellow
Write-Host "(Ouvrez le fichier, copiez-collez dans le terminal SQL Coolify.)"
Read-Host "Appuyez sur Entrée après avoir exécuté la commande (ou si la base existe déjà)"

Write-Step 3 "Variables de l'application API (Git → Dockerfile)"
Write-Host @"
Coolify → votre application **API** (repo ERP_Administration_Scolaire_2026) :
  • **General** : Dockerfile, port exposé **1804**
  • **Environment Variables** → mode **Runtime** (pas seulement Build)
  • Supprimez les anciennes variables SQL en conflit si besoin
  • Collez TOUT le bloc du fichier ci-dessous → Save → **Redeploy**
"@

$jwt = "ErpScolaireCloudJwtSecretKey_2026_ChangeMe_Min32Chars"
$envBlock = @"
PORT=$apiPort
ASPNETCORE_URLS=http://0.0.0.0:$apiPort
ASPNETCORE_ENVIRONMENT=Production
Deployment__Role=Cloud
Deployment__ReadOnly=true
FILE_STORAGE_ROOT=/app/data/files

SQL_CONNECTION_STRING=Server=$hostIp,$sqlPort;Database=$db;User Id=sa;Password=$saPassword;TrustServerCertificate=True;Encrypt=True

Jwt__SecretKey=$jwt
Jwt__Issuer=SchoolManagementRDC
Jwt__Audience=SchoolManagementClients
Cors__AllowedOrigins__0=*
ERP_CONFIG_ENCRYPTION_KEY=ErpScolaireDockerAesKey_ChangeMe_32Chars
"@

$envPath = Join-Path $artifacts "coolify-api.env"
Set-Content -Path $envPath -Value $envBlock -Encoding UTF8
Write-Host "Fichier prêt : $envPath" -ForegroundColor Green
Start-Process notepad.exe $envPath
Read-Host "Collez le contenu dans Coolify, Redeploy l'API, puis Entrée ici"

Write-Step 4 "Test depuis votre PC"
Write-Host "Attente 30 s (démarrage conteneur)..."
Start-Sleep -Seconds 30

$healthUrl = "http://${hostIp}:${apiPort}/api/v1/health"
try {
    $r = Invoke-WebRequest -Uri $healthUrl -TimeoutSec 15 -UseBasicParsing
    Write-Host "SUCCÈS API : $($r.StatusCode) — $healthUrl" -ForegroundColor Green
    Write-Host $r.Content
} catch {
    Write-Host "L'API ne répond pas encore : $($_.Exception.Message)" -ForegroundColor Red
    Write-Host @"

Vérifications Coolify :
  1. Deploy API = **Finished** (pas Failed)
  2. Logs API : erreur « SQL » ou « Jwt » ?
  3. Port public **1804** mappé sur l'application
  4. Firewall VPS : port $apiPort ouvert

SQL depuis ici (1433) :
"@
    $tcp = Test-NetConnection -ComputerName $hostIp -Port $sqlPort -WarningAction SilentlyContinue
    if ($tcp.TcpTestSucceeded) {
        Write-Host "  SQL port $sqlPort : OK depuis votre PC" -ForegroundColor Green
    } else {
        Write-Host "  SQL port $sqlPort : inaccessible" -ForegroundColor Red
    }
}

Write-Step 5 "Sync PC école (optionnel, après API OK)"
$sync = Read-Host "Configurer la sync cloud sur ce PC maintenant ? (o/N)"
if ($sync -eq "o" -or $sync -eq "O") {
    & (Join-Path $PSScriptRoot "configure-cloud-sync.ps1") `
        -Server $hostIp -Port $sqlPort -Database $db -User "sa" -Password $saPassword -Actif 1
    Write-Host "Redémarrez l'API locale + Desktop → Sync cloud → Synchroniser." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Mobile 4G : http://${hostIp}:${apiPort}" -ForegroundColor Green
Write-Host "Fichiers sensibles dans artifacts\ (ne pas committer)." -ForegroundColor DarkGray
