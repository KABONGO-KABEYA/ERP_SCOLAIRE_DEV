@echo off
netsh advfirewall firewall delete rule name="ERP API Dotnet" >nul 2>&1
netsh advfirewall firewall add rule name="ERP API Dotnet" dir=in action=allow program="D:\Mes Projet\ERP_Administration_Scolaire_2026\src\SchoolManagement.API\bin\Debug\net8.0\SchoolManagement.API.exe" profile=any enable=yes
echo DONE
