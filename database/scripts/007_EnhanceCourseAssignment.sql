-- CourseAssignments: configuration réelle des cours par salle / année
-- Idempotent — safe to re-run

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CourseAssignments') AND name = 'PedagogicalClassId')
BEGIN
    ALTER TABLE [CourseAssignments] ADD [PedagogicalClassId] uniqueidentifier NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CourseAssignments') AND name = 'IsActive')
BEGIN
    ALTER TABLE [CourseAssignments] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_CourseAssignments_IsActive] DEFAULT 1;
END
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('CourseAssignments')
      AND name = 'TeacherId'
      AND is_nullable = 0)
BEGIN
    ALTER TABLE [CourseAssignments] ALTER COLUMN [TeacherId] uniqueidentifier NULL;
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CourseAssignments_TeacherId_CourseId_ClassRoomId_AcademicYearId')
BEGIN
    DROP INDEX [IX_CourseAssignments_TeacherId_CourseId_ClassRoomId_AcademicYearId] ON [CourseAssignments];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CourseAssignments_ClassRoomId_AcademicYearId_CourseId')
BEGIN
    SET QUOTED_IDENTIFIER ON;
    CREATE UNIQUE INDEX [IX_CourseAssignments_ClassRoomId_AcademicYearId_CourseId]
        ON [CourseAssignments] ([ClassRoomId], [AcademicYearId], [CourseId])
        WHERE [IsDeleted] = 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CourseAssignments_PedagogicalClasses_PedagogicalClassId')
   AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClasses')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CourseAssignments') AND name = 'PedagogicalClassId')
BEGIN
    UPDATE ca
    SET ca.PedagogicalClassId = cr.PedagogicalClassId
    FROM [CourseAssignments] ca
    INNER JOIN [ClassRooms] cr ON cr.Id = ca.ClassRoomId
    WHERE ca.PedagogicalClassId IS NULL AND cr.PedagogicalClassId IS NOT NULL;

    ALTER TABLE [CourseAssignments] ALTER COLUMN [PedagogicalClassId] uniqueidentifier NOT NULL;

    ALTER TABLE [CourseAssignments] ADD CONSTRAINT [FK_CourseAssignments_PedagogicalClasses_PedagogicalClassId]
        FOREIGN KEY ([PedagogicalClassId]) REFERENCES [PedagogicalClasses] ([Id]) ON DELETE NO ACTION;
END
GO
