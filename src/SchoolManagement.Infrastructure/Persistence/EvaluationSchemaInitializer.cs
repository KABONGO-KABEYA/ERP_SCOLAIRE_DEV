using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class EvaluationSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<EvaluationSchemaInitializer> _logger;

    public EvaluationSchemaInitializer(string connectionString, ILogger<EvaluationSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EvaluationTypes')
            BEGIN
                CREATE TABLE [EvaluationTypes] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [Code] nvarchar(20) NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    [IsActive] bit NOT NULL CONSTRAINT [DF_EvaluationTypes_IsActive] DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_EvaluationTypes_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_EvaluationTypes] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_EvaluationTypes_Schools_SchoolId]
                        FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
                );
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            SET QUOTED_IDENTIFIER ON;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EvaluationTypes_SchoolId_Code')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EvaluationTypes')
            BEGIN
                CREATE UNIQUE INDEX [IX_EvaluationTypes_SchoolId_Code]
                    ON [EvaluationTypes] ([SchoolId], [Code])
                    WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EvaluationTypes')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Schools')
            BEGIN
                INSERT INTO [EvaluationTypes] ([Id], [SchoolId], [Code], [Name], [IsActive], [CreatedAt], [IsDeleted])
                SELECT NEWID(), s.[Id], d.[Code], d.[Name], 1, GETUTCDATE(), 0
                FROM [Schools] s
                CROSS JOIN (VALUES
                    ('DEVOIR', N'Devoir'),
                    ('INTERRO', N'Interrogation'),
                    ('EXAMEN', N'Examen'),
                    ('COMPOSITION', N'Composition')
                ) AS d([Code], [Name])
                WHERE s.[IsDeleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [EvaluationTypes] et
                      WHERE et.[SchoolId] = s.[Id] AND et.[Code] = d.[Code] AND et.[IsDeleted] = 0);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'EnrollmentId')
            BEGIN
                ALTER TABLE [Evaluations] ADD [EnrollmentId] uniqueidentifier NULL;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'EvaluationTypeId')
            BEGIN
                ALTER TABLE [Evaluations] ADD [EvaluationTypeId] uniqueidentifier NULL;
            END
            """, cancellationToken);

        // SQL dynamique : SQL Server valide les noms de colonnes à la compilation même dans un IF.
        await ExecuteAsync(connection, """
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'EvaluationType')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'EvaluationTypeId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EvaluationTypes')
            BEGIN
                EXEC(N'
                    UPDATE ev
                    SET ev.[EvaluationTypeId] = et.[Id]
                    FROM [Evaluations] ev
                    INNER JOIN [ClassRooms] cr ON cr.[Id] = ev.[ClassRoomId]
                    INNER JOIN [EvaluationTypes] et ON et.[SchoolId] = cr.[SchoolId] AND et.[IsDeleted] = 0
                    INNER JOIN (VALUES
                        (1, ''DEVOIR''),
                        (2, ''INTERRO''),
                        (3, ''EXAMEN''),
                        (4, ''COMPOSITION'')
                    ) AS map([LegacyType], [Code]) ON map.[LegacyType] = ev.[EvaluationType] AND map.[Code] = et.[Code]
                    WHERE ev.[EvaluationTypeId] IS NULL;
                ');
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'EvaluationTypeId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EvaluationTypes')
            BEGIN
                UPDATE ev
                SET ev.[EvaluationTypeId] = et.[Id]
                FROM [Evaluations] ev
                INNER JOIN [ClassRooms] cr ON cr.[Id] = ev.[ClassRoomId]
                INNER JOIN [EvaluationTypes] et ON et.[SchoolId] = cr.[SchoolId] AND et.[Code] = 'INTERRO' AND et.[IsDeleted] = 0
                WHERE ev.[EvaluationTypeId] IS NULL;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'CourseAssignmentId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CourseAssignments')
            BEGIN
                UPDATE ev
                SET ev.[CourseAssignmentId] = ca.[Id]
                FROM [Evaluations] ev
                INNER JOIN [CourseAssignments] ca
                    ON ca.[ClassRoomId] = ev.[ClassRoomId]
                   AND ca.[AcademicYearId] = ev.[AcademicYearId]
                   AND ca.[CourseId] = ev.[CourseId]
                   AND ca.[IsDeleted] = 0
                WHERE ev.[CourseAssignmentId] IS NULL;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Evaluations_Enrollments_EnrollmentId')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'EnrollmentId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Enrollments')
            BEGIN
                ALTER TABLE [Evaluations] ADD CONSTRAINT [FK_Evaluations_Enrollments_EnrollmentId]
                    FOREIGN KEY ([EnrollmentId]) REFERENCES [Enrollments] ([Id]) ON DELETE NO ACTION;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Evaluations_CourseAssignments_CourseAssignmentId')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'CourseAssignmentId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CourseAssignments')
               AND NOT EXISTS (SELECT 1 FROM [Evaluations] WHERE [CourseAssignmentId] IS NULL)
            BEGIN
                ALTER TABLE [Evaluations] ALTER COLUMN [CourseAssignmentId] uniqueidentifier NOT NULL;
                ALTER TABLE [Evaluations] ADD CONSTRAINT [FK_Evaluations_CourseAssignments_CourseAssignmentId]
                    FOREIGN KEY ([CourseAssignmentId]) REFERENCES [CourseAssignments] ([Id]) ON DELETE NO ACTION;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Evaluations_EvaluationTypes_EvaluationTypeId')
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'EvaluationTypeId')
               AND EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EvaluationTypes')
               AND NOT EXISTS (SELECT 1 FROM [Evaluations] WHERE [EvaluationTypeId] IS NULL)
            BEGIN
                ALTER TABLE [Evaluations] ALTER COLUMN [EvaluationTypeId] uniqueidentifier NOT NULL;
                ALTER TABLE [Evaluations] ADD CONSTRAINT [FK_Evaluations_EvaluationTypes_EvaluationTypeId]
                    FOREIGN KEY ([EvaluationTypeId]) REFERENCES [EvaluationTypes] ([Id]) ON DELETE NO ACTION;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Evaluations') AND name = 'EvaluationType')
               AND NOT EXISTS (SELECT 1 FROM [Evaluations] WHERE [EvaluationTypeId] IS NULL)
            BEGIN
                EXEC(N'ALTER TABLE [Evaluations] DROP COLUMN [EvaluationType];');
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma Evaluations vérifié (EvaluationTypes, EnrollmentId, CourseAssignmentId, EvaluationTypeId).");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
