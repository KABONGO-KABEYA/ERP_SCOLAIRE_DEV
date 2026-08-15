-- Migration test Lot 2B-1 : schéma 2 → 3 (idempotent, GO + ALTER TABLE)
IF COL_LENGTH(N'dbo.Lot2B1_Probe', N'Step3') IS NULL
BEGIN
    ALTER TABLE dbo.Lot2B1_Probe ADD Step3 NVARCHAR(32) NULL;
END
GO

UPDATE dbo.Lot2B1_Probe SET Step3 = N'after-2-3' WHERE Id = 1;
GO
