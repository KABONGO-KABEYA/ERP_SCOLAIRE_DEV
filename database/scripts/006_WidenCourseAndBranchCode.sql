-- Élargissement des codes cours et branches (catalogue RDC complet)
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE [SchoolManagementRDC];
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'Courses')
      AND name = N'Code'
      AND max_length < 200)
BEGIN
    ALTER TABLE [Courses] ALTER COLUMN [Code] nvarchar(100) NOT NULL;
    PRINT N'Colonne Courses.Code élargie à nvarchar(100).';
END
GO

IF OBJECT_ID(N'Branches') IS NOT NULL
   AND EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'Branches')
      AND name = N'Code'
      AND max_length < 200)
BEGIN
    ALTER TABLE [Branches] ALTER COLUMN [Code] nvarchar(100) NOT NULL;
    PRINT N'Colonne Branches.Code élargie à nvarchar(100).';
END
GO

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260723153000_WidenCourseAndBranchCode')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723153000_WidenCourseAndBranchCode', N'8.0.11');
    PRINT N'Migration WidenCourseAndBranchCode enregistrée.';
END
GO
