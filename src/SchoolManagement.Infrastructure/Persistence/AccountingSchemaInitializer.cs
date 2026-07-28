using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Tables comptabilité : demandes de paiement et dépenses.</summary>
public sealed class AccountingSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<AccountingSchemaInitializer> _logger;

    public AccountingSchemaInitializer(string connectionString, ILogger<AccountingSchemaInitializer> logger)
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
            "Schéma comptabilité vérifié (FinDemandePaiement, FinDepense, FinDepenseRepartitionDevise).");
    }

    private static readonly string[] Scripts =
    [
        """
        IF OBJECT_ID(N'FinDemandePaiement', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinDemandePaiement] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [AcademicYearId] uniqueidentifier NOT NULL,
                [DestinationId] uniqueidentifier NOT NULL,
                [Reference] nvarchar(40) NOT NULL,
                [Title] nvarchar(200) NOT NULL,
                [Description] nvarchar(500) NULL,
                [RequestedAmount] decimal(18,2) NOT NULL,
                [Currency] int NOT NULL,
                [RequestDate] date NOT NULL,
                [Status] int NOT NULL,
                [SubmittedAt] datetime2 NULL,
                [ApprovedAt] datetime2 NULL,
                [ApprovedByUserId] uniqueidentifier NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinDemandePaiement_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinDemandePaiement] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinDemandePaiement_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                CONSTRAINT [FK_FinDemandePaiement_AcademicYears] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
                CONSTRAINT [FK_FinDemandePaiement_Destination] FOREIGN KEY ([DestinationId]) REFERENCES [FinDestinationRepartition] ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinDemandePaiement_School_Reference]
                ON [FinDemandePaiement] ([SchoolId], [Reference]) WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_FinDemandePaiement_School_Status]
                ON [FinDemandePaiement] ([SchoolId], [Status]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinDepense] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [AcademicYearId] uniqueidentifier NOT NULL,
                [DestinationId] uniqueidentifier NOT NULL,
                [ExpenseRequestId] uniqueidentifier NULL,
                [Reference] nvarchar(40) NOT NULL,
                [Label] nvarchar(500) NOT NULL,
                [BeneficiaryName] nvarchar(150) NOT NULL,
                [AuthorizedByName] nvarchar(150) NOT NULL,
                [Amount] decimal(18,2) NOT NULL,
                [Currency] int NOT NULL,
                [ExpenseDate] date NOT NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinDepense_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinDepense] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinDepense_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                CONSTRAINT [FK_FinDepense_AcademicYears] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
                CONSTRAINT [FK_FinDepense_Destination] FOREIGN KEY ([DestinationId]) REFERENCES [FinDestinationRepartition] ([Id]),
                CONSTRAINT [FK_FinDepense_Demande] FOREIGN KEY ([ExpenseRequestId]) REFERENCES [FinDemandePaiement] ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinDepense_School_Reference]
                ON [FinDepense] ([SchoolId], [Reference]) WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_FinDepense_School_Date_Destination]
                ON [FinDepense] ([SchoolId], [ExpenseDate], [DestinationId]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'BeneficiaryName') IS NULL
        BEGIN
            ALTER TABLE [FinDepense] ADD [BeneficiaryName] nvarchar(150) NULL;
        END
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'AuthorizedByName') IS NULL
        BEGIN
            ALTER TABLE [FinDepense] ADD [AuthorizedByName] nvarchar(150) NULL;
        END
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'Label') IS NOT NULL
        BEGIN
            ALTER TABLE [FinDepense] ALTER COLUMN [Label] nvarchar(500) NOT NULL;
        END
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'BeneficiaryName') IS NOT NULL
        BEGIN
            UPDATE [FinDepense] SET [BeneficiaryName] = N'—' WHERE [BeneficiaryName] IS NULL OR LTRIM(RTRIM([BeneficiaryName])) = N'';
            ALTER TABLE [FinDepense] ALTER COLUMN [BeneficiaryName] nvarchar(150) NOT NULL;
        END
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'AuthorizedByName') IS NOT NULL
        BEGIN
            UPDATE [FinDepense] SET [AuthorizedByName] = N'—' WHERE [AuthorizedByName] IS NULL OR LTRIM(RTRIM([AuthorizedByName])) = N'';
            ALTER TABLE [FinDepense] ALTER COLUMN [AuthorizedByName] nvarchar(150) NOT NULL;
        END
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'PrimaryCurrencyId') IS NULL
        BEGIN
            ALTER TABLE [FinDepense] ADD [PrimaryCurrencyId] uniqueidentifier NULL;
            IF OBJECT_ID(N'FinDevise', N'U') IS NOT NULL
            BEGIN
                ALTER TABLE [FinDepense] ADD CONSTRAINT [FK_FinDepense_PrimaryCurrency]
                    FOREIGN KEY ([PrimaryCurrencyId]) REFERENCES [FinDevise] ([Id]);
            END
        END
        """,
        """
        IF OBJECT_ID(N'FinDepenseRepartitionDevise', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinDepenseRepartitionDevise] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [ExpensePaymentId] uniqueidentifier NOT NULL,
                [CurrencyId] uniqueidentifier NOT NULL,
                [Amount] decimal(18,2) NOT NULL,
                [ExchangeRateId] uniqueidentifier NULL,
                [AppliedExchangeRate] decimal(18,8) NOT NULL CONSTRAINT [DF_FinDepenseRepartitionDevise_Rate] DEFAULT(1),
                [EquivalentInPrimaryCurrency] decimal(18,2) NOT NULL,
                [SortOrder] int NOT NULL CONSTRAINT [DF_FinDepenseRepartitionDevise_Sort] DEFAULT(0),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinDepenseRepartitionDevise_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinDepenseRepartitionDevise] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinDepenseRepartitionDevise_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                CONSTRAINT [FK_FinDepenseRepartitionDevise_Depense] FOREIGN KEY ([ExpensePaymentId]) REFERENCES [FinDepense] ([Id]),
                CONSTRAINT [FK_FinDepenseRepartitionDevise_Currency] FOREIGN KEY ([CurrencyId]) REFERENCES [FinDevise] ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinDepenseRepartitionDevise_Payment_Currency]
                ON [FinDepenseRepartitionDevise] ([ExpensePaymentId], [CurrencyId]) WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_FinDepenseRepartitionDevise_School_Payment]
                ON [FinDepenseRepartitionDevise] ([SchoolId], [ExpensePaymentId]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'ExternalReference') IS NULL
            ALTER TABLE [FinDepense] ADD [ExternalReference] nvarchar(80) NULL;
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'Category') IS NULL
            ALTER TABLE [FinDepense] ADD [Category] nvarchar(40) NULL;
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'Observations') IS NULL
            ALTER TABLE [FinDepense] ADD [Observations] nvarchar(1000) NULL;
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'AttachmentFileName') IS NULL
            ALTER TABLE [FinDepense] ADD [AttachmentFileName] nvarchar(260) NULL;
        """,
        """
        IF OBJECT_ID(N'FinDepense', N'U') IS NOT NULL AND COL_LENGTH(N'FinDepense', N'AttachmentStoragePath') IS NULL
            ALTER TABLE [FinDepense] ADD [AttachmentStoragePath] nvarchar(500) NULL;
        """
    ];
}
