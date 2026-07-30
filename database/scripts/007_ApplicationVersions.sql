-- 007 : table ApplicationVersions + AppSchemaVersion (mises à jour auto)
IF OBJECT_ID(N'dbo.ApplicationVersions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApplicationVersions
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ApplicationVersions PRIMARY KEY,
        Version NVARCHAR(32) NOT NULL,
        MinimumVersion NVARCHAR(32) NOT NULL,
        Mandatory BIT NOT NULL CONSTRAINT DF_ApplicationVersions_Mandatory DEFAULT(0),
        ReleaseDate DATE NOT NULL,
        ReleaseNotes NVARCHAR(MAX) NOT NULL,
        DesktopUrl NVARCHAR(1000) NULL,
        MobileUrl NVARCHAR(1000) NULL,
        Sha256 NVARCHAR(128) NULL,
        Size BIGINT NULL,
        SchemaVersion INT NOT NULL CONSTRAINT DF_ApplicationVersions_SchemaVersion DEFAULT(0),
        Active BIT NOT NULL CONSTRAINT DF_ApplicationVersions_Active DEFAULT(1),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ApplicationVersions_CreatedAtUtc DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ApplicationVersions_Active ON dbo.ApplicationVersions(Active);
END
GO

IF OBJECT_ID(N'dbo.AppSchemaVersion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSchemaVersion
    (
        Id INT NOT NULL CONSTRAINT PK_AppSchemaVersion PRIMARY KEY CHECK (Id = 1),
        SchemaVersion INT NOT NULL,
        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_AppSchemaVersion_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
    );
    INSERT INTO dbo.AppSchemaVersion (Id, SchemaVersion) VALUES (1, 1);
END
GO

-- Exemple de version publiée (désactivez Active=0 tant que les binaires ne sont pas hébergés)
IF NOT EXISTS (SELECT 1 FROM dbo.ApplicationVersions WHERE Version = N'1.0.1')
BEGIN
    INSERT INTO dbo.ApplicationVersions
    (
        Id, Version, MinimumVersion, Mandatory, ReleaseDate, ReleaseNotes,
        DesktopUrl, MobileUrl, Sha256, Size, SchemaVersion, Active, CreatedAtUtc
    )
    VALUES
    (
        NEWID(),
        N'1.0.1',
        N'1.0.0',
        0,
        CAST(GETUTCDATE() AS DATE),
        N'["Correctifs de stabilité","Module de mise à jour automatique","Améliorations mobile"]',
        N'https://169.58.93.203:1804/updates/DesktopSetup-1.0.1.exe',
        N'https://169.58.93.203:1804/updates/SuperEcole-1.0.1.apk',
        N'0000000000000000000000000000000000000000000000000000000000000000',
        0,
        1,
        0, -- Active=0 jusqu'à publication réelle
        SYSUTCDATETIME()
    );
END
GO
