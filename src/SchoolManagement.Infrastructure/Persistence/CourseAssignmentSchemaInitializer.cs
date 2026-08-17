using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class CourseAssignmentSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<CourseAssignmentSchemaInitializer> _logger;

    public CourseAssignmentSchemaInitializer(string connectionString, ILogger<CourseAssignmentSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CourseAssignments') AND name = 'PedagogicalClassId')
            BEGIN
                ALTER TABLE [CourseAssignments] ADD [PedagogicalClassId] uniqueidentifier NULL;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CourseAssignments') AND name = 'IsActive')
            BEGIN
                ALTER TABLE [CourseAssignments] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_CourseAssignments_IsActive] DEFAULT 1;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('CourseAssignments')
                  AND name = 'TeacherId'
                  AND is_nullable = 0)
            BEGIN
                ALTER TABLE [CourseAssignments] ALTER COLUMN [TeacherId] uniqueidentifier NULL;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CourseAssignments_TeacherId_CourseId_ClassRoomId_AcademicYearId')
            BEGIN
                DROP INDEX [IX_CourseAssignments_TeacherId_CourseId_ClassRoomId_AcademicYearId] ON [CourseAssignments];
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            SET QUOTED_IDENTIFIER ON;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CourseAssignments_ClassRoomId_AcademicYearId_CourseId')
            BEGIN
                CREATE UNIQUE INDEX [IX_CourseAssignments_ClassRoomId_AcademicYearId_CourseId]
                    ON [CourseAssignments] ([ClassRoomId], [AcademicYearId], [CourseId])
                    WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CourseAssignments') AND name = 'PedagogicalClassId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClasses')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassRooms')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClassRooms') AND name = 'PedagogicalClassId')
            BEGIN
                UPDATE ca
                SET ca.PedagogicalClassId = cr.PedagogicalClassId
                FROM [CourseAssignments] ca
                INNER JOIN [ClassRooms] cr ON cr.Id = ca.ClassRoomId
                WHERE ca.PedagogicalClassId IS NULL AND cr.PedagogicalClassId IS NOT NULL;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CourseAssignments_PedagogicalClasses_PedagogicalClassId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalClasses')
               AND EXISTS (
                   SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('CourseAssignments') AND name = 'PedagogicalClassId')
               AND NOT EXISTS (
                   SELECT 1 FROM [CourseAssignments] WHERE [PedagogicalClassId] IS NULL)
            BEGIN
                ALTER TABLE [CourseAssignments] ALTER COLUMN [PedagogicalClassId] uniqueidentifier NOT NULL;
                ALTER TABLE [CourseAssignments] ADD CONSTRAINT [FK_CourseAssignments_PedagogicalClasses_PedagogicalClassId]
                    FOREIGN KEY ([PedagogicalClassId]) REFERENCES [PedagogicalClasses] ([Id]) ON DELETE NO ACTION;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CourseAssignments') AND name = 'MaxScore')
            BEGIN
                ALTER TABLE [CourseAssignments] ADD [MaxScore] int NOT NULL CONSTRAINT [DF_CourseAssignments_MaxScore] DEFAULT 20;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CourseAssignments') AND name = 'WeeklyHours')
            BEGIN
                ALTER TABLE [CourseAssignments] ADD [WeeklyHours] int NOT NULL CONSTRAINT [DF_CourseAssignments_WeeklyHours] DEFAULT 0;
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma CourseAssignments vérifié (PedagogicalClassId, IsActive, TeacherId nullable, MaxScore, WeeklyHours).");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
