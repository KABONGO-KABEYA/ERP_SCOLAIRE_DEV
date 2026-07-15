param(
    [string]$ApiBaseUrl = "http://localhost:5041",
    [string]$Username = "admin",
    [string]$Password = "Admin@2026"
)

$ErrorActionPreference = "Stop"

Write-Host "Connexion à l'API..." -ForegroundColor Cyan
$loginBody = @{ username = $Username; password = $Password } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/v1/auth/login" -Method POST -Body $loginBody -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($login.data.accessToken)" }

Write-Host "Réinitialisation des inscriptions, élèves et fichiers..." -ForegroundColor Yellow
$result = Invoke-RestMethod -Uri "$ApiBaseUrl/api/v1/admin/reset-enrollment-data" -Method POST -Headers $headers

Write-Host ""
Write-Host "Terminé :" -ForegroundColor Green
Write-Host "  Élèves supprimés      : $($result.data.studentsRemoved)"
Write-Host "  Inscriptions suppr.   : $($result.data.enrollmentsRemoved)"
Write-Host "  Responsables suppr.   : $($result.data.guardiansRemoved)"
Write-Host "  Entrées fichiers suppr: $($result.data.filesRemoved)"
Write-Host "  Locaux corrigés       : $($result.data.classRoomsRepaired)"
Write-Host ""
Write-Host $result.data.message -ForegroundColor Green
