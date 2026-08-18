using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class AttendanceSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<AttendanceSchemaInitializer> _logger;

    public AttendanceSchemaInitializer(string connectionString, ILogger<AttendanceSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StudentAttendances')
            BEGIN
                CREATE TABLE [StudentAttendances] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [EnrollmentId] uniqueidentifier NULL,
                    [StudentId] uniqueidentifier NOT NULL,
                    [ClassRoomId] uniqueidentifier NOT NULL,
                    [CourseAssignmentId] uniqueidentifier NULL,
                    [AttendanceDate] date NOT NULL,
                    [Presence] int NOT NULL CONSTRAINT [DF_StudentAttendances_Presence] DEFAULT 1,
                    [IsPresent] bit NOT NULL CONSTRAINT [DF_StudentAttendances_IsPresent] DEFAULT 1,
                    [IsLate] bit NOT NULL CONSTRAINT [DF_StudentAttendances_IsLate] DEFAULT 0,
                    [Justification] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_StudentAttendances_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_StudentAttendances] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_StudentAttendances_Students_StudentId]
                        FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_StudentAttendances_ClassRooms_ClassRoomId]
                        FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION
                );
            END
            """, cancellationToken);

        await EnsureColumnAsync(
            connection,
            "StudentAttendances",
            "SchoolId",
            """
            ALTER TABLE [StudentAttendances]
            ADD [SchoolId] uniqueidentifier NOT NULL
                CONSTRAINT [DF_StudentAttendances_SchoolId] DEFAULT '00000000-0000-0000-0000-000000000000';
            """,
            cancellationToken);

        await EnsureColumnAsync(
            connection,
            "StudentAttendances",
            "EnrollmentId",
            """
            ALTER TABLE [StudentAttendances]
            ADD [EnrollmentId] uniqueidentifier NULL;
            """,
            cancellationToken);

        await EnsureColumnAsync(
            connection,
            "StudentAttendances",
            "Presence",
            """
            ALTER TABLE [StudentAttendances]
            ADD [Presence] int NOT NULL
                CONSTRAINT [DF_StudentAttendances_Presence] DEFAULT 1;
            """,
            cancellationToken);

        await EnsureColumnAsync(
            connection,
            "TeacherAttendances",
            "SchoolId",
            """
            ALTER TABLE [TeacherAttendances]
            ADD [SchoolId] uniqueidentifier NOT NULL
                CONSTRAINT [DF_TeacherAttendances_SchoolId] DEFAULT '00000000-0000-0000-0000-000000000000';
            """,
            cancellationToken);

        await ExecuteAsync(connection, """
            UPDATE sa
            SET sa.[SchoolId] = s.[SchoolId]
            FROM [StudentAttendances] sa
            INNER JOIN [Students] s ON s.[Id] = sa.[StudentId]
            WHERE sa.[SchoolId] = '00000000-0000-0000-0000-000000000000';
            """, cancellationToken);

        await ExecuteAsync(connection, """
            UPDATE ta
            SET ta.[SchoolId] = t.[SchoolId]
            FROM [TeacherAttendances] ta
            INNER JOIN [Teachers] t ON t.[Id] = ta.[TeacherId]
            WHERE ta.[SchoolId] = '00000000-0000-0000-0000-000000000000';
            """, cancellationToken);

        await ExecuteAsync(connection, """
            UPDATE sa
            SET sa.[Presence] = CASE
                WHEN sa.[IsPresent] = 0 THEN 0
                WHEN sa.[IsLate] = 1 THEN 2
                ELSE 1
            END
            FROM [StudentAttendances] sa
            WHERE sa.[Presence] IS NULL
               OR sa.[Presence] NOT IN (0, 1, 2, 3);
            """, cancellationToken);

        await ExecuteAsync(connection, """
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
            """, cancellationToken);

        await ExecuteAsync(connection, """
            UPDATE sa
            SET sa.[EnrollmentId] = e.[Id]
            FROM [StudentAttendances] sa
            INNER JOIN [Enrollments] e
                ON e.[StudentId] = sa.[StudentId]
               AND e.[ClassRoomId] = sa.[ClassRoomId]
               AND e.[IsDeleted] = 0
               AND e.[IsActive] = 1
            WHERE sa.[EnrollmentId] IS NULL;
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_StudentAttendances_Enrollments_EnrollmentId')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudentAttendances') AND name = 'EnrollmentId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Enrollments')
               AND NOT EXISTS (SELECT 1 FROM [StudentAttendances] WHERE [EnrollmentId] IS NULL)
            BEGIN
                ALTER TABLE [StudentAttendances] ALTER COLUMN [EnrollmentId] uniqueidentifier NOT NULL;
                ALTER TABLE [StudentAttendances] ADD CONSTRAINT [FK_StudentAttendances_Enrollments_EnrollmentId]
                    FOREIGN KEY ([EnrollmentId]) REFERENCES [Enrollments] ([Id]) ON DELETE NO ACTION;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_StudentAttendances_Enrollments_EnrollmentId')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudentAttendances') AND name = 'EnrollmentId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Enrollments')
            BEGIN
                ALTER TABLE [StudentAttendances] ADD CONSTRAINT [FK_StudentAttendances_Enrollments_EnrollmentId]
                    FOREIGN KEY ([EnrollmentId]) REFERENCES [Enrollments] ([Id]) ON DELETE NO ACTION;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentAttendances_EnrollmentId_AttendanceDate')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudentAttendances') AND name = 'EnrollmentId')
            BEGIN
                CREATE INDEX [IX_StudentAttendances_EnrollmentId_AttendanceDate]
                    ON [StudentAttendances] ([EnrollmentId], [AttendanceDate]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            SET QUOTED_IDENTIFIER ON;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudentAttendances_EnrollmentId_AttendanceDate_CourseAssignmentId')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('StudentAttendances') AND name = 'EnrollmentId')
            BEGIN
                CREATE UNIQUE INDEX [IX_StudentAttendances_EnrollmentId_AttendanceDate_CourseAssignmentId]
                    ON [StudentAttendances] ([EnrollmentId], [AttendanceDate], [CourseAssignmentId])
                    WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        _logger.LogInformation(
            "Schéma présences vérifié (StudentAttendances.SchoolId, EnrollmentId, Presence, TeacherAttendances.SchoolId).");
        // Les index baseline IX_TeacherAttendances_TeacherId_AttendanceDate (UNIQUE)
        // et IX_StudentAttendances_StudentId_AttendanceDate (non UNIQUE) restent globaux :
        // TeacherId/StudentId sont des PK GUID, pas une clé métier partagée (contrairement à Courses.Code).
        // Unicité métier élèves : IX_StudentAttendances_EnrollmentId_AttendanceDate_CourseAssignmentId
        // (EnrollmentId = PK GUID d'inscription, déjà propre à une école).
    }

    private static async Task EnsureColumnAsync(
        SqlConnection connection,
        string tableName,
        string columnName,
        string alterSql,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = """
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @table AND COLUMN_NAME = @column
            """;
        check.Parameters.AddWithValue("@table", tableName);
        check.Parameters.AddWithValue("@column", columnName);

        var exists = await check.ExecuteScalarAsync(cancellationToken) is not null;
        if (exists)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = alterSql;
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
