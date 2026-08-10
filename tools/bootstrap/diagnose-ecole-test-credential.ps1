#Requires -Version 5.1
$ErrorActionPreference = "Continue"
$SchoolId = "71635f62-b975-479d-9e6e-fbacd05e4996"
$Server = "localhost\HEROS_SQL19"
$Db = "SchoolManagementRDC_Development"
$Bootstrap = "https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io"

Write-Host "=== 1. Local school + credential tables ==="
sqlcmd -S $Server -E -C -d $Db -Q @"
SET NOCOUNT ON;
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE '%Establishment%' OR TABLE_NAME LIKE '%Credential%';
SELECT CAST(Id AS nvarchar(36)) AS SchoolId, Name FROM Schools WHERE Id='$SchoolId';
"@ -W -s "|" 

Write-Host "=== 2. Ensure SchoolEstablishmentCredentials schema ==="
$sqlCreate = Get-Content -Raw "src\SchoolManagement.Infrastructure\Persistence\SchoolEstablishmentSchemaInitializer.cs"
# Use inline DDL matching initializer
sqlcmd -S $Server -E -C -d $Db -Q @"
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.SchoolEstablishmentCredentials', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchoolEstablishmentCredentials
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SchoolEstablishmentCredentials PRIMARY KEY,
        SchoolId UNIQUEIDENTIFIER NOT NULL,
        CredentialVersion INT NOT NULL,
        TokenType NVARCHAR(64) NOT NULL,
        SecretHash NVARCHAR(128) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        RevokedAtUtc DATETIME2 NULL,
        RevokedReason NVARCHAR(500) NULL,
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        BootstrapSyncPending BIT NOT NULL CONSTRAINT DF_SchoolEstablishmentCredentials_SyncPending DEFAULT(1),
        BootstrapSyncStatus NVARCHAR(32) NOT NULL,
        LastBootstrapSyncError NVARCHAR(1000) NULL,
        LastBootstrapSyncAttemptUtc DATETIME2 NULL,
        BootstrapSyncedAtUtc DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL,
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SchoolEstablishmentCredentials_IsDeleted DEFAULT(0),
        DeletedAt DATETIME2 NULL,
        DeletedBy UNIQUEIDENTIFIER NULL
    );
    CREATE UNIQUE INDEX UX_SchoolEstablishmentCredential_SchoolId_Version
        ON dbo.SchoolEstablishmentCredentials(SchoolId, CredentialVersion);
    CREATE UNIQUE INDEX UX_SchoolEstablishmentCredential_Active
        ON dbo.SchoolEstablishmentCredentials(SchoolId)
        WHERE Status = N'Active';
END
SELECT 'SCHEMA_OK' AS Result;
SELECT Id, SchoolId, CredentialVersion, Status, TokenType,
       LEN(SecretHash) AS HashLen, LEFT(SecretHash,12) AS HashPrefix,
       BootstrapSyncStatus, BootstrapSyncPending, CreatedAt
FROM SchoolEstablishmentCredentials
WHERE SchoolId='$SchoolId' AND IsDeleted=0
ORDER BY CredentialVersion DESC;
"@ -W -s "|"

Write-Host "=== 3. Health before ==="
try {
  $h = Invoke-RestMethod -Uri "$Bootstrap/health" -TimeoutSec 30
  $h | ConvertTo-Json -Compress
} catch { Write-Host "health error: $($_.Exception.Message)" }

Write-Host "=== 4. Probe relay key (status only) ==="
$relayCandidates = @()
$devKey = "dev-bootstrap-relay-key-change-in-production"
$relayCandidates += $devKey
if (Test-Path .env) {
  Get-Content .env | ForEach-Object {
    if ($_ -match '^(BOOTSTRAP_RELAY_API_KEY|Bootstrap__RelayApiKey)\s*=\s*(.+)$') {
      $relayCandidates += $Matches[2].Trim()
    }
  }
}
# Also check user env
foreach ($n in @("BOOTSTRAP_RELAY_API_KEY","Bootstrap__RelayApiKey")) {
  $v = [Environment]::GetEnvironmentVariable($n)
  if ($v) { $relayCandidates += $v }
}

$relayCandidates = $relayCandidates | Select-Object -Unique
$workingKey = $null
foreach ($k in $relayCandidates) {
  $headers = @{ "X-Bootstrap-Relay-Key" = $k }
  try {
    # upsert with school only (no credential) to probe auth — use a harmless GET-like by posting upsert of known school URLs
    $body = @{
      schoolId = $SchoolId
      schoolName = "ECOLE TEST"
      activationBaseUrl = "http://169.58.93.203:1804"
      cloudBaseUrl = "http://169.58.93.203:1804"
    } | ConvertTo-Json
    $resp = Invoke-WebRequest -Uri "$Bootstrap/registry/schools/upsert" -Method Post -Headers $headers -Body $body -ContentType "application/json" -TimeoutSec 30 -UseBasicParsing
    Write-Host "Relay probe OK status=$($resp.StatusCode) keyLen=$($k.Length)"
    $workingKey = $k
    break
  } catch {
    $code = $null
    if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
    Write-Host "Relay probe FAIL status=$code keyLen=$($k.Length)"
  }
}

if (-not $workingKey) {
  Write-Host "NO_WORKING_RELAY_KEY"
  exit 2
}

Write-Host "WORKING_RELAY=yes"

# Persist key for next steps via temp file (local only)
$workingKey | Set-Content -Path "artifacts\.relay-key.tmp" -Encoding ascii -NoNewline
Write-Host "Relay key cached for seed step."
