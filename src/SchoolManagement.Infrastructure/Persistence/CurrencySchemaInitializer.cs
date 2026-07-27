using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Schéma devises / taux de change (FinDevise, FinEtablissementDevise, FinTypeTaux, FinTauxChange, FinHistoriqueTaux).</summary>
public sealed class CurrencySchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<CurrencySchemaInitializer> _logger;

    public CurrencySchemaInitializer(string connectionString, ILogger<CurrencySchemaInitializer> logger)
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

        await SeedDefaultsAsync(connection, cancellationToken);
        _logger.LogInformation(
            "Schéma devises vérifié (FinDevise, FinEtablissementDevise, FinTypeTaux, FinTauxChange, FinHistoriqueTaux).");
    }

    private static async Task SeedDefaultsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM [FinDevise] WHERE [Code] = N'CDF' AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [FinDevise] ([Id],[Code],[Name],[Symbol],[DecimalPlaces],[IsSystemLocal],[IsActive],[CreatedAt],[IsDeleted])
                VALUES (NEWID(), N'CDF', N'Franc congolais', N'FC', 0, 1, 1, SYSUTCDATETIME(), 0);
            END
            IF NOT EXISTS (SELECT 1 FROM [FinDevise] WHERE [Code] = N'USD' AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [FinDevise] ([Id],[Code],[Name],[Symbol],[DecimalPlaces],[IsSystemLocal],[IsActive],[CreatedAt],[IsDeleted])
                VALUES (NEWID(), N'USD', N'Dollar américain', N'$', 2, 0, 1, SYSUTCDATETIME(), 0);
            END
            IF NOT EXISTS (SELECT 1 FROM [FinDevise] WHERE [Code] = N'EUR' AND [IsDeleted] = 0)
            BEGIN
                INSERT INTO [FinDevise] ([Id],[Code],[Name],[Symbol],[DecimalPlaces],[IsSystemLocal],[IsActive],[CreatedAt],[IsDeleted])
                VALUES (NEWID(), N'EUR', N'Euro', N'€', 2, 0, 1, SYSUTCDATETIME(), 0);
            END

            IF NOT EXISTS (SELECT 1 FROM [FinTypeTaux] WHERE [Code] = N'INTERNE' AND [IsDeleted] = 0)
                INSERT INTO [FinTypeTaux] ([Id],[Code],[Name],[Description],[IsActive],[CreatedAt],[IsDeleted])
                VALUES (NEWID(), N'INTERNE', N'Interne', N'Taux interne établissement', 1, SYSUTCDATETIME(), 0);
            IF NOT EXISTS (SELECT 1 FROM [FinTypeTaux] WHERE [Code] = N'BCC' AND [IsDeleted] = 0)
                INSERT INTO [FinTypeTaux] ([Id],[Code],[Name],[Description],[IsActive],[CreatedAt],[IsDeleted])
                VALUES (NEWID(), N'BCC', N'Banque Centrale', N'Taux banque centrale', 1, SYSUTCDATETIME(), 0);
            IF NOT EXISTS (SELECT 1 FROM [FinTypeTaux] WHERE [Code] = N'ACHAT' AND [IsDeleted] = 0)
                INSERT INTO [FinTypeTaux] ([Id],[Code],[Name],[Description],[IsActive],[CreatedAt],[IsDeleted])
                VALUES (NEWID(), N'ACHAT', N'Achat', N'Taux d''achat', 1, SYSUTCDATETIME(), 0);
            IF NOT EXISTS (SELECT 1 FROM [FinTypeTaux] WHERE [Code] = N'VENTE' AND [IsDeleted] = 0)
                INSERT INTO [FinTypeTaux] ([Id],[Code],[Name],[Description],[IsActive],[CreatedAt],[IsDeleted])
                VALUES (NEWID(), N'VENTE', N'Vente', N'Taux de vente', 1, SYSUTCDATETIME(), 0);

            -- Associer CDF/USD à chaque école existante (CDF principale) si aucune devise école.
            INSERT INTO [FinEtablissementDevise] ([Id],[SchoolId],[CurrencyId],[IsPrimary],[AllowPayment],[CreatedAt],[IsDeleted])
            SELECT NEWID(), s.[Id], d.[Id], 1, 1, SYSUTCDATETIME(), 0
            FROM [Schools] s
            CROSS JOIN [FinDevise] d
            WHERE d.[Code] = N'CDF' AND d.[IsDeleted] = 0 AND s.[IsDeleted] = 0
              AND NOT EXISTS (
                  SELECT 1 FROM [FinEtablissementDevise] sc
                  WHERE sc.[SchoolId] = s.[Id] AND sc.[IsDeleted] = 0);
            INSERT INTO [FinEtablissementDevise] ([Id],[SchoolId],[CurrencyId],[IsPrimary],[AllowPayment],[CreatedAt],[IsDeleted])
            SELECT NEWID(), s.[Id], d.[Id], 0, 1, SYSUTCDATETIME(), 0
            FROM [Schools] s
            CROSS JOIN [FinDevise] d
            WHERE d.[Code] = N'USD' AND d.[IsDeleted] = 0 AND s.[IsDeleted] = 0
              AND EXISTS (SELECT 1 FROM [FinEtablissementDevise] sc WHERE sc.[SchoolId] = s.[Id] AND sc.[IsDeleted] = 0)
              AND NOT EXISTS (
                  SELECT 1 FROM [FinEtablissementDevise] sc
                  WHERE sc.[SchoolId] = s.[Id] AND sc.[CurrencyId] = d.[Id] AND sc.[IsDeleted] = 0);
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static readonly string[] Scripts =
    [
        """
        IF OBJECT_ID(N'FinDevise', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinDevise] (
                [Id] uniqueidentifier NOT NULL,
                [Code] nvarchar(10) NOT NULL,
                [Name] nvarchar(120) NOT NULL,
                [Symbol] nvarchar(10) NOT NULL,
                [DecimalPlaces] int NOT NULL CONSTRAINT [DF_FinDevise_DecimalPlaces] DEFAULT(2),
                [IsSystemLocal] bit NOT NULL CONSTRAINT [DF_FinDevise_IsSystemLocal] DEFAULT(0),
                [IsActive] bit NOT NULL CONSTRAINT [DF_FinDevise_IsActive] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinDevise_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinDevise] PRIMARY KEY ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinDevise_Code] ON [FinDevise] ([Code]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'FinTypeTaux', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinTypeTaux] (
                [Id] uniqueidentifier NOT NULL,
                [Code] nvarchar(40) NOT NULL,
                [Name] nvarchar(120) NOT NULL,
                [Description] nvarchar(500) NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_FinTypeTaux_IsActive] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinTypeTaux_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinTypeTaux] PRIMARY KEY ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinTypeTaux_Code] ON [FinTypeTaux] ([Code]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'FinEtablissementDevise', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinEtablissementDevise] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [CurrencyId] uniqueidentifier NOT NULL,
                [IsPrimary] bit NOT NULL CONSTRAINT [DF_FinEtablissementDevise_IsPrimary] DEFAULT(0),
                [AllowPayment] bit NOT NULL CONSTRAINT [DF_FinEtablissementDevise_AllowPayment] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinEtablissementDevise_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinEtablissementDevise] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinEtablissementDevise_School] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                CONSTRAINT [FK_FinEtablissementDevise_Devise] FOREIGN KEY ([CurrencyId]) REFERENCES [FinDevise] ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinEtablissementDevise_School_Currency]
                ON [FinEtablissementDevise] ([SchoolId], [CurrencyId]) WHERE [IsDeleted] = 0;
            CREATE UNIQUE INDEX [IX_FinEtablissementDevise_School_Primary]
                ON [FinEtablissementDevise] ([SchoolId]) WHERE [IsDeleted] = 0 AND [IsPrimary] = 1;
        END
        """,
        """
        IF OBJECT_ID(N'FinTauxChange', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinTauxChange] (
                [Id] uniqueidentifier NOT NULL,
                [SourceCurrencyId] uniqueidentifier NOT NULL,
                [TargetCurrencyId] uniqueidentifier NOT NULL,
                [RateTypeId] uniqueidentifier NOT NULL,
                [EffectiveDate] date NOT NULL,
                [Rate] decimal(18,6) NOT NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_FinTauxChange_IsActive] DEFAULT(1),
                [Notes] nvarchar(500) NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinTauxChange_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinTauxChange] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinTauxChange_Source] FOREIGN KEY ([SourceCurrencyId]) REFERENCES [FinDevise] ([Id]),
                CONSTRAINT [FK_FinTauxChange_Target] FOREIGN KEY ([TargetCurrencyId]) REFERENCES [FinDevise] ([Id]),
                CONSTRAINT [FK_FinTauxChange_Type] FOREIGN KEY ([RateTypeId]) REFERENCES [FinTypeTaux] ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinTauxChange_Active_Pair_Type]
                ON [FinTauxChange] ([SourceCurrencyId], [TargetCurrencyId], [RateTypeId])
                WHERE [IsDeleted] = 0 AND [IsActive] = 1;
            CREATE INDEX [IX_FinTauxChange_Date]
                ON [FinTauxChange] ([EffectiveDate], [SourceCurrencyId], [TargetCurrencyId]);
        END
        """,
        """
        IF OBJECT_ID(N'FinHistoriqueTaux', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinHistoriqueTaux] (
                [Id] uniqueidentifier NOT NULL,
                [ExchangeRateId] uniqueidentifier NOT NULL,
                [SourceCurrencyId] uniqueidentifier NOT NULL,
                [TargetCurrencyId] uniqueidentifier NOT NULL,
                [RateTypeId] uniqueidentifier NOT NULL,
                [OldRate] decimal(18,6) NULL,
                [NewRate] decimal(18,6) NOT NULL,
                [Action] nvarchar(40) NOT NULL,
                [UserId] uniqueidentifier NULL,
                [MachineName] nvarchar(120) NULL,
                [IpAddress] nvarchar(64) NULL,
                [OccurredAt] datetime2 NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinHistoriqueTaux_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinHistoriqueTaux] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinHistoriqueTaux_Taux] FOREIGN KEY ([ExchangeRateId]) REFERENCES [FinTauxChange] ([Id])
            );
            CREATE INDEX [IX_FinHistoriqueTaux_Rate_Date]
                ON [FinHistoriqueTaux] ([ExchangeRateId], [OccurredAt]);
        END
        """,
        """
        IF OBJECT_ID(N'Payments', N'U') IS NOT NULL AND COL_LENGTH(N'Payments', N'FeeCurrencyId') IS NULL
        BEGIN
            ALTER TABLE [Payments] ADD [FeeCurrencyId] uniqueidentifier NULL;
            ALTER TABLE [Payments] ADD [PaymentCurrencyId] uniqueidentifier NULL;
            ALTER TABLE [Payments] ADD [ExchangeRateId] uniqueidentifier NULL;
            ALTER TABLE [Payments] ADD [FeeCurrencyAmount] decimal(18,2) NULL;
            ALTER TABLE [Payments] ADD [PaymentCurrencyAmount] decimal(18,2) NULL;
            ALTER TABLE [Payments] ADD [AppliedExchangeRate] decimal(18,6) NULL;
        END
        """
    ];
}
