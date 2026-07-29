-- Maxima par période (cours × classe pédagogique × période académique)
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MaximaParPeriode')
BEGIN
    CREATE TABLE [MaximaParPeriode] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [PedagogicalClassId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [AcademicPeriodId] uniqueidentifier NOT NULL,
        [Maximum] int NOT NULL CONSTRAINT [DF_MaximaParPeriode_Maximum] DEFAULT 20,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL CONSTRAINT [DF_MaximaParPeriode_IsDeleted] DEFAULT 0,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_MaximaParPeriode] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaximaParPeriode_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
        CONSTRAINT [FK_MaximaParPeriode_PedagogicalClasses_PedagogicalClassId] FOREIGN KEY ([PedagogicalClassId]) REFERENCES [PedagogicalClasses] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MaximaParPeriode_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]),
        CONSTRAINT [FK_MaximaParPeriode_AcademicPeriods_AcademicPeriodId] FOREIGN KEY ([AcademicPeriodId]) REFERENCES [AcademicPeriods] ([Id])
    );

    CREATE UNIQUE INDEX [IX_MaximaParPeriode_PedagogicalClassId_CourseId_AcademicPeriodId]
        ON [MaximaParPeriode] ([PedagogicalClassId], [CourseId], [AcademicPeriodId])
        WHERE [IsDeleted] = 0;
    CREATE INDEX [IX_MaximaParPeriode_IsDeleted] ON [MaximaParPeriode] ([IsDeleted]);
    CREATE INDEX [IX_MaximaParPeriode_AcademicPeriodId] ON [MaximaParPeriode] ([AcademicPeriodId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaximaParPeriode_PedagogicalClassId_CourseId_AcademicPeriodId')
   AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MaximaParPeriode')
BEGIN
    CREATE UNIQUE INDEX [IX_MaximaParPeriode_PedagogicalClassId_CourseId_AcademicPeriodId]
        ON [MaximaParPeriode] ([PedagogicalClassId], [CourseId], [AcademicPeriodId])
        WHERE [IsDeleted] = 0;
END;
