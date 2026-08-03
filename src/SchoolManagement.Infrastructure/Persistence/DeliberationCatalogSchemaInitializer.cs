using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Schéma conseil de classe : mentions, conduites, bonus pédagogiques, audit, IsRemedial.
/// </summary>
public sealed class DeliberationCatalogSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DeliberationCatalogSchemaInitializer> _logger;

    public DeliberationCatalogSchemaInitializer(
        string connectionString,
        ILogger<DeliberationCatalogSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF COL_LENGTH('AcademicPeriods', 'IsRemedial') IS NULL
                ALTER TABLE [AcademicPeriods] ADD [IsRemedial] bit NOT NULL
                    CONSTRAINT [DF_AcademicPeriods_IsRemedial] DEFAULT 0;
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ResultMentionDefinitions')
            BEGIN
                CREATE TABLE [ResultMentionDefinitions] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [Label] nvarchar(100) NOT NULL,
                    [MinPercentageInclusive] decimal(9,2) NOT NULL,
                    [MaxPercentageInclusive] decimal(9,2) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL CONSTRAINT [DF_ResultMentionDefinitions_IsActive] DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_ResultMentionDefinitions_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_ResultMentionDefinitions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ResultMentionDefinitions_Schools]
                        FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_ResultMentionDefinitions_School_Label]
                    ON [ResultMentionDefinitions] ([SchoolId], [Label]) WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ConductDefinitions')
            BEGIN
                CREATE TABLE [ConductDefinitions] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [Label] nvarchar(100) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL CONSTRAINT [DF_ConductDefinitions_IsActive] DEFAULT 1,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_ConductDefinitions_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_ConductDefinitions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ConductDefinitions_Schools]
                        FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_ConductDefinitions_School_Label]
                    ON [ConductDefinitions] ([SchoolId], [Label]) WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StudentPeriodConducts')
            BEGIN
                CREATE TABLE [StudentPeriodConducts] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [AcademicYearId] uniqueidentifier NOT NULL,
                    [ClassRoomId] uniqueidentifier NOT NULL,
                    [AcademicPeriodId] uniqueidentifier NOT NULL,
                    [StudentId] uniqueidentifier NOT NULL,
                    [ConductDefinitionId] uniqueidentifier NOT NULL,
                    [Observation] nvarchar(1000) NULL,
                    [RecordedByUserId] uniqueidentifier NULL,
                    [RecordedByUserName] nvarchar(150) NOT NULL,
                    [RecordedAtUtc] datetime2 NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_StudentPeriodConducts_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_StudentPeriodConducts] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_StudentPeriodConducts_Schools]
                        FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_StudentPeriodConducts_Years]
                        FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_StudentPeriodConducts_ClassRooms]
                        FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_StudentPeriodConducts_Periods]
                        FOREIGN KEY ([AcademicPeriodId]) REFERENCES [AcademicPeriods] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_StudentPeriodConducts_Students]
                        FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_StudentPeriodConducts_Conduct]
                        FOREIGN KEY ([ConductDefinitionId]) REFERENCES [ConductDefinitions] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_StudentPeriodConducts_Scope]
                    ON [StudentPeriodConducts] ([SchoolId], [AcademicYearId], [ClassRoomId], [AcademicPeriodId], [StudentId])
                    WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PedagogicalBonusPoints')
            BEGIN
                CREATE TABLE [PedagogicalBonusPoints] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [AcademicYearId] uniqueidentifier NOT NULL,
                    [ClassRoomId] uniqueidentifier NOT NULL,
                    [AcademicPeriodId] uniqueidentifier NOT NULL,
                    [StudentId] uniqueidentifier NOT NULL,
                    [CourseId] uniqueidentifier NOT NULL,
                    [CourseAssignmentId] uniqueidentifier NULL,
                    [PointsAdded] decimal(9,2) NOT NULL,
                    [Motive] nvarchar(500) NOT NULL,
                    [RecordedByUserId] uniqueidentifier NULL,
                    [RecordedByUserName] nvarchar(150) NOT NULL,
                    [RecordedAtUtc] datetime2 NOT NULL,
                    [IsCancelled] bit NOT NULL CONSTRAINT [DF_PedagogicalBonusPoints_IsCancelled] DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_PedagogicalBonusPoints_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_PedagogicalBonusPoints] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PedagogicalBonusPoints_Schools]
                        FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PedagogicalBonusPoints_Years]
                        FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PedagogicalBonusPoints_ClassRooms]
                        FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PedagogicalBonusPoints_Periods]
                        FOREIGN KEY ([AcademicPeriodId]) REFERENCES [AcademicPeriods] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PedagogicalBonusPoints_Students]
                        FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PedagogicalBonusPoints_Courses]
                        FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION
                );
                CREATE INDEX [IX_PedagogicalBonusPoints_Scope]
                    ON [PedagogicalBonusPoints] ([SchoolId], [ClassRoomId], [AcademicPeriodId], [StudentId], [IsCancelled]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliberationAuditEntries')
            BEGIN
                CREATE TABLE [DeliberationAuditEntries] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [AcademicYearId] uniqueidentifier NOT NULL,
                    [ClassRoomId] uniqueidentifier NOT NULL,
                    [AcademicPeriodId] uniqueidentifier NOT NULL,
                    [StudentId] uniqueidentifier NULL,
                    [ActionCode] nvarchar(50) NOT NULL,
                    [Summary] nvarchar(500) NOT NULL,
                    [Observation] nvarchar(2000) NULL,
                    [UserId] uniqueidentifier NULL,
                    [UserName] nvarchar(150) NOT NULL,
                    [OccurredAtUtc] datetime2 NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_DeliberationAuditEntries_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_DeliberationAuditEntries] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_DeliberationAuditEntries_Scope]
                    ON [DeliberationAuditEntries] ([SchoolId], [ClassRoomId], [AcademicPeriodId], [OccurredAtUtc]);
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma délibération (mentions, conduite, bonus, audit, IsRemedial) vérifié.");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
