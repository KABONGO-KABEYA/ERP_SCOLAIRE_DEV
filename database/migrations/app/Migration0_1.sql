-- Migration schéma 0 → 1 (exemple pour MigrationManager)
-- Appliquée automatiquement si AppSchemaVersion = 0
IF COL_LENGTH('dbo.ApplicationVersions', 'SchemaVersion') IS NULL
BEGIN
    ALTER TABLE dbo.ApplicationVersions ADD SchemaVersion INT NOT NULL CONSTRAINT DF_ApplicationVersions_SchemaVersion_mig DEFAULT(0);
END
GO
