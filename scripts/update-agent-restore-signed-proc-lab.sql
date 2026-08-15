/*
  Lot 2B-5B-Impl — procédure RESTORE signée, LABORATOIRE UNIQUEMENT.

  Owner labo : ErpScolaireRestoreOwner_Lab (DENY CONNECT) — pas l'agent, pas un certificat
  (Msg 15353 : un login certificat ne peut pas posséder une base).
  Signataire : IMPERSONATE ErpScolaireRestoreOwner_Lab (EXECUTE AS LOGIN), hors rôle dbcreator.
  NE PAS exécuter contre SchoolManagementRDC, _Dev, _Development, _Production.

  Variables sqlcmd :
    CertPassword  — mot de passe de la clé privée du certificat
    AgentPassword — mot de passe du login UpdateAgent labo

  BackupRoot labo figé :
    D:\Mes Projet\ERP_Administration_Scolaire_2026\logs\ua-2b5b-backups
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_ID(N'SchoolManagementRDC_UpdateIntegration') IS NULL
BEGIN
    RAISERROR('Base labo SchoolManagementRDC_UpdateIntegration absente.', 16, 1);
    RETURN;
END

USE master;

IF OBJECT_ID(N'dbo.ErpScolaire_RestoreAllowlist', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ErpScolaire_RestoreAllowlist
    (
        DatabaseName sysname NOT NULL CONSTRAINT PK_ErpScolaire_RestoreAllowlist PRIMARY KEY,
        BackupRoot nvarchar(512) NOT NULL
    );
END

DELETE FROM dbo.ErpScolaire_RestoreAllowlist
WHERE DatabaseName <> N'SchoolManagementRDC_UpdateIntegration';

DECLARE @BackupRoot nvarchar(512) = N'D:\Mes Projet\ERP_Administration_Scolaire_2026\logs\ua-2b5b-backups';
IF @BackupRoot LIKE N'\\%' OR @BackupRoot LIKE N'//%'
BEGIN
    RAISERROR('BackupRoot UNC refusé.', 16, 1);
    RETURN;
END
IF RIGHT(@BackupRoot, 1) NOT IN (N'\', N'/')
    SET @BackupRoot = @BackupRoot + N'\';

IF EXISTS (SELECT 1 FROM dbo.ErpScolaire_RestoreAllowlist WHERE DatabaseName = N'SchoolManagementRDC_UpdateIntegration')
    UPDATE dbo.ErpScolaire_RestoreAllowlist
        SET BackupRoot = @BackupRoot
        WHERE DatabaseName = N'SchoolManagementRDC_UpdateIntegration';
ELSE
    INSERT INTO dbo.ErpScolaire_RestoreAllowlist (DatabaseName, BackupRoot)
    VALUES (N'SchoolManagementRDC_UpdateIntegration', @BackupRoot);

IF NOT EXISTS (SELECT 1 FROM sys.certificates WHERE name = N'ErpScolaireRestoreCert_Lab2B5B')
BEGIN
    CREATE CERTIFICATE ErpScolaireRestoreCert_Lab2B5B
        ENCRYPTION BY PASSWORD = N'$(CertPassword)'
        WITH SUBJECT = 'ERP Scolaire lab 2B-5B restore signing',
             EXPIRY_DATE = '20301231';
END

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'ErpScolaireRestoreCert_Lab')
    CREATE LOGIN [ErpScolaireRestoreCert_Lab] FROM CERTIFICATE ErpScolaireRestoreCert_Lab2B5B;

DENY CONNECT SQL TO [ErpScolaireRestoreCert_Lab];

IF EXISTS (SELECT 1 FROM sys.server_role_members rm
           JOIN sys.server_principals r ON r.principal_id = rm.role_principal_id
           JOIN sys.server_principals m ON m.principal_id = rm.member_principal_id
           WHERE m.name = N'ErpScolaireRestoreCert_Lab' AND r.name IN (N'sysadmin', N'dbcreator'))
BEGIN
    RAISERROR('Refus : le login certificat ne doit pas être sysadmin/dbcreator.', 16, 1);
    RETURN;
END

IF OBJECT_ID(N'dbo.ErpScolaire_RestoreSchoolDatabase', N'P') IS NOT NULL
    DROP PROCEDURE dbo.ErpScolaire_RestoreSchoolDatabase;
IF OBJECT_ID(N'dbo.ErpScolaire_VerifySchoolBackup', N'P') IS NOT NULL
    DROP PROCEDURE dbo.ErpScolaire_VerifySchoolBackup;
GO

CREATE PROCEDURE dbo.ErpScolaire_RestoreSchoolDatabase
    @DatabaseName sysname,
    @BackupPath nvarchar(512)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Target sysname = N'SchoolManagementRDC_UpdateIntegration';
    DECLARE @BackupRoot nvarchar(512);
    DECLARE @HeaderDb nvarchar(128);
    DECLARE @HasChecksum bit;

    SELECT @BackupRoot = BackupRoot
    FROM dbo.ErpScolaire_RestoreAllowlist
    WHERE DatabaseName = @Target;

    IF @BackupRoot IS NULL
    BEGIN
        RAISERROR(N'ErpScolaire restore : allowlist labo absente.', 16, 1);
        RETURN;
    END

    IF @DatabaseName IS NULL OR @DatabaseName <> @Target
    BEGIN
        RAISERROR(N'ErpScolaire restore : base non autorisée.', 16, 1);
        RETURN;
    END

    IF @BackupPath IS NULL OR LEN(@BackupPath) = 0
    BEGIN
        RAISERROR(N'ErpScolaire restore : chemin vide.', 16, 1);
        RETURN;
    END

    IF @BackupPath LIKE N'\\%' OR @BackupPath LIKE N'//%'
    BEGIN
        RAISERROR(N'ErpScolaire restore : UNC refusé.', 16, 1);
        RETURN;
    END

    IF CHARINDEX(N'..', @BackupPath) > 0
        OR CHARINDEX(N'''', @BackupPath) > 0
        OR CHARINDEX(N';', @BackupPath) > 0
        OR CHARINDEX(CHAR(0), @BackupPath) > 0
    BEGIN
        RAISERROR(N'ErpScolaire restore : chemin invalide.', 16, 1);
        RETURN;
    END

    IF LOWER(RIGHT(@BackupPath, 4)) <> N'.bak'
    BEGIN
        RAISERROR(N'ErpScolaire restore : extension .bak requise.', 16, 1);
        RETURN;
    END

    IF RIGHT(@BackupRoot, 1) NOT IN (N'\', N'/')
        SET @BackupRoot = @BackupRoot + N'\';

    IF LEFT(@BackupPath, LEN(@BackupRoot)) COLLATE Latin1_General_CI_AI
        <> @BackupRoot COLLATE Latin1_General_CI_AI
    BEGIN
        RAISERROR(N'ErpScolaire restore : hors répertoire Backup autorisé.', 16, 1);
        RETURN;
    END

    DECLARE @Header TABLE
    (
        BackupName nvarchar(128) NULL,
        BackupDescription nvarchar(255) NULL,
        BackupType smallint NULL,
        ExpirationDate datetime NULL,
        Compressed tinyint NULL,
        Position smallint NULL,
        DeviceType tinyint NULL,
        UserName nvarchar(128) NULL,
        ServerName nvarchar(128) NULL,
        DatabaseName nvarchar(128) NULL,
        DatabaseVersion int NULL,
        DatabaseCreationDate datetime NULL,
        BackupSize numeric(20, 0) NULL,
        FirstLSN numeric(25, 0) NULL,
        LastLSN numeric(25, 0) NULL,
        CheckpointLSN numeric(25, 0) NULL,
        DatabaseBackupLSN numeric(25, 0) NULL,
        BackupStartDate datetime NULL,
        BackupFinishDate datetime NULL,
        SortOrder smallint NULL,
        CodePage smallint NULL,
        UnicodeLocaleId int NULL,
        UnicodeComparisonStyle int NULL,
        CompatibilityLevel tinyint NULL,
        SoftwareVendorId int NULL,
        SoftwareVersionMajor int NULL,
        SoftwareVersionMinor int NULL,
        SoftwareVersionBuild int NULL,
        MachineName nvarchar(128) NULL,
        Flags int NULL,
        BindingID uniqueidentifier NULL,
        RecoveryForkID uniqueidentifier NULL,
        Collation nvarchar(128) NULL,
        FamilyGUID uniqueidentifier NULL,
        HasBulkLoggedData bit NULL,
        IsSnapshot bit NULL,
        IsReadOnly bit NULL,
        IsSingleUser bit NULL,
        HasBackupChecksums bit NULL,
        IsDamaged bit NULL,
        BeginsLogChain bit NULL,
        HasIncompleteMetaData bit NULL,
        IsForceOffline bit NULL,
        IsCopyOnly bit NULL,
        FirstRecoveryForkID uniqueidentifier NULL,
        ForkPointLSN numeric(25, 0) NULL,
        RecoveryModel nvarchar(60) NULL,
        DifferentialBaseLSN numeric(25, 0) NULL,
        DifferentialBaseGUID uniqueidentifier NULL,
        BackupTypeDescription nvarchar(60) NULL,
        BackupSetGUID uniqueidentifier NULL,
        CompressedBackupSize bigint NULL,
        Containment tinyint NULL,
        KeyAlgorithm nvarchar(32) NULL,
        EncryptorThumbprint varbinary(20) NULL,
        EncryptorType nvarchar(32) NULL
    );

    INSERT INTO @Header
    EXEC sys.sp_executesql
        N'RESTORE HEADERONLY FROM DISK = @Disk',
        N'@Disk nvarchar(512)',
        @Disk = @BackupPath;

    SELECT TOP (1)
        @HeaderDb = DatabaseName,
        @HasChecksum = HasBackupChecksums
    FROM @Header;

    IF @HeaderDb IS NULL OR @HeaderDb <> @Target
    BEGIN
        RAISERROR(N'ErpScolaire restore : header .bak ≠ base autorisée.', 16, 1);
        RETURN;
    END

    IF ISNULL(@HasChecksum, 0) = 0
    BEGIN
        RAISERROR(N'ErpScolaire restore : CHECKSUM absent du backup.', 16, 1);
        RETURN;
    END

    BEGIN TRY
        EXECUTE AS LOGIN = N'ErpScolaireRestoreOwner_Lab';

        ALTER DATABASE [SchoolManagementRDC_UpdateIntegration] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

        RESTORE DATABASE [SchoolManagementRDC_UpdateIntegration]
            FROM DISK = @BackupPath
            WITH REPLACE, CHECKSUM;

        ALTER DATABASE [SchoolManagementRDC_UpdateIntegration] SET MULTI_USER;

        REVERT;
    END TRY
    BEGIN CATCH
        DECLARE @ErrMsg nvarchar(2048) = ERROR_MESSAGE();

        BEGIN TRY
            ALTER DATABASE [SchoolManagementRDC_UpdateIntegration] SET MULTI_USER;
        END TRY
        BEGIN CATCH
            SET @HasChecksum = @HasChecksum;
        END CATCH;

        BEGIN TRY
            REVERT;
        END TRY
        BEGIN CATCH
            SET @HasChecksum = @HasChecksum;
        END CATCH;

        RAISERROR(N'ErpScolaire restore : %s', 16, 1, @ErrMsg);
        RETURN;
    END CATCH
END
GO

ADD SIGNATURE TO dbo.ErpScolaire_RestoreSchoolDatabase
    BY CERTIFICATE ErpScolaireRestoreCert_Lab2B5B
    WITH PASSWORD = N'$(CertPassword)';
GO

CREATE PROCEDURE dbo.ErpScolaire_VerifySchoolBackup
    @DatabaseName sysname,
    @BackupPath nvarchar(512)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Target sysname = N'SchoolManagementRDC_UpdateIntegration';
    DECLARE @BackupRoot nvarchar(512);
    DECLARE @HeaderDb nvarchar(128);
    DECLARE @HasChecksum bit;

    SELECT @BackupRoot = BackupRoot
    FROM dbo.ErpScolaire_RestoreAllowlist
    WHERE DatabaseName = @Target;

    IF @BackupRoot IS NULL
    BEGIN
        RAISERROR(N'ErpScolaire verify : allowlist labo absente.', 16, 1);
        RETURN;
    END

    IF @DatabaseName IS NULL OR @DatabaseName <> @Target
    BEGIN
        RAISERROR(N'ErpScolaire verify : base non autorisée.', 16, 1);
        RETURN;
    END

    IF @BackupPath IS NULL OR LEN(@BackupPath) = 0
        OR @BackupPath LIKE N'\\%' OR @BackupPath LIKE N'//%'
        OR CHARINDEX(N'..', @BackupPath) > 0
        OR CHARINDEX(N'''', @BackupPath) > 0
        OR CHARINDEX(N';', @BackupPath) > 0
        OR LOWER(RIGHT(@BackupPath, 4)) <> N'.bak'
    BEGIN
        RAISERROR(N'ErpScolaire verify : chemin refusé.', 16, 1);
        RETURN;
    END

    IF RIGHT(@BackupRoot, 1) NOT IN (N'\', N'/')
        SET @BackupRoot = @BackupRoot + N'\';

    IF LEFT(@BackupPath, LEN(@BackupRoot)) COLLATE Latin1_General_CI_AI
        <> @BackupRoot COLLATE Latin1_General_CI_AI
    BEGIN
        RAISERROR(N'ErpScolaire verify : hors répertoire Backup autorisé.', 16, 1);
        RETURN;
    END

    DECLARE @Header TABLE
    (
        BackupName nvarchar(128) NULL,
        BackupDescription nvarchar(255) NULL,
        BackupType smallint NULL,
        ExpirationDate datetime NULL,
        Compressed tinyint NULL,
        Position smallint NULL,
        DeviceType tinyint NULL,
        UserName nvarchar(128) NULL,
        ServerName nvarchar(128) NULL,
        DatabaseName nvarchar(128) NULL,
        DatabaseVersion int NULL,
        DatabaseCreationDate datetime NULL,
        BackupSize numeric(20, 0) NULL,
        FirstLSN numeric(25, 0) NULL,
        LastLSN numeric(25, 0) NULL,
        CheckpointLSN numeric(25, 0) NULL,
        DatabaseBackupLSN numeric(25, 0) NULL,
        BackupStartDate datetime NULL,
        BackupFinishDate datetime NULL,
        SortOrder smallint NULL,
        CodePage smallint NULL,
        UnicodeLocaleId int NULL,
        UnicodeComparisonStyle int NULL,
        CompatibilityLevel tinyint NULL,
        SoftwareVendorId int NULL,
        SoftwareVersionMajor int NULL,
        SoftwareVersionMinor int NULL,
        SoftwareVersionBuild int NULL,
        MachineName nvarchar(128) NULL,
        Flags int NULL,
        BindingID uniqueidentifier NULL,
        RecoveryForkID uniqueidentifier NULL,
        Collation nvarchar(128) NULL,
        FamilyGUID uniqueidentifier NULL,
        HasBulkLoggedData bit NULL,
        IsSnapshot bit NULL,
        IsReadOnly bit NULL,
        IsSingleUser bit NULL,
        HasBackupChecksums bit NULL,
        IsDamaged bit NULL,
        BeginsLogChain bit NULL,
        HasIncompleteMetaData bit NULL,
        IsForceOffline bit NULL,
        IsCopyOnly bit NULL,
        FirstRecoveryForkID uniqueidentifier NULL,
        ForkPointLSN numeric(25, 0) NULL,
        RecoveryModel nvarchar(60) NULL,
        DifferentialBaseLSN numeric(25, 0) NULL,
        DifferentialBaseGUID uniqueidentifier NULL,
        BackupTypeDescription nvarchar(60) NULL,
        BackupSetGUID uniqueidentifier NULL,
        CompressedBackupSize bigint NULL,
        Containment tinyint NULL,
        KeyAlgorithm nvarchar(32) NULL,
        EncryptorThumbprint varbinary(20) NULL,
        EncryptorType nvarchar(32) NULL
    );

    INSERT INTO @Header
    EXEC sys.sp_executesql
        N'RESTORE HEADERONLY FROM DISK = @Disk',
        N'@Disk nvarchar(512)',
        @Disk = @BackupPath;

    SELECT TOP (1)
        @HeaderDb = DatabaseName,
        @HasChecksum = HasBackupChecksums
    FROM @Header;

    IF @HeaderDb IS NULL OR @HeaderDb <> @Target OR ISNULL(@HasChecksum, 0) = 0
    BEGIN
        RAISERROR(N'ErpScolaire verify : header .bak invalide.', 16, 1);
        RETURN;
    END

    RESTORE VERIFYONLY FROM DISK = @BackupPath WITH CHECKSUM;
END
GO

ADD SIGNATURE TO dbo.ErpScolaire_VerifySchoolBackup
    BY CERTIFICATE ErpScolaireRestoreCert_Lab2B5B
    WITH PASSWORD = N'$(CertPassword)';
GO

-- Msg 15353 : un login certificat ne peut PAS posséder une base.
-- CREATE ANY DATABASE seul ≠ RESTORE d'une base existante (il faut dbcreator/owner).
-- Élévation : signature + IMPERSONATE du owner dédié (EXECUTE AS LOGIN, pas EXECUTE AS OWNER).
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'ErpScolaireRestoreOwner_Lab')
    CREATE LOGIN [ErpScolaireRestoreOwner_Lab] WITH PASSWORD = N'$(CertPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;

DENY CONNECT SQL TO [ErpScolaireRestoreOwner_Lab];

ALTER AUTHORIZATION ON DATABASE::SchoolManagementRDC_UpdateIntegration TO [ErpScolaireRestoreOwner_Lab];

GRANT IMPERSONATE ON LOGIN::[ErpScolaireRestoreOwner_Lab] TO [ErpScolaireRestoreCert_Lab];

-- HEADERONLY / VERIFYONLY : CREATE ANY DATABASE via signature (pas le rôle dbcreator).
-- RESTORE d'une base existante : EXECUTE AS LOGIN du owner (CREATE ANY DATABASE insuffisant).
GRANT CREATE ANY DATABASE TO [ErpScolaireRestoreCert_Lab];

IF EXISTS (SELECT 1 FROM sys.server_role_members rm
           JOIN sys.server_principals r ON r.principal_id = rm.role_principal_id
           JOIN sys.server_principals m ON m.principal_id = rm.member_principal_id
           WHERE m.name = N'ErpScolaireRestoreCert_Lab' AND r.name IN (N'sysadmin', N'dbcreator'))
BEGIN
    RAISERROR('Refus : le login certificat ne doit pas être sysadmin/dbcreator.', 16, 1);
    RETURN;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'ErpScolaireUA_Lab2B5B')
    CREATE LOGIN [ErpScolaireUA_Lab2B5B] WITH PASSWORD = N'$(AgentPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;

IF EXISTS (SELECT 1 FROM sys.server_role_members rm
           JOIN sys.server_principals r ON r.principal_id = rm.role_principal_id
           JOIN sys.server_principals m ON m.principal_id = rm.member_principal_id
           WHERE m.name = N'ErpScolaireUA_Lab2B5B' AND r.name IN (N'sysadmin', N'dbcreator'))
BEGIN
    RAISERROR('Refus : le login UpdateAgent labo ne doit pas être sysadmin/dbcreator.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ErpScolaireUA_Lab2B5B')
    CREATE USER [ErpScolaireUA_Lab2B5B] FOR LOGIN [ErpScolaireUA_Lab2B5B];

GRANT EXECUTE ON OBJECT::dbo.ErpScolaire_RestoreSchoolDatabase TO [ErpScolaireUA_Lab2B5B];
GRANT EXECUTE ON OBJECT::dbo.ErpScolaire_VerifySchoolBackup TO [ErpScolaireUA_Lab2B5B];
GO

USE [SchoolManagementRDC_UpdateIntegration];
GO

IF DB_NAME() <> N'SchoolManagementRDC_UpdateIntegration'
BEGIN
    RAISERROR('Refus : bascule hors base labo.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ErpScolaireUA_Lab2B5B')
    CREATE USER [ErpScolaireUA_Lab2B5B] FOR LOGIN [ErpScolaireUA_Lab2B5B];

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ua_migrator' AND type = 'R')
    CREATE ROLE ua_migrator;

ALTER ROLE ua_migrator ADD MEMBER [ErpScolaireUA_Lab2B5B];

GRANT CREATE TABLE, CREATE VIEW, CREATE PROCEDURE, CREATE FUNCTION, CREATE TYPE TO ua_migrator;
GRANT ALTER, SELECT, INSERT, UPDATE, DELETE, REFERENCES, EXECUTE ON SCHEMA::dbo TO ua_migrator;
GRANT SELECT, UPDATE ON OBJECT::dbo.AppSchemaVersion TO ua_migrator;
GRANT BACKUP DATABASE TO [ErpScolaireUA_Lab2B5B];

IF IS_ROLEMEMBER(N'db_owner', N'ErpScolaireUA_Lab2B5B') = 1
    ALTER ROLE db_owner DROP MEMBER [ErpScolaireUA_Lab2B5B];

-- ALTER DATABASE SET SINGLE_USER/MULTI_USER : sous EXECUTE AS LOGIN owner (pas de GRANT ALTER au certificat).
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ErpScolaireRestoreCert_Lab')
BEGIN
    REVOKE ALTER ON DATABASE::SchoolManagementRDC_UpdateIntegration FROM [ErpScolaireRestoreCert_Lab];
END
GO

USE master;
SELECT 'CERT_LOGIN' AS k, name AS v FROM sys.server_principals WHERE name = N'ErpScolaireRestoreCert_Lab'
UNION ALL
SELECT 'AGENT_LOGIN', name FROM sys.server_principals WHERE name = N'ErpScolaireUA_Lab2B5B'
UNION ALL
SELECT 'OWNER', SUSER_SNAME(owner_sid) FROM sys.databases WHERE name = N'SchoolManagementRDC_UpdateIntegration'
UNION ALL
SELECT 'CERT_DBCREATOR', CASE WHEN IS_SRVROLEMEMBER(N'dbcreator', N'ErpScolaireRestoreCert_Lab') = 1 THEN 'yes' ELSE 'no' END
UNION ALL
SELECT 'CERT_SYSADMIN', CASE WHEN IS_SRVROLEMEMBER(N'sysadmin', N'ErpScolaireRestoreCert_Lab') = 1 THEN 'yes' ELSE 'no' END
UNION ALL
SELECT 'PROC', name FROM sys.procedures WHERE name = N'ErpScolaire_RestoreSchoolDatabase'
UNION ALL
SELECT 'SIGNATURE', CASE WHEN EXISTS (
    SELECT 1 FROM sys.crypt_properties WHERE major_id = OBJECT_ID(N'dbo.ErpScolaire_RestoreSchoolDatabase')
) THEN 'yes' ELSE 'no' END;
GO
