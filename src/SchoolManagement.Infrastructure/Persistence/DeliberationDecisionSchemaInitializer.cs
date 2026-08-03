using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class DeliberationDecisionSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DeliberationDecisionSchemaInitializer> _logger;

    public DeliberationDecisionSchemaInitializer(
        string connectionString,
        ILogger<DeliberationDecisionSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliberationDecisions')
            BEGIN
                CREATE TABLE [DeliberationDecisions] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [AcademicYearId] uniqueidentifier NOT NULL,
                    [ClassRoomId] uniqueidentifier NOT NULL,
                    [AcademicPeriodId] uniqueidentifier NOT NULL,
                    [StudentId] uniqueidentifier NOT NULL,
                    [ProposedDecision] int NOT NULL,
                    [FinalDecision] int NOT NULL,
                    [Observation] nvarchar(2000) NULL,
                    [DecidedAtUtc] datetime2 NOT NULL,
                    [DecidedByUserId] uniqueidentifier NULL,
                    [DecidedByUserName] nvarchar(150) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_DeliberationDecisions_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_DeliberationDecisions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_DeliberationDecisions_Schools_SchoolId]
                        FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_DeliberationDecisions_AcademicYears_AcademicYearId]
                        FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_DeliberationDecisions_ClassRooms_ClassRoomId]
                        FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_DeliberationDecisions_AcademicPeriods_AcademicPeriodId]
                        FOREIGN KEY ([AcademicPeriodId]) REFERENCES [AcademicPeriods] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_DeliberationDecisions_Students_StudentId]
                        FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_DeliberationDecisions_Scope_Student]
                    ON [DeliberationDecisions] ([SchoolId], [AcademicYearId], [ClassRoomId], [AcademicPeriodId], [StudentId])
                    WHERE [IsDeleted] = 0;
                CREATE INDEX [IX_DeliberationDecisions_IsDeleted] ON [DeliberationDecisions] ([IsDeleted]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliberationDecisionEvents')
            BEGIN
                CREATE TABLE [DeliberationDecisionEvents] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [DecisionId] uniqueidentifier NOT NULL,
                    [ProposedDecision] int NOT NULL,
                    [FinalDecision] int NOT NULL,
                    [Observation] nvarchar(2000) NULL,
                    [UserId] uniqueidentifier NULL,
                    [UserName] nvarchar(150) NOT NULL,
                    [OccurredAtUtc] datetime2 NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_DeliberationDecisionEvents_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_DeliberationDecisionEvents] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_DeliberationDecisionEvents_DeliberationDecisions_DecisionId]
                        FOREIGN KEY ([DecisionId]) REFERENCES [DeliberationDecisions] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_DeliberationDecisionEvents_DecisionId_OccurredAtUtc]
                    ON [DeliberationDecisionEvents] ([DecisionId], [OccurredAtUtc]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StudentRemedialSessions')
            BEGIN
                CREATE TABLE [StudentRemedialSessions] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [DecisionId] uniqueidentifier NOT NULL,
                    [StudentId] uniqueidentifier NOT NULL,
                    [AcademicYearId] uniqueidentifier NOT NULL,
                    [ClassRoomId] uniqueidentifier NOT NULL,
                    [AcademicPeriodId] uniqueidentifier NOT NULL,
                    [SessionKind] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_StudentRemedialSessions_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_StudentRemedialSessions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_StudentRemedialSessions_DeliberationDecisions_DecisionId]
                        FOREIGN KEY ([DecisionId]) REFERENCES [DeliberationDecisions] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_StudentRemedialSessions_Students_StudentId]
                        FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_StudentRemedialSessions_DecisionId]
                    ON [StudentRemedialSessions] ([DecisionId]) WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StudentRemedialCourses')
            BEGIN
                CREATE TABLE [StudentRemedialCourses] (
                    [Id] uniqueidentifier NOT NULL,
                    [RemedialSessionId] uniqueidentifier NOT NULL,
                    [CourseId] uniqueidentifier NOT NULL,
                    [CourseAssignmentId] uniqueidentifier NULL,
                    [Status] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_StudentRemedialCourses_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_StudentRemedialCourses] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_StudentRemedialCourses_StudentRemedialSessions_RemedialSessionId]
                        FOREIGN KEY ([RemedialSessionId]) REFERENCES [StudentRemedialSessions] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_StudentRemedialCourses_Courses_CourseId]
                        FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_StudentRemedialCourses_Session_Course]
                    ON [StudentRemedialCourses] ([RemedialSessionId], [CourseId]) WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CourseExemptions')
            BEGIN
                CREATE TABLE [CourseExemptions] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [DecisionId] uniqueidentifier NOT NULL,
                    [StudentId] uniqueidentifier NOT NULL,
                    [CourseId] uniqueidentifier NOT NULL,
                    [CourseAssignmentId] uniqueidentifier NULL,
                    [Motive] nvarchar(500) NOT NULL,
                    [Observation] nvarchar(2000) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_CourseExemptions_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_CourseExemptions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CourseExemptions_DeliberationDecisions_DecisionId]
                        FOREIGN KEY ([DecisionId]) REFERENCES [DeliberationDecisions] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_CourseExemptions_Students_StudentId]
                        FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_CourseExemptions_Courses_CourseId]
                        FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_CourseExemptions_Decision_Course]
                    ON [CourseExemptions] ([DecisionId], [CourseId]) WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        // Remap one-shot : anciennes valeurs FinalCouncilDecision (1..5) → catalogue v2.
        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'dbo.__DeliberationFinalDecisionV2', N'U') IS NULL
               AND OBJECT_ID(N'dbo.DeliberationDecisions', N'U') IS NOT NULL
            BEGIN
                UPDATE [DeliberationDecisions] SET [FinalDecision] = CASE [FinalDecision]
                    WHEN 1 THEN 5  -- Admis → Passe de classe
                    WHEN 2 THEN 6  -- Redouble
                    WHEN 3 THEN 8  -- Repêchage
                    WHEN 4 THEN 9  -- Exclu
                    WHEN 5 THEN 10 -- Dispensé
                    ELSE [FinalDecision] END;
                IF OBJECT_ID(N'dbo.DeliberationDecisionEvents', N'U') IS NOT NULL
                BEGIN
                    UPDATE [DeliberationDecisionEvents] SET [FinalDecision] = CASE [FinalDecision]
                        WHEN 1 THEN 5
                        WHEN 2 THEN 6
                        WHEN 3 THEN 8
                        WHEN 4 THEN 9
                        WHEN 5 THEN 10
                        ELSE [FinalDecision] END;
                END
                CREATE TABLE [dbo].[__DeliberationFinalDecisionV2] ([MigratedAtUtc] datetime2 NOT NULL);
                INSERT INTO [dbo].[__DeliberationFinalDecisionV2] ([MigratedAtUtc]) VALUES (SYSUTCDATETIME());
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma décisions de délibération vérifié.");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
