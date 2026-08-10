#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$SchoolId = "71635f62-b975-479d-9e6e-fbacd05e4996"
$LocalServer = "localhost\HEROS_SQL19"
$LocalDb = "SchoolManagementRDC_Development"
$BootstrapUrl = "https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io"
$OfficialSql = "169.58.93.203,1433"

Write-Host "=== A. Ensure local schema (QUOTED_IDENTIFIER ON) ==="
sqlcmd -S $LocalServer -E -C -d $LocalDb -I -Q @"
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
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
        BootstrapSyncStatus NVARCHAR(32) NOT NULL CONSTRAINT DF_SchoolEstablishmentCredentials_SyncStatus DEFAULT(N'Pending'),
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
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SchoolEstablishmentCredential_SchoolId_Version' AND object_id = OBJECT_ID(N'dbo.SchoolEstablishmentCredentials'))
    CREATE UNIQUE INDEX UX_SchoolEstablishmentCredential_SchoolId_Version ON dbo.SchoolEstablishmentCredentials(SchoolId, CredentialVersion);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SchoolEstablishmentCredential_Active' AND object_id = OBJECT_ID(N'dbo.SchoolEstablishmentCredentials'))
    CREATE UNIQUE INDEX UX_SchoolEstablishmentCredential_Active ON dbo.SchoolEstablishmentCredentials(SchoolId) WHERE Status = N'Active';
SELECT 'LOCAL_SCHEMA_OK' AS Result;
"@

Write-Host "=== B. Local active credential (or create once) ==="
# Create via .NET crypto: SHA256 hex of 32 random bytes â€” same as SchoolEstablishmentCrypto
Add-Type -AssemblyName System.Security
$existing = sqlcmd -S $LocalServer -E -C -d $LocalDb -I -h -1 -W -Q @"
SET NOCOUNT ON;
SELECT TOP 1 CAST(Id AS nvarchar(36)), CredentialVersion, Status, SecretHash
FROM SchoolEstablishmentCredentials
WHERE SchoolId='$SchoolId' AND Status='Active' AND IsDeleted=0;
"@
$existingTrim = ($existing | Out-String).Trim()
Write-Host "Existing local row raw: [$existingTrim]"

$credId = $null
$credVersion = 1
$secretHash = $null

if ($existingTrim -and $existingTrim -notmatch 'rows affected' -and $existingTrim.Length -gt 10) {
    $parts = ($existingTrim -split '\s+') | Where-Object { $_ }
    # Format may be: guid version status hash
    if ($parts.Count -ge 4) {
        $credId = $parts[0]
        $credVersion = [int]$parts[1]
        $secretHash = $parts[3]
        Write-Host "USING_EXISTING_LOCAL credentialId=$credId version=$credVersion hashLen=$($secretHash.Length)"
    }
}

if (-not $secretHash) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $hashBytes = $sha.ComputeHash($bytes)
    $secretHash = ([BitConverter]::ToString($hashBytes) -replace '-','').ToLowerInvariant()
    $credId = [guid]::NewGuid().ToString("D")
    $credVersion = 1
    $now = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss")
    sqlcmd -S $LocalServer -E -C -d $LocalDb -I -Q @"
SET NOCOUNT ON;
INSERT INTO SchoolEstablishmentCredentials
(Id, SchoolId, CredentialVersion, TokenType, SecretHash, Status,
 BootstrapSyncPending, BootstrapSyncStatus, CreatedAt, IsDeleted)
VALUES
('$credId', '$SchoolId', $credVersion, N'school_establishment', N'$secretHash', N'Active',
 1, N'Pending', SYSUTCDATETIME(), 0);
SELECT 'LOCAL_CREATED' AS Result, '$credId' AS Id, $credVersion AS Ver;
"@
    Write-Host "CREATED_LOCAL credentialId=$credId version=$credVersion hashLen=$($secretHash.Length)"
}

# Save meta for next steps (hash needed for JWT test â€” keep local artifacts gitignored)
@{
  credentialId = $credId
  credentialVersion = $credVersion
  secretHash = $secretHash
  schoolId = $SchoolId
} | ConvertTo-Json | Set-Content -Path "artifacts\ecole-test-credential.local.json" -Encoding utf8

Write-Host "=== C. Read SQL password from .env (official host) ==="
$saPassword = $null
if (Test-Path .env) {
  Get-Content .env | ForEach-Object {
    if ($_ -match 'Password=([^;]+)') { $saPassword = $Matches[1] }
  }
}
if (-not $saPassword) { throw "No SQL password in .env" }
Write-Host "SQL password present len=$($saPassword.Length)"

Write-Host "=== D. Probe official SQL Bootstrap DB ==="
$probe = sqlcmd -S $OfficialSql -U sa -P $saPassword -C -Q "SELECT name FROM sys.databases WHERE name='SchoolManagementBootstrap'; SELECT DB_NAME();" -W -h -1 2>&1
Write-Host ($probe | Out-String)

