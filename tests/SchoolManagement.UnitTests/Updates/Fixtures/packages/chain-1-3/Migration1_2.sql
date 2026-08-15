-- Migration test Lot 2B-1 : schéma 1 → 2 (idempotent, DDL transactionnel SQL Server)
IF OBJECT_ID(N'dbo.Lot2B1_Probe', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Lot2B1_Probe
    (
        Id INT NOT NULL CONSTRAINT PK_Lot2B1_Probe PRIMARY KEY,
        Step NVARCHAR(32) NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Lot2B1_Probe WHERE Id = 1)
BEGIN
    INSERT INTO dbo.Lot2B1_Probe (Id, Step) VALUES (1, N'after-1-2');
END
GO
