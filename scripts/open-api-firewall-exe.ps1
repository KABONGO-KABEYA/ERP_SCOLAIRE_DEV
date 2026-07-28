$exe = "D:\Mes Projet\ERP_Administration_Scolaire_2026\src\SchoolManagement.API\bin\Debug\net8.0\SchoolManagement.API.exe"
netsh advfirewall firewall delete rule name="ERP API Dotnet" | Out-Null
netsh advfirewall firewall add rule name="ERP API Dotnet" dir=in action=allow program="$exe" profile=any enable=yes
Write-Host DONE
