# Requires Administrator — allows inbound TCP 5041 + SchoolManagement.API.exe
$ErrorActionPreference = 'Stop'
$port = 5041
$exe = 'D:\Mes Projet\ERP_Administration_Scolaire_2026\src\SchoolManagement.API\bin\Debug\net8.0\SchoolManagement.API.exe'

foreach ($name in @('ERP API 5041', 'ERP API Dotnet')) {
    netsh advfirewall firewall delete rule name="$name" | Out-Null
}

netsh advfirewall firewall add rule name="ERP API 5041" dir=in action=allow protocol=TCP localport=$port profile=any enable=yes | Out-Null

if (Test-Path $exe) {
    netsh advfirewall firewall add rule name="ERP API Dotnet" dir=in action=allow program="$exe" profile=any enable=yes | Out-Null
}

Write-Host "OK: firewall inbound allow TCP $port + API exe"
netsh advfirewall firewall show rule name="ERP API 5041"
netsh advfirewall firewall show rule name="ERP API Dotnet"
