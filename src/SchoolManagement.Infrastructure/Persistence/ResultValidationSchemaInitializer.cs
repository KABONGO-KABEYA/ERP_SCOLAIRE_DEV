using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class ResultValidationSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<ResultValidationSchemaInitializer> _logger;

    public ResultValidationSchemaInitializer(
        string connectionString,
        ILogger<ResultValidationSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassPeriodResultValidations')
            BEGIN
                CREATE TABLE [ClassPeriodResultValidations] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [AcademicYearId] uniqueidentifier NOT NULL,
                    [ClassRoomId] uniqueidentifier NOT NULL,
                    [AcademicPeriodId] uniqueidentifier NOT NULL,
                    [Status] int NOT NULL,
                    [ValidatedAtUtc] datetime2 NULL,
                    [ValidatedByUserId] uniqueidentifier NULL,
                    [ValidatedByUserName] nvarchar(150) NULL,
                    [LockedAtUtc] datetime2 NULL,
                    [LockedByUserId] uniqueidentifier NULL,
                    [LockedByUserName] nvarchar(150) NULL,
                    [Observations] nvarchar(1000) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_ClassPeriodResultValidations_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_ClassPeriodResultValidations] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ClassPeriodResultValidations_Schools_SchoolId]
                        FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_ClassPeriodResultValidations_AcademicYears_AcademicYearId]
                        FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_ClassPeriodResultValidations_ClassRooms_ClassRoomId]
                        FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_ClassPeriodResultValidations_AcademicPeriods_AcademicPeriodId]
                        FOREIGN KEY ([AcademicPeriodId]) REFERENCES [AcademicPeriods] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_ClassPeriodResultValidations_SchoolId_AcademicYearId_ClassRoomId_AcademicPeriodId]
                    ON [ClassPeriodResultValidations] ([SchoolId], [AcademicYearId], [ClassRoomId], [AcademicPeriodId])
                    WHERE [IsDeleted] = 0;
                CREATE INDEX [IX_ClassPeriodResultValidations_IsDeleted]
                    ON [ClassPeriodResultValidations] ([IsDeleted]);
                CREATE INDEX [IX_ClassPeriodResultValidations_AcademicPeriodId]
                    ON [ClassPeriodResultValidations] ([AcademicPeriodId]);
                CREATE INDEX [IX_ClassPeriodResultValidations_AcademicYearId]
                    ON [ClassPeriodResultValidations] ([AcademicYearId]);
                CREATE INDEX [IX_ClassPeriodResultValidations_ClassRoomId]
                    ON [ClassPeriodResultValidations] ([ClassRoomId]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClassPeriodResultValidationEvents')
            BEGIN
                CREATE TABLE [ClassPeriodResultValidationEvents] (
                    [Id] uniqueidentifier NOT NULL,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [ValidationId] uniqueidentifier NOT NULL,
                    [Operation] int NOT NULL,
                    [UserId] uniqueidentifier NULL,
                    [UserName] nvarchar(150) NOT NULL,
                    [OccurredAtUtc] datetime2 NOT NULL,
                    [Observations] nvarchar(1000) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_ClassPeriodResultValidationEvents_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [PK_ClassPeriodResultValidationEvents] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ClassPeriodResultValidationEvents_ClassPeriodResultValidations_ValidationId]
                        FOREIGN KEY ([ValidationId]) REFERENCES [ClassPeriodResultValidations] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_ClassPeriodResultValidationEvents_IsDeleted]
                    ON [ClassPeriodResultValidationEvents] ([IsDeleted]);
                CREATE INDEX [IX_ClassPeriodResultValidationEvents_ValidationId_OccurredAtUtc]
                    ON [ClassPeriodResultValidationEvents] ([ValidationId], [OccurredAtUtc]);
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma Validation des résultats vérifié.");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
