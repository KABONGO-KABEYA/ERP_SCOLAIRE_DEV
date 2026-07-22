using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Crée les tables SyncOutbox*, SyncJournal, SyncWatermark (sync local → cloud).</summary>
public sealed class CloudSyncSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<CloudSyncSchemaInitializer> _logger;

    public CloudSyncSchemaInitializer(string connectionString, ILogger<CloudSyncSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var script in Scripts)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = script;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Schéma sync cloud vérifié (SyncOutboxUnit, SyncOutboxItem, SyncJournal, SyncWatermark).");
    }

    private static readonly string[] Scripts =
    [
        """
        IF OBJECT_ID(N'SyncOutboxUnit', N'U') IS NULL
        BEGIN
            CREATE TABLE [SyncOutboxUnit] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NULL,
                [AggregateType] nvarchar(80) NOT NULL,
                [AggregateId] uniqueidentifier NULL,
                [Priority] int NOT NULL,
                [Status] int NOT NULL,
                [AttemptCount] int NOT NULL CONSTRAINT [DF_SyncOutboxUnit_AttemptCount] DEFAULT(0),
                [LastAttemptAt] datetime2 NULL,
                [LastError] nvarchar(2000) NULL,
                [CompletedAt] datetime2 NULL,
                [ExpectedItemCount] int NOT NULL CONSTRAINT [DF_SyncOutboxUnit_Expected] DEFAULT(0),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_SyncOutboxUnit_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_SyncOutboxUnit] PRIMARY KEY ([Id])
            );
            CREATE INDEX [IX_SyncOutboxUnit_Status_Priority_Created]
                ON [SyncOutboxUnit] ([Status], [Priority], [CreatedAt]) WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_SyncOutboxUnit_Aggregate]
                ON [SyncOutboxUnit] ([AggregateType], [AggregateId]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'SyncOutboxItem', N'U') IS NULL
        BEGIN
            CREATE TABLE [SyncOutboxItem] (
                [Id] uniqueidentifier NOT NULL,
                [UnitId] uniqueidentifier NOT NULL,
                [TableName] nvarchar(128) NOT NULL,
                [EntityId] uniqueidentifier NOT NULL,
                [Operation] int NOT NULL,
                [Status] int NOT NULL,
                [Sequence] int NOT NULL CONSTRAINT [DF_SyncOutboxItem_Sequence] DEFAULT(0),
                [LastError] nvarchar(2000) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_SyncOutboxItem_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_SyncOutboxItem] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_SyncOutboxItem_Unit] FOREIGN KEY ([UnitId])
                    REFERENCES [SyncOutboxUnit] ([Id]) ON DELETE CASCADE
            );
            CREATE INDEX [IX_SyncOutboxItem_Unit_Sequence]
                ON [SyncOutboxItem] ([UnitId], [Sequence]);
            CREATE INDEX [IX_SyncOutboxItem_Table_Entity_Status]
                ON [SyncOutboxItem] ([TableName], [EntityId], [Status]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'SyncJournal', N'U') IS NULL
        BEGIN
            CREATE TABLE [SyncJournal] (
                [Id] uniqueidentifier NOT NULL,
                [StartedAt] datetime2 NOT NULL,
                [EndedAt] datetime2 NULL,
                [DurationMs] int NOT NULL CONSTRAINT [DF_SyncJournal_Duration] DEFAULT(0),
                [Success] bit NOT NULL,
                [Skipped] bit NOT NULL CONSTRAINT [DF_SyncJournal_Skipped] DEFAULT(0),
                [UnitsAttempted] int NOT NULL CONSTRAINT [DF_SyncJournal_UA] DEFAULT(0),
                [UnitsSucceeded] int NOT NULL CONSTRAINT [DF_SyncJournal_US] DEFAULT(0),
                [UnitsFailed] int NOT NULL CONSTRAINT [DF_SyncJournal_UF] DEFAULT(0),
                [RecordsSent] int NOT NULL CONSTRAINT [DF_SyncJournal_RS] DEFAULT(0),
                [RecordsSucceeded] int NOT NULL CONSTRAINT [DF_SyncJournal_ROK] DEFAULT(0),
                [RecordsFailed] int NOT NULL CONSTRAINT [DF_SyncJournal_RFail] DEFAULT(0),
                [TablesTouched] nvarchar(2000) NULL,
                [ErrorSummary] nvarchar(4000) NULL,
                [DetailJson] nvarchar(max) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_SyncJournal_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_SyncJournal] PRIMARY KEY ([Id])
            );
            CREATE INDEX [IX_SyncJournal_StartedAt] ON [SyncJournal] ([StartedAt]);
        END
        """,
        """
        IF OBJECT_ID(N'SyncWatermark', N'U') IS NULL
        BEGIN
            CREATE TABLE [SyncWatermark] (
                [Id] uniqueidentifier NOT NULL,
                [TableName] nvarchar(128) NOT NULL,
                [LastSyncedAt] datetime2 NOT NULL,
                [LastSyncedEntityId] uniqueidentifier NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_SyncWatermark_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_SyncWatermark] PRIMARY KEY ([Id])
            );
            CREATE UNIQUE INDEX [IX_SyncWatermark_TableName]
                ON [SyncWatermark] ([TableName]) WHERE [IsDeleted] = 0;
        END
        """
    ];
}
