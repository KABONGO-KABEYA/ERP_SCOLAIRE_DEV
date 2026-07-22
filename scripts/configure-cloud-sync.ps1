<#
.SYNOPSIS
  Configure ServeurDonneesCloud.txt pour la synchronisation locale → SQL distant.
  Le mot de passe est chiffré via DPAPI (LocalMachine) — ne jamais le committer.

.EXAMPLE
  .\scripts\configure-cloud-sync.ps1
  .\scripts\configure-cloud-sync.ps1 -Server "161.97.105.22" -User "sa" -Password "..." -Actif 1
#>
param(
    [string]$Server = "161.97.105.22",
    [int]$Port = 1433,
    [string]$Database = "SchoolManagementRDC",
    [string]$User = "sa",
    [Parameter(Mandatory = $true)]
    [string]$Password,
    [int]$Actif = 1,
    [int]$IntervalMinutes = 5,
    [string]$ApiDir = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ApiDir)) {
    $ApiDir = Join-Path $PSScriptRoot "..\src\SchoolManagement.API\bin\Debug\net8.0"
    if (-not (Test-Path $ApiDir)) {
        $ApiDir = Join-Path $PSScriptRoot "..\src\SchoolManagement.API"
    }
}

$ApiDir = (Resolve-Path $ApiDir).Path
Write-Host "Répertoire cible : $ApiDir"

# Build Application pour disposer du chiffrement DPAPI
$appProj = Join-Path $PSScriptRoot "..\src\SchoolManagement.Application\SchoolManagement.Application.csproj"
dotnet build $appProj -c Debug --nologo | Out-Null

$dll = Join-Path $PSScriptRoot "..\src\SchoolManagement.Application\bin\Debug\net8.0\SchoolManagement.Application.dll"
$dll = (Resolve-Path $dll).Path

Add-Type -AssemblyName System.Security
# Utilise un petit programme C# inline pour écrire le fichier via EncryptionService
$tempCs = Join-Path $env:TEMP "erp-cloud-sync-config.cs"
$tempExe = Join-Path $env:TEMP "erp-cloud-sync-config.exe"

@"
using System;
using System.IO;
using System.Reflection;

class Program {
  static int Main(string[] args) {
    var appDir = args[0];
    var server = args[1];
    var port = int.Parse(args[2]);
    var database = args[3];
    var user = args[4];
    var password = args[5];
    var actif = args[6] == "1";
    var interval = int.Parse(args[7]);
    var dllPath = args[8];

    var asm = Assembly.LoadFrom(dllPath);
    var encType = asm.GetType("SchoolManagement.Application.Configuration.Encryption.EncryptionService")!;
    var mgrType = asm.GetType("SchoolManagement.Application.Configuration.Database.CloudDatabaseConfigurationManager")!;
    var cfgType = asm.GetType("SchoolManagement.Application.Configuration.Database.CloudDatabaseConfiguration")!;

    var enc = Activator.CreateInstance(encType)!;
    var mgr = Activator.CreateInstance(mgrType, appDir, enc)!;
    var cfg = Activator.CreateInstance(cfgType)!;
    cfgType.GetProperty("Serveur")!.SetValue(cfg, server);
    cfgType.GetProperty("Port")!.SetValue(cfg, port);
    cfgType.GetProperty("Base")!.SetValue(cfg, database);
    cfgType.GetProperty("Utilisateur")!.SetValue(cfg, user);
    cfgType.GetProperty("Actif")!.SetValue(cfg, actif);
    cfgType.GetProperty("IntervalleMinutes")!.SetValue(cfg, interval);
    var authType = asm.GetType("SchoolManagement.Application.Configuration.Database.DatabaseAuthenticationMode")!;
    cfgType.GetProperty("Authentification")!.SetValue(cfg, Enum.Parse(authType, "SqlServer"));

    mgrType.GetMethod("SaveConfiguration")!.Invoke(mgr, new object[] { cfg, password });
    Console.WriteLine("OK: " + Path.Combine(appDir, "ServeurDonneesCloud.txt"));
    return 0;
  }
}
"@ | Set-Content -Path $tempCs -Encoding UTF8

# Compile with references
$csc = Join-Path ${env:WINDIR} "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    # Prefer dotnet script approach via built helper
    Write-Host "Compilation via dotnet..."
}

# Simpler: write file directly using PowerShell DPAPI matching EncryptionService
Add-Type -AssemblyName System.Security
$entropy = [Text.Encoding]::UTF8.GetBytes("SchoolManagement.ERP.Scolaire.RDC.v1")
$plain = [Text.Encoding]::UTF8.GetBytes($Password)
$protected = [System.Security.Cryptography.ProtectedData]::Protect(
    $plain, $entropy, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
$encPassword = "ENC:" + [Convert]::ToBase64String($protected)

$content = @"
#######################################################
# ERP SCOLAIRE RDC
# Configuration SQL Server DISTANT (cloud)
# Sync automatique locale → cloud dès qu'Internet est dispo
# Ne jamais committer ce fichier (mot de passe chiffré machine)
#######################################################

ACTIF=$Actif
INTERVALLE_MINUTES=$IntervalMinutes
SERVEUR=$Server
PORT=$Port
BASE=$Database
AUTHENTIFICATION=SQL
UTILISATEUR=$User
MOTDEPASSE=$encPassword
"@

$outFile = Join-Path $ApiDir "ServeurDonneesCloud.txt"
Set-Content -Path $outFile -Value $content -Encoding UTF8
Write-Host "Fichier écrit : $outFile"
Write-Host "ACTIF=$Actif SERVEUR=$Server BASE=$Database INTERVALLE=$IntervalMinutes min"
Write-Host "Redémarrez l'API pour activer la synchronisation."
