/*
    Module présence — lien StudentAttendances ↔ Enrollments + valeur Presence.
    Idempotent. Exécuter sur SchoolManagementRDC (local ou cloud).
*/
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StudentAttendances')
BEGIN
    RAISERROR('Table StudentAttendances absente. Exécutez d''abord 001_InitialCreate_EF.sql.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudentAttendances') AND name = 'EnrollmentId')
BEGIN
    ALTER TABLE [StudentAttendances] ADD [EnrollmentId] uniqueidentifier NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudentAttendances') AND name = 'Presence')
BEGIN
    ALTER TABLE [StudentAttendances]
    ADD [Presence] int NOT NULL CONSTRAINT [DF_StudentAttendances_Presence] DEFAULT 1;
END
GO

UPDATE sa
SET sa.[Presence] = CASE
    WHEN sa.[IsPresent] = 0 THEN 0
    WHEN sa.[IsLate] = 1 THEN 2
    ELSE 1
END
FROM [StudentAttendances] sa;
GO

UPDATE sa
SET sa.[EnrollmentId] = e.[Id]
FROM [StudentAttendances] sa
INNER JOIN [Enrollments] e
    ON e.[StudentId] = sa.[StudentId]
   AND e.[ClassRoomId] = sa.[ClassRoomId]
   AND e.[IsDeleted] = 0
   AND e.[IsActive] = 1
   AND sa.[AttendanceDate] >= e.[EnrollmentDate]
   AND sa.[AttendanceDate] <= COALESCE(e.[EndDate], '9999-12-31')
WHERE sa.[EnrollmentId] IS NULL;
GO

UPDATE sa
SET sa.[EnrollmentId] = e.[Id]
FROM [StudentAttendances] sa
INNER JOIN [Enrollments] e
    ON e.[StudentId] = sa.[StudentId]
   AND e.[ClassRoomId] = sa.[ClassRoomId]
   AND e.[IsDeleted] = 0
   AND e.[IsActive] = 1
WHERE sa.[EnrollmentId] IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_StudentAttendances_Enrollments_EnrollmentId')
   AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Enrollments')
BEGIN
    ALTER TABLE [StudentAttendances] ADD CONSTRAINT [FK_StudentAttendances_Enrollments_EnrollmentId]
        FOREIGN KEY ([EnrollmentId]) REFERENCES [Enrollments] ([Id]) ON DELETE NO ACTION;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentAttendances_EnrollmentId_AttendanceDate')
BEGIN
    CREATE INDEX [IX_StudentAttendances_EnrollmentId_AttendanceDate]
        ON [StudentAttendances] ([EnrollmentId], [AttendanceDate]);
END
GO

SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentAttendances_EnrollmentId_AttendanceDate_CourseAssignmentId')
BEGIN
    CREATE UNIQUE INDEX [IX_StudentAttendances_EnrollmentId_AttendanceDate_CourseAssignmentId]
        ON [StudentAttendances] ([EnrollmentId], [AttendanceDate], [CourseAssignmentId])
        WHERE [IsDeleted] = 0;
END
GO

PRINT '009_StudentAttendanceEnrollment.sql terminé.';