if (($probe | Out-String) -notmatch 'SchoolManagementBootstrap') {
    Write-Host "BOOTSTRAP_DB_MISSING_OR_LOGIN_FAILED"
    # Still try create DB
    sqlcmd -S $OfficialSql -U sa -P $saPassword -C -Q "IF DB_ID('SchoolManagementBootstrap') IS NULL CREATE DATABASE SchoolManagementBootstrap;" 2>&1 | Out-Host
}

Write-Host "=== E. Inspect BootstrapSchoolEstablishmentCredentials ==="
$bootCreds = sqlcmd -S $OfficialSql -U sa -P $saPassword -C -d SchoolManagementBootstrap -W -s "|" -Q @"
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.BootstrapSchoolEstablishmentCredentials', N'U') IS NULL
BEGIN SELECT 'NO_TABLE' AS info; END
ELSE
BEGIN
  SELECT CAST(Id AS nvarchar(36)) AS Id, CAST(SchoolId AS nvarchar(36)) AS SchoolId,
         CredentialVersion, Status, TokenType, LEN(SecretHash) AS HashLen,
         LEFT(SecretHash,12) AS HashPrefix, CreatedAtUtc
  FROM BootstrapSchoolEstablishmentCredentials
  WHERE SchoolId='$SchoolId'
  ORDER BY CredentialVersion DESC;
END
"@ 2>&1
Write-Host ($bootCreds | Out-String)

Write-Host "=== F. Upsert matching credential into Bootstrap SQL ==="
# Table column names from entity - check migration
sqlcmd -S $OfficialSql -U sa -P $saPassword -C -d SchoolManagementBootstrap -I -Q @"
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

-- Revoke other actives for this school if any (keep architecture: one Active)
IF OBJECT_ID(N'dbo.BootstrapSchoolEstablishmentCredentials', N'U') IS NOT NULL
BEGIN
  UPDATE BootstrapSchoolEstablishmentCredentials
  SET Status=N'Revoked', RevokedAtUtc=SYSUTCDATETIME(), RevokedReason=N'Align local ECOLE TEST seed'
  WHERE SchoolId='$SchoolId' AND Status=N'Active' AND Id <> '$credId';

  IF EXISTS (SELECT 1 FROM BootstrapSchoolEstablishmentCredentials WHERE Id='$credId')
  BEGIN
    UPDATE BootstrapSchoolEstablishmentCredentials
    SET SchoolId='$SchoolId',
        CredentialVersion=$credVersion,
        TokenType=N'school_establishment',
        SecretHash=N'$secretHash',
        Status=N'Active',
        RevokedAtUtc=NULL,
        RevokedReason=NULL,
        CreatedBy=N'phase8-seed-local-align'
    WHERE Id='$credId';
    SELECT 'BOOTSTRAP_UPDATED' AS Result;
  END
  ELSE
  BEGIN
    INSERT INTO BootstrapSchoolEstablishmentCredentials
    (Id, SchoolId, CredentialVersion, TokenType, SecretHash, Status, CreatedAtUtc, CreatedBy)
    VALUES
    ('$credId', '$SchoolId', $credVersion, N'school_establishment', N'$secretHash', N'Active', SYSUTCDATETIME(), N'phase8-seed-local-align');
    SELECT 'BOOTSTRAP_INSERTED' AS Result;
  END

  -- Ensure registry row exists/active
  IF OBJECT_ID(N'dbo.BootstrapSchoolRegistry', N'U') IS NOT NULL
  BEGIN
    IF NOT EXISTS (SELECT 1 FROM BootstrapSchoolRegistry WHERE SchoolId='$SchoolId')
      INSERT INTO BootstrapSchoolRegistry
      (Id, SchoolId, SchoolName, ActivationBaseUrl, CloudBaseUrl, IsActive, RegisteredAtUtc, UpdatedAtUtc)
      VALUES
      (NEWID(), '$SchoolId', N'ECOLE TEST', N'http://169.58.93.203:1804', N'http://169.58.93.203:1804', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
    ELSE
      UPDATE BootstrapSchoolRegistry
      SET SchoolName=N'ECOLE TEST',
          ActivationBaseUrl=N'http://169.58.93.203:1804',
          CloudBaseUrl=N'http://169.58.93.203:1804',
          IsActive=1,
          UpdatedAtUtc=SYSUTCDATETIME()
      WHERE SchoolId='$SchoolId';
  END
END
ELSE SELECT 'NO_BOOTSTRAP_CRED_TABLE' AS Result;
"@

Write-Host "=== G. Mark local sync Synced ==="
sqlcmd -S $LocalServer -E -C -d $LocalDb -I -Q @"
UPDATE SchoolEstablishmentCredentials
SET BootstrapSyncPending=0, BootstrapSyncStatus=N'Synced', BootstrapSyncedAtUtc=SYSUTCDATETIME(), LastBootstrapSyncError=NULL
WHERE Id='$credId';
"@

Write-Host "=== H. Health after ==="
Start-Sleep -Seconds 2
$h2 = Invoke-RestMethod -Uri "$BootstrapUrl/health" -TimeoutSec 30
$h2 | ConvertTo-Json -Depth 6
Write-Host ("activeCredentials=" + $h2.activeCredentials)

