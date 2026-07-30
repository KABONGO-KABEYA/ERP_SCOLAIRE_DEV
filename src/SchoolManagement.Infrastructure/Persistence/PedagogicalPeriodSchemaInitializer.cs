using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Schéma du moteur de périodes pédagogiques (MainPeriod + enrichissement AcademicPeriods).
/// </summary>
public sealed class PedagogicalPeriodSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<PedagogicalPeriodSchemaInitializer> _logger;

    public PedagogicalPeriodSchemaInitializer(
        string connectionString,
        ILogger<PedagogicalPeriodSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AcademicMainPeriods')
            BEGIN
                CREATE TABLE [AcademicMainPeriods] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [SchoolId] uniqueidentifier NOT NULL,
                    [AcademicYearId] uniqueidentifier NOT NULL,
                    [CycleGroup] int NOT NULL,
                    [Name] nvarchar(80) NOT NULL,
                    [PeriodType] int NOT NULL,
                    [OrderIndex] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] uniqueidentifier NULL,
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] uniqueidentifier NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_AcademicMainPeriods_IsDeleted] DEFAULT 0,
                    [DeletedAt] datetime2 NULL,
                    [DeletedBy] uniqueidentifier NULL,
                    CONSTRAINT [FK_AcademicMainPeriods_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools]([Id]),
                    CONSTRAINT [FK_AcademicMainPeriods_AcademicYears] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears]([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_AcademicMainPeriods_Year_Cycle_Order]
                    ON [AcademicMainPeriods] ([AcademicYearId], [CycleGroup], [OrderIndex])
                    WHERE [IsDeleted] = 0;
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF COL_LENGTH('AcademicPeriods', 'MainPeriodId') IS NULL
                ALTER TABLE [AcademicPeriods] ADD [MainPeriodId] uniqueidentifier NULL;
            IF COL_LENGTH('AcademicPeriods', 'Kind') IS NULL
                ALTER TABLE [AcademicPeriods] ADD [Kind] int NOT NULL CONSTRAINT [DF_AcademicPeriods_Kind] DEFAULT 1;
            IF COL_LENGTH('AcademicPeriods', 'Status') IS NULL
                ALTER TABLE [AcademicPeriods] ADD [Status] int NOT NULL CONSTRAINT [DF_AcademicPeriods_Status] DEFAULT 1;
            IF COL_LENGTH('AcademicPeriods', 'MaxScore') IS NULL
                ALTER TABLE [AcademicPeriods] ADD [MaxScore] int NOT NULL CONSTRAINT [DF_AcademicPeriods_MaxScore] DEFAULT 20;
            IF COL_LENGTH('AcademicPeriods', 'MaxEvaluationCount') IS NULL
                ALTER TABLE [AcademicPeriods] ADD [MaxEvaluationCount] int NULL;
            IF COL_LENGTH('AcademicPeriods', 'OpenedAt') IS NULL
                ALTER TABLE [AcademicPeriods] ADD [OpenedAt] datetime2 NULL;
            IF COL_LENGTH('AcademicPeriods', 'PlannedCloseDate') IS NULL
                ALTER TABLE [AcademicPeriods] ADD [PlannedCloseDate] date NULL;
            IF COL_LENGTH('AcademicPeriods', 'ClosedAt') IS NULL
                ALTER TABLE [AcademicPeriods] ADD [ClosedAt] datetime2 NULL;

            -- Dates nullables : renseignées uniquement par l'administrateur (étape 2 du calendrier).
            IF EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('AcademicPeriods') AND name = 'StartDate' AND is_nullable = 0)
                ALTER TABLE [AcademicPeriods] ALTER COLUMN [StartDate] date NULL;
            IF EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID('AcademicPeriods') AND name = 'EndDate' AND is_nullable = 0)
                ALTER TABLE [AcademicPeriods] ALTER COLUMN [EndDate] date NULL;
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AcademicPeriods_AcademicMainPeriods')
            BEGIN
                ALTER TABLE [AcademicPeriods] WITH CHECK
                ADD CONSTRAINT [FK_AcademicPeriods_AcademicMainPeriods]
                    FOREIGN KEY ([MainPeriodId]) REFERENCES [AcademicMainPeriods]([Id]);
            END
            """, cancellationToken);

        await ExecuteAsync(connection, """
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AcademicPeriods_AcademicYearId_OrderIndex' AND object_id = OBJECT_ID('AcademicPeriods'))
                DROP INDEX [IX_AcademicPeriods_AcademicYearId_OrderIndex] ON [AcademicPeriods];

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AcademicPeriods_Year_Main_Order' AND object_id = OBJECT_ID('AcademicPeriods'))
                CREATE INDEX [IX_AcademicPeriods_Year_Main_Order]
                    ON [AcademicPeriods] ([AcademicYearId], [MainPeriodId], [OrderIndex]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AcademicPeriods_Year_Status' AND object_id = OBJECT_ID('AcademicPeriods'))
                CREATE INDEX [IX_AcademicPeriods_Year_Status]
                    ON [AcademicPeriods] ([AcademicYearId], [Status]);
            """, cancellationToken);

        // Aligner Status legacy depuis IsClosed.
        await ExecuteAsync(connection, """
            UPDATE [AcademicPeriods]
            SET [Status] = CASE WHEN [IsClosed] = 1 THEN 3 ELSE [Status] END
            WHERE [MainPeriodId] IS NULL;
            """, cancellationToken);

        // Réinit. one-shot : dates null sur les périodes « À venir » (saisie uniquement à l'ouverture).
        await ExecuteAsync(connection, """
            IF OBJECT_ID(N'dbo.__PedagogicalDatesNullReset_v1', N'U') IS NULL
            BEGIN
                UPDATE [AcademicPeriods]
                SET [StartDate] = NULL,
                    [EndDate] = NULL,
                    [PlannedCloseDate] = NULL
                WHERE [MainPeriodId] IS NOT NULL
                  AND [Status] = 1; -- À venir

                CREATE TABLE [dbo].[__PedagogicalDatesNullReset_v1] (
                    [DoneAt] datetime2 NOT NULL
                );
                INSERT INTO [dbo].[__PedagogicalDatesNullReset_v1] ([DoneAt]) VALUES (SYSUTCDATETIME());
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma périodes pédagogiques à jour.");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
