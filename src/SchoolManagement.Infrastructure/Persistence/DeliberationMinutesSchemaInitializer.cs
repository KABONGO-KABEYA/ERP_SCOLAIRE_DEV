using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class DeliberationMinutesSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DeliberationMinutesSchemaInitializer> _logger;

    public DeliberationMinutesSchemaInitializer(
        string connectionString,
        ILogger<DeliberationMinutesSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassPeriodDeliberationMinutes')
            BEGIN
                CREATE TABLE [ClassPeriodDeliberationMinutes] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [AcademicYearId] uniqueidentifier NOT NULL,
                    [ClassRoomId] uniqueidentifier NOT NULL,
                    [AcademicPeriodId] uniqueidentifier NOT NULL,
                    [GeneralObservations] nvarchar(4000) NULL,
                    [CouncilDecisions] nvarchar(4000) NULL,
                    [PedagogicalRecommendations] nvarchar(4000) NULL,
                    [RecordedAtUtc] datetime2 NOT NULL,
                    [RecordedByUserId] uniqueidentifier NULL,
                    [RecordedByUserName] nvarchar(150) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_ClassPeriodDeliberationMinutes_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_ClassPeriodDeliberationMinutes] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ClassPeriodDeliberationMinutes_Schools_SchoolId]
                        FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_ClassPeriodDeliberationMinutes_AcademicYears_AcademicYearId]
                        FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_ClassPeriodDeliberationMinutes_ClassRooms_ClassRoomId]
                        FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_ClassPeriodDeliberationMinutes_AcademicPeriods_AcademicPeriodId]
                        FOREIGN KEY ([AcademicPeriodId]) REFERENCES [AcademicPeriods] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_ClassPeriodDeliberationMinutes_SchoolId_AcademicYearId_ClassRoomId_AcademicPeriodId]
                    ON [ClassPeriodDeliberationMinutes] ([SchoolId], [AcademicYearId], [ClassRoomId], [AcademicPeriodId])
                    WHERE [IsDeleted] = 0;
                CREATE INDEX [IX_ClassPeriodDeliberationMinutes_IsDeleted]
                    ON [ClassPeriodDeliberationMinutes] ([IsDeleted]);
                CREATE INDEX [IX_ClassPeriodDeliberationMinutes_AcademicPeriodId]
                    ON [ClassPeriodDeliberationMinutes] ([AcademicPeriodId]);
                CREATE INDEX [IX_ClassPeriodDeliberationMinutes_AcademicYearId]
                    ON [ClassPeriodDeliberationMinutes] ([AcademicYearId]);
                CREATE INDEX [IX_ClassPeriodDeliberationMinutes_ClassRoomId]
                    ON [ClassPeriodDeliberationMinutes] ([ClassRoomId]);
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma Procès-verbal de délibération vérifié.");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
