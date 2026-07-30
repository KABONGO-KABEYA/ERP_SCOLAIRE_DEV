-- Données initiales de référence pour ERP Scolaire RDC
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE [SchoolManagementRDC];
GO

DECLARE @SchoolId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @YearId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @AdminRoleId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333';

IF NOT EXISTS (SELECT 1 FROM Schools WHERE Id = @SchoolId)
BEGIN
    INSERT INTO Schools (Id, Name, LegalName, City, Province, Country, DefaultCurrency, IsActive, CreatedAt, IsDeleted)
    VALUES (@SchoolId, N'École Démonstration RDC', N'École Démonstration RDC SARL', N'Kinshasa', N'Kinshasa', N'RDC', 1, 1, SYSUTCDATETIME(), 0);

    INSERT INTO AcademicYears (Id, SchoolId, Label, StartDate, EndDate, IsCurrent, IsClosed, CreatedAt, IsDeleted)
    VALUES (@YearId, @SchoolId, N'2025-2026', '2025-09-01', '2026-06-30', 1, 0, SYSUTCDATETIME(), 0);

    INSERT INTO AcademicPeriods (Id, AcademicYearId, Name, PeriodType, OrderIndex, StartDate, EndDate, IsClosed, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), @YearId, N'1er Trimestre', 1, 1, '2025-09-01', '2025-12-15', 0, SYSUTCDATETIME(), 0),
        (NEWID(), @YearId, N'2e Trimestre', 1, 2, '2026-01-05', '2026-03-31', 0, SYSUTCDATETIME(), 0),
        (NEWID(), @YearId, N'3e Trimestre', 1, 3, '2026-04-01', '2026-06-30', 0, SYSUTCDATETIME(), 0);

    INSERT INTO Sections (Id, SchoolId, Code, Name, Cycle, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), @SchoolId, N'MAT', N'Maternelle', 1, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'PRI', N'Primaire', 1, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'CTEB', N'Secondaire générale', 2, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'HUM', N'Humanité', 2, SYSUTCDATETIME(), 0);

    INSERT INTO FeeTypes (Id, SchoolId, Code, Name, Currency, IsMandatory, IsActive, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), @SchoolId, N'INSCR', N'Frais d''inscription', 1, 1, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'MINVAL', N'Minerval', 1, 1, 1, SYSUTCDATETIME(), 0);

    INSERT INTO FeeInstallments (Id, SchoolId, Name, SortOrder, IsActive, CreatedAt, IsDeleted)
    VALUES
        (NEWID(), @SchoolId, N'Inscription', 1, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'1ère tranche', 2, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'2ème tranche', 3, 1, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'3ème tranche', 4, 1, SYSUTCDATETIME(), 0);

    INSERT INTO CashRegisters (Id, SchoolId, Code, Name, Currency, IsActive, CreatedAt, IsDeleted)
    VALUES (NEWID(), @SchoolId, N'CAISSE1', N'Caisse principale', 1, 1, SYSUTCDATETIME(), 0);

    INSERT INTO Banks (Id, SchoolId, Name, AccountNumber, Currency, IsActive, CreatedAt, IsDeleted)
    VALUES (NEWID(), @SchoolId, N'Banque Principale', N'0000000001', 1, 1, SYSUTCDATETIME(), 0);

    INSERT INTO Roles (Id, SchoolId, Name, Code, SystemRole, CreatedAt, IsDeleted)
    VALUES
        (@AdminRoleId, @SchoolId, N'Administrateur', N'ADMIN', 1, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'Direction', N'DIRECTION', 2, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'Enseignant', N'ENSEIGNANT', 5, SYSUTCDATETIME(), 0),
        (NEWID(), @SchoolId, N'Parent', N'PARENT', 6, SYSUTCDATETIME(), 0);

    PRINT N'Données de démonstration insérées (école, année, périodes, frais, rôles).';
    PRINT N'Compte admin : à créer à l''étape Infrastructure (authentification JWT).';
END
ELSE
    PRINT N'École de démonstration déjà présente — seed ignoré.';
GO
