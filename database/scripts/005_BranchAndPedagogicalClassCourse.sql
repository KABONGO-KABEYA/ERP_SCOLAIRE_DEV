-- Migration : branches, BranchId sur Courses, PedagogicalClassCourses
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE [SchoolManagementRDC];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Branches')
BEGIN
    CREATE TABLE [Branches] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Program] int NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Branches_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_Branches_SchoolId_Code] ON [Branches] ([SchoolId], [Code]) WHERE [IsDeleted] = 0;
    CREATE INDEX [IX_Branches_IsDeleted] ON [Branches] ([IsDeleted]);
    PRINT N'Table Branches créée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Courses') AND name = 'BranchId')
BEGIN
    ALTER TABLE [Courses] ADD [BranchId] uniqueidentifier NULL;
    PRINT N'Colonne Courses.BranchId ajoutée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Courses_Branches_BranchId')
BEGIN
    ALTER TABLE [Courses] ADD CONSTRAINT [FK_Courses_Branches_BranchId]
        FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE SET NULL;
    PRINT N'Contrainte FK_Courses_Branches_BranchId ajoutée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Courses_SchoolId_Code_ClassRoomId')
BEGIN
    CREATE UNIQUE INDEX [IX_Courses_SchoolId_Code_ClassRoomId]
        ON [Courses] ([SchoolId], [Code], [ClassRoomId])
        WHERE [ClassRoomId] IS NULL AND [IsDeleted] = 0;
    PRINT N'Index IX_Courses_SchoolId_Code_ClassRoomId créé.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClassCourses')
BEGIN
    CREATE TABLE [PedagogicalClassCourses] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [PedagogicalClassId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_PedagogicalClassCourses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PedagogicalClassCourses_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PedagogicalClassCourses_PedagogicalClasses_PedagogicalClassId] FOREIGN KEY ([PedagogicalClassId]) REFERENCES [PedagogicalClasses] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PedagogicalClassCourses_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_PedagogicalClassCourses_PedagogicalClassId_CourseId]
        ON [PedagogicalClassCourses] ([PedagogicalClassId], [CourseId]) WHERE [IsDeleted] = 0;
    CREATE INDEX [IX_PedagogicalClassCourses_CourseId] ON [PedagogicalClassCourses] ([CourseId]);
    CREATE INDEX [IX_PedagogicalClassCourses_IsDeleted] ON [PedagogicalClassCourses] ([IsDeleted]);
    PRINT N'Table PedagogicalClassCourses créée.';
END
GO

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260723140000_AddBranchAndPedagogicalClassCourse')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723140000_AddBranchAndPedagogicalClassCourse', N'8.0.11');
    PRINT N'Migration AddBranchAndPedagogicalClassCourse enregistrée.';
END
GO
