-- Migration : structure pédagogique RDC (PedagogicalClasses + locaux)
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE [SchoolManagementRDC];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClasses')
BEGIN
    CREATE TABLE [PedagogicalClasses] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [TemplateCode] nvarchar(50) NOT NULL,
        [Program] int NOT NULL,
        [LevelOrder] int NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [HumanitiesSection] nvarchar(100) NULL,
        [StudyOption] nvarchar(100) NULL,
        [MinAge] int NULL,
        [MaxAge] int NULL,
        [IsEnabled] bit NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_PedagogicalClasses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PedagogicalClasses_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_PedagogicalClasses_SchoolId_TemplateCode] ON [PedagogicalClasses] ([SchoolId], [TemplateCode]) WHERE [IsDeleted] = 0;
    CREATE INDEX [IX_PedagogicalClasses_SchoolId_IsEnabled] ON [PedagogicalClasses] ([SchoolId], [IsEnabled]);
    CREATE INDEX [IX_PedagogicalClasses_IsDeleted] ON [PedagogicalClasses] ([IsDeleted]);

    PRINT N'Table PedagogicalClasses créée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudyOptions') AND name = 'HumanitiesSection')
BEGIN
    ALTER TABLE [StudyOptions] ADD [HumanitiesSection] nvarchar(100) NULL;
    PRINT N'Colonne StudyOptions.HumanitiesSection ajoutée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClassRooms') AND name = 'PedagogicalClassId')
BEGIN
    ALTER TABLE [ClassRooms] ADD [PedagogicalClassId] uniqueidentifier NULL;
    PRINT N'Colonne ClassRooms.PedagogicalClassId ajoutée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClassRooms') AND name = 'Observations')
BEGIN
    ALTER TABLE [ClassRooms] ADD [Observations] nvarchar(500) NULL;
    PRINT N'Colonne ClassRooms.Observations ajoutée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClassRooms') AND name = 'IsActive')
BEGIN
    ALTER TABLE [ClassRooms] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_ClassRooms_IsActive] DEFAULT 1;
    PRINT N'Colonne ClassRooms.IsActive ajoutée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ClassRooms_PedagogicalClasses_PedagogicalClassId')
BEGIN
    ALTER TABLE [ClassRooms] ADD CONSTRAINT [FK_ClassRooms_PedagogicalClasses_PedagogicalClassId]
        FOREIGN KEY ([PedagogicalClassId]) REFERENCES [PedagogicalClasses] ([Id]) ON DELETE NO ACTION;
    PRINT N'Contrainte FK_ClassRooms_PedagogicalClasses_PedagogicalClassId ajoutée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ClassRooms_PedagogicalClassId_AcademicYearId_Name')
BEGIN
    CREATE UNIQUE INDEX [IX_ClassRooms_PedagogicalClassId_AcademicYearId_Name]
        ON [ClassRooms] ([PedagogicalClassId], [AcademicYearId], [Name])
        WHERE [PedagogicalClassId] IS NOT NULL AND [IsDeleted] = 0;
    PRINT N'Index IX_ClassRooms_PedagogicalClassId_AcademicYearId_Name créé.';
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClassRooms') AND name = 'Code' AND max_length < 160)
BEGIN
    ALTER TABLE [ClassRooms] ALTER COLUMN [Code] nvarchar(80) NOT NULL;
    PRINT N'Colonne ClassRooms.Code alignée sur nvarchar(80).';
END
GO
