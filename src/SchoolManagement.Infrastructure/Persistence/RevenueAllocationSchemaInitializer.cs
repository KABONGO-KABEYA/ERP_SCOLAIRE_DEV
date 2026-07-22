using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Initialise les tables de répartition des recettes (FIN_*).</summary>
public sealed class RevenueAllocationSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<RevenueAllocationSchemaInitializer> _logger;

    public RevenueAllocationSchemaInitializer(string connectionString, ILogger<RevenueAllocationSchemaInitializer> logger)
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
            "Schéma répartition recettes vérifié (FinDestinationRepartition, FinCleRepartition, FinCleRepartitionDetail, FinRepartitionRecette).");
    }

    private static readonly string[] Scripts =
    [
        // Recrée les tables si une version erronée (CreatedBy nvarchar) a été déployée.
        """
        IF OBJECT_ID(N'FinDestinationRepartition', N'U') IS NOT NULL
           AND EXISTS (
               SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'FinDestinationRepartition')
                 AND name = N'CreatedBy'
                 AND system_type_id = 231)
        BEGIN
            IF OBJECT_ID(N'FinRepartitionRecette', N'U') IS NOT NULL DROP TABLE [FinRepartitionRecette];
            IF OBJECT_ID(N'FinCleRepartitionDetail', N'U') IS NOT NULL DROP TABLE [FinCleRepartitionDetail];
            IF OBJECT_ID(N'FinCleRepartition', N'U') IS NOT NULL DROP TABLE [FinCleRepartition];
            DROP TABLE [FinDestinationRepartition];
        END
        """,
        """
        IF OBJECT_ID(N'FinDestinationRepartition', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinDestinationRepartition] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [Code] nvarchar(20) NOT NULL,
                [Name] nvarchar(120) NOT NULL,
                [Description] nvarchar(500) NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_FinDestinationRepartition_IsActive] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinDestinationRepartition_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinDestinationRepartition] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinDestinationRepartition_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinDestinationRepartition_SchoolId_Code]
                ON [FinDestinationRepartition] ([SchoolId], [Code]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'FinCleRepartition', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinCleRepartition] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [AcademicYearId] uniqueidentifier NOT NULL,
                [FeeTypeId] uniqueidentifier NULL,
                [WithholdingTypeId] uniqueidentifier NULL,
                [Name] nvarchar(150) NOT NULL,
                [Notes] nvarchar(500) NULL,
                [StartDate] date NOT NULL,
                [EndDate] date NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_FinCleRepartition_IsActive] DEFAULT(0),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinCleRepartition_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinCleRepartition] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinCleRepartition_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                CONSTRAINT [FK_FinCleRepartition_AcademicYears] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
                CONSTRAINT [FK_FinCleRepartition_FeeTypes] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeTypes] ([Id])
            );
            IF OBJECT_ID(N'FinRetenue', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FinCleRepartition_Retenue')
                EXEC(N'ALTER TABLE [FinCleRepartition] WITH CHECK ADD CONSTRAINT [FK_FinCleRepartition_Retenue] FOREIGN KEY ([WithholdingTypeId]) REFERENCES [FinRetenue] ([Id])');
            CREATE UNIQUE INDEX [IX_FinCleRepartition_School_Year_Fee]
                ON [FinCleRepartition] ([SchoolId], [AcademicYearId], [FeeTypeId])
                WHERE [IsDeleted] = 0 AND [FeeTypeId] IS NOT NULL;
            EXEC(N'CREATE UNIQUE INDEX [IX_FinCleRepartition_School_Year_Retenue] ON [FinCleRepartition] ([SchoolId], [AcademicYearId], [WithholdingTypeId]) WHERE [IsDeleted] = 0 AND [WithholdingTypeId] IS NOT NULL');
            CREATE INDEX [IX_FinCleRepartition_School_Fee_Start]
                ON [FinCleRepartition] ([SchoolId], [FeeTypeId], [StartDate]);
            EXEC(N'CREATE INDEX [IX_FinCleRepartition_School_Retenue_Start] ON [FinCleRepartition] ([SchoolId], [WithholdingTypeId], [StartDate])');
        END
        """,
        """
        IF OBJECT_ID(N'FinCleRepartition', N'U') IS NOT NULL
           AND COL_LENGTH(N'FinCleRepartition', N'FeeTypeId') IS NULL
        BEGIN
            ALTER TABLE [FinCleRepartition] ADD [FeeTypeId] uniqueidentifier NULL;
            ALTER TABLE [FinCleRepartition] ADD [StartDate] date NULL;
            ALTER TABLE [FinCleRepartition] ADD [EndDate] date NULL;
        END
        """,
        """
        -- Ancienne migration (FeeTypeId obligatoire) : ne s'applique plus dès que WithholdingTypeId existe.
        IF OBJECT_ID(N'FinCleRepartition', N'U') IS NOT NULL
           AND COL_LENGTH(N'FinCleRepartition', N'FeeTypeId') IS NOT NULL
           AND COL_LENGTH(N'FinCleRepartition', N'WithholdingTypeId') IS NULL
           AND EXISTS (
               SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'FinCleRepartition')
                 AND name = N'FeeTypeId'
                 AND is_nullable = 1)
        BEGIN
            UPDATE k
            SET k.[FeeTypeId] = ft.[Id],
                k.[StartDate] = CAST(k.[CreatedAt] AS date)
            FROM [FinCleRepartition] k
            OUTER APPLY (
                SELECT TOP (1) f.[Id]
                FROM [FeeTypes] f
                WHERE f.[SchoolId] = k.[SchoolId] AND f.[IsDeleted] = 0
                ORDER BY f.[IsActive] DESC, f.[Name]
            ) ft
            WHERE k.[FeeTypeId] IS NULL AND ft.[Id] IS NOT NULL;

            UPDATE [FinCleRepartition]
            SET [StartDate] = CAST([CreatedAt] AS date)
            WHERE [StartDate] IS NULL AND [FeeTypeId] IS NOT NULL;

            DELETE d
            FROM [FinCleRepartitionDetail] d
            INNER JOIN [FinCleRepartition] k ON k.[Id] = d.[AllocationKeyId]
            WHERE k.[FeeTypeId] IS NULL;

            DELETE e
            FROM [FinRepartitionRecette] e
            INNER JOIN [FinCleRepartition] k ON k.[Id] = e.[AllocationKeyId]
            WHERE k.[FeeTypeId] IS NULL;

            DELETE FROM [FinCleRepartition] WHERE [FeeTypeId] IS NULL;

            ALTER TABLE [FinCleRepartition] ALTER COLUMN [FeeTypeId] uniqueidentifier NOT NULL;
            ALTER TABLE [FinCleRepartition] ALTER COLUMN [StartDate] date NOT NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FinCleRepartition_FeeTypes')
                ALTER TABLE [FinCleRepartition] WITH CHECK
                ADD CONSTRAINT [FK_FinCleRepartition_FeeTypes] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeTypes] ([Id]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FinCleRepartition_School_Year_Fee' AND object_id = OBJECT_ID(N'FinCleRepartition'))
                CREATE UNIQUE INDEX [IX_FinCleRepartition_School_Year_Fee]
                    ON [FinCleRepartition] ([SchoolId], [AcademicYearId], [FeeTypeId]) WHERE [IsDeleted] = 0;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FinCleRepartition_School_Fee_Start' AND object_id = OBJECT_ID(N'FinCleRepartition'))
                CREATE INDEX [IX_FinCleRepartition_School_Fee_Start]
                    ON [FinCleRepartition] ([SchoolId], [FeeTypeId], [StartDate]);
        END
        """,
        """
        IF OBJECT_ID(N'FinCleRepartition', N'U') IS NOT NULL
           AND COL_LENGTH(N'FinCleRepartition', N'FeeTypeId') IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM sys.indexes
               WHERE name = N'IX_FinCleRepartition_School_Year_Fee'
                 AND object_id = OBJECT_ID(N'FinCleRepartition'))
        BEGIN
            -- Conservez une seule clé (année + type de frais) avant création de l'index unique.
            ;WITH Duplicates AS (
                SELECT [Id],
                       ROW_NUMBER() OVER (
                           PARTITION BY [SchoolId], [AcademicYearId], [FeeTypeId]
                           ORDER BY CASE WHEN [EndDate] IS NULL THEN 0 ELSE 1 END,
                                    [StartDate] DESC,
                                    [CreatedAt] DESC) AS rn
                FROM [FinCleRepartition]
                WHERE [IsDeleted] = 0
            )
            UPDATE k
            SET k.[IsDeleted] = 1,
                k.[DeletedAt] = SYSUTCDATETIME()
            FROM [FinCleRepartition] k
            INNER JOIN Duplicates d ON d.[Id] = k.[Id]
            WHERE d.rn > 1;

            CREATE UNIQUE INDEX [IX_FinCleRepartition_School_Year_Fee]
                ON [FinCleRepartition] ([SchoolId], [AcademicYearId], [FeeTypeId]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'FinCleRepartitionDetail', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinCleRepartitionDetail] (
                [Id] uniqueidentifier NOT NULL,
                [AllocationKeyId] uniqueidentifier NOT NULL,
                [DestinationId] uniqueidentifier NOT NULL,
                [CalculationType] int NOT NULL,
                [Value] decimal(18,4) NOT NULL,
                [SortOrder] int NOT NULL CONSTRAINT [DF_FinCleRepartitionDetail_SortOrder] DEFAULT(0),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinCleRepartitionDetail_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinCleRepartitionDetail] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinCleRepartitionDetail_Cle] FOREIGN KEY ([AllocationKeyId]) REFERENCES [FinCleRepartition] ([Id]),
                CONSTRAINT [FK_FinCleRepartitionDetail_Destination] FOREIGN KEY ([DestinationId]) REFERENCES [FinDestinationRepartition] ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinCleRepartitionDetail_Key_Destination]
                ON [FinCleRepartitionDetail] ([AllocationKeyId], [DestinationId]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'FinRepartitionRecette', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinRepartitionRecette] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [PaymentId] uniqueidentifier NOT NULL,
                [AllocationKeyId] uniqueidentifier NULL,
                [DestinationId] uniqueidentifier NOT NULL,
                [FeeTypeId] uniqueidentifier NULL,
                [WithholdingTypeId] uniqueidentifier NULL,
                [AcademicYearId] uniqueidentifier NOT NULL,
                [Amount] decimal(18,2) NOT NULL,
                [AppliedPercentage] decimal(18,4) NULL,
                [CalculationType] int NOT NULL,
                [AllocatedAt] datetime2 NOT NULL,
                [AllocatedByUserId] uniqueidentifier NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinRepartitionRecette_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinRepartitionRecette] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinRepartitionRecette_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                CONSTRAINT [FK_FinRepartitionRecette_Payments] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]),
                CONSTRAINT [FK_FinRepartitionRecette_Cle] FOREIGN KEY ([AllocationKeyId]) REFERENCES [FinCleRepartition] ([Id]),
                CONSTRAINT [FK_FinRepartitionRecette_Destination] FOREIGN KEY ([DestinationId]) REFERENCES [FinDestinationRepartition] ([Id]),
                CONSTRAINT [FK_FinRepartitionRecette_FeeTypes] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeTypes] ([Id]),
                CONSTRAINT [FK_FinRepartitionRecette_Years] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id])
            );
            IF OBJECT_ID(N'FinRetenue', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FinRepartitionRecette_Retenue')
                EXEC(N'ALTER TABLE [FinRepartitionRecette] WITH CHECK ADD CONSTRAINT [FK_FinRepartitionRecette_Retenue] FOREIGN KEY ([WithholdingTypeId]) REFERENCES [FinRetenue] ([Id])');
            CREATE INDEX [IX_FinRepartitionRecette_Payment] ON [FinRepartitionRecette] ([PaymentId]);
            CREATE INDEX [IX_FinRepartitionRecette_School_Date] ON [FinRepartitionRecette] ([SchoolId], [AllocatedAt]);
            CREATE INDEX [IX_FinRepartitionRecette_Dest_Year] ON [FinRepartitionRecette] ([SchoolId], [DestinationId], [AcademicYearId]);
        END
        """,
        // Compte principal + retenues : FeeTypeId nullable, WithholdingTypeId, AllocationKeyId nullable
        // (index/FK sur nouvelle colonne en EXEC pour éviter erreur de compilation de lot SQL Server)
        """
        IF OBJECT_ID(N'FinCleRepartition', N'U') IS NOT NULL
           AND COL_LENGTH(N'FinCleRepartition', N'WithholdingTypeId') IS NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FinCleRepartition_School_Year_Fee' AND object_id = OBJECT_ID(N'FinCleRepartition'))
                DROP INDEX [IX_FinCleRepartition_School_Year_Fee] ON [FinCleRepartition];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FinCleRepartition_School_Year_Fee_Active' AND object_id = OBJECT_ID(N'FinCleRepartition'))
                DROP INDEX [IX_FinCleRepartition_School_Year_Fee_Active] ON [FinCleRepartition];
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FinCleRepartition_School_Fee_Start' AND object_id = OBJECT_ID(N'FinCleRepartition'))
                DROP INDEX [IX_FinCleRepartition_School_Fee_Start] ON [FinCleRepartition];
        END
        """,
        """
        IF OBJECT_ID(N'FinCleRepartition', N'U') IS NOT NULL
           AND COL_LENGTH(N'FinCleRepartition', N'WithholdingTypeId') IS NULL
        BEGIN
            ALTER TABLE [FinCleRepartition] ALTER COLUMN [FeeTypeId] uniqueidentifier NULL;
            ALTER TABLE [FinCleRepartition] ADD [WithholdingTypeId] uniqueidentifier NULL;
        END
        """,
        """
        IF OBJECT_ID(N'FinCleRepartition', N'U') IS NOT NULL
           AND COL_LENGTH(N'FinCleRepartition', N'WithholdingTypeId') IS NOT NULL
        BEGIN
            IF OBJECT_ID(N'FinRetenue', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FinCleRepartition_Retenue')
                EXEC(N'ALTER TABLE [FinCleRepartition] WITH CHECK ADD CONSTRAINT [FK_FinCleRepartition_Retenue] FOREIGN KEY ([WithholdingTypeId]) REFERENCES [FinRetenue] ([Id])');

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FinCleRepartition_School_Year_Fee' AND object_id = OBJECT_ID(N'FinCleRepartition'))
                CREATE UNIQUE INDEX [IX_FinCleRepartition_School_Year_Fee]
                    ON [FinCleRepartition] ([SchoolId], [AcademicYearId], [FeeTypeId])
                    WHERE [IsDeleted] = 0 AND [FeeTypeId] IS NOT NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FinCleRepartition_School_Fee_Start' AND object_id = OBJECT_ID(N'FinCleRepartition'))
                CREATE INDEX [IX_FinCleRepartition_School_Fee_Start]
                    ON [FinCleRepartition] ([SchoolId], [FeeTypeId], [StartDate]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FinCleRepartition_School_Year_Retenue' AND object_id = OBJECT_ID(N'FinCleRepartition'))
                EXEC(N'CREATE UNIQUE INDEX [IX_FinCleRepartition_School_Year_Retenue] ON [FinCleRepartition] ([SchoolId], [AcademicYearId], [WithholdingTypeId]) WHERE [IsDeleted] = 0 AND [WithholdingTypeId] IS NOT NULL');

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FinCleRepartition_School_Retenue_Start' AND object_id = OBJECT_ID(N'FinCleRepartition'))
                EXEC(N'CREATE INDEX [IX_FinCleRepartition_School_Retenue_Start] ON [FinCleRepartition] ([SchoolId], [WithholdingTypeId], [StartDate])');
        END
        """,
        """
        IF OBJECT_ID(N'FinRepartitionRecette', N'U') IS NOT NULL
           AND COL_LENGTH(N'FinRepartitionRecette', N'WithholdingTypeId') IS NULL
        BEGIN
            ALTER TABLE [FinRepartitionRecette] ALTER COLUMN [AllocationKeyId] uniqueidentifier NULL;
            ALTER TABLE [FinRepartitionRecette] ADD [WithholdingTypeId] uniqueidentifier NULL;
        END
        """,
        """
        IF OBJECT_ID(N'FinRepartitionRecette', N'U') IS NOT NULL
           AND COL_LENGTH(N'FinRepartitionRecette', N'WithholdingTypeId') IS NOT NULL
           AND OBJECT_ID(N'FinRetenue', N'U') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FinRepartitionRecette_Retenue')
            EXEC(N'ALTER TABLE [FinRepartitionRecette] WITH CHECK ADD CONSTRAINT [FK_FinRepartitionRecette_Retenue] FOREIGN KEY ([WithholdingTypeId]) REFERENCES [FinRetenue] ([Id])');
        """
    ];
}
