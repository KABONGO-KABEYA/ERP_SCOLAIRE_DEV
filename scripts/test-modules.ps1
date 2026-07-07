param(
    [string]$BaseUrl = "http://localhost:5041",
    [string]$UserName = "admin",
    [string]$Password = "Admin@2026"
)

$ErrorActionPreference = "Stop"
Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCerts : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint s, X509Certificate c, WebRequest r, int p) { return true; }
}
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCerts

function Invoke-Api {
    param([string]$Method, [string]$Path, [hashtable]$Headers = @{}, [object]$Body = $null)
    $uri = "$BaseUrl$Path"
    $params = @{ Uri = $uri; Method = $Method; Headers = $Headers; ContentType = "application/json" }
    if ($Body) { $params.Body = ($Body | ConvertTo-Json -Depth 6) }
    return Invoke-RestMethod @params
}

$login = Invoke-Api -Method POST -Path "/api/v1/auth/login" -Body @{ userName = $UserName; password = $Password }
$token = $login.data.accessToken
$h = @{ Authorization = "Bearer $token" }

$modules = @(
    @{ Name = "Tableau de bord"; Path = "/api/v1/health" },
    @{ Name = "Paramétrage - école"; Path = "/api/v1/schools/current" },
    @{ Name = "Paramétrage - années"; Path = "/api/v1/schools/current/academic-years" },
    @{ Name = "Élèves"; Path = "/api/v1/students?page=1&pageSize=50" },
    @{ Name = "Académique - sections"; Path = "/api/v1/academic/sections" },
    @{ Name = "Académique - classes"; Path = "/api/v1/academic/classrooms" },
    @{ Name = "Académique - cours"; Path = "/api/v1/academic/courses" },
    @{ Name = "Académique - inscriptions"; Path = "/api/v1/academic/enrollments" },
    @{ Name = "Notes - lookups"; Path = "/api/v1/schools/current/lookups" },
    @{ Name = "Financier"; Path = "/api/v1/payments?page=1&pageSize=50" },
    @{ Name = "Documents"; Path = "/api/v1/documents" },
    @{ Name = "Statistiques - dashboard"; Path = "/api/v1/reports/dashboard" },
    @{ Name = "Statistiques - effectifs"; Path = "/api/v1/reports/enrollment-by-class" },
    @{ Name = "Statistiques - moyennes"; Path = "/api/v1/reports/class-averages" },
    @{ Name = "Statistiques - finances"; Path = "/api/v1/reports/financial-summary" },
    @{ Name = "Administration - users"; Path = "/api/v1/admin/users" },
    @{ Name = "Administration - roles"; Path = "/api/v1/admin/roles" }
)

$results = @()
foreach ($m in $modules) {
    try {
        $r = Invoke-Api -Method GET -Path $m.Path -Headers $h
        $ok = $r.success -eq $true
        $results += [pscustomobject]@{ Module = $m.Name; Status = if ($ok) { "OK" } else { "ERREUR" }; Detail = $r.message }
        Write-Host ("[{0}] {1}" -f ($(if ($ok) { "OK" } else { "KO" })), $m.Name)
    }
    catch {
        $results += [pscustomobject]@{ Module = $m.Name; Status = "ERREUR"; Detail = $_.Exception.Message }
        Write-Host "[KO] $($m.Name) - $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "=== Résumé ==="
$results | Format-Table -AutoSize
if ($results | Where-Object Status -ne "OK") { exit 1 }
