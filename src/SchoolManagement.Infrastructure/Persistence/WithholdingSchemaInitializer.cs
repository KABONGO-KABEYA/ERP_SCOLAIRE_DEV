using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Initialise les tables de retenues (FinRetenue, FinRetenueConfiguration).</summary>
public sealed class WithholdingSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<WithholdingSchemaInitializer> _logger;

    public WithholdingSchemaInitializer(string connectionString, ILogger<WithholdingSchemaInitializer> logger)
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

        _logger.LogInformation("Schéma retenues vérifié (FinRetenue, FinRetenueConfiguration).");
    }

    private static readonly string[] Scripts =
    [
        """
        IF OBJECT_ID(N'FinRetenue', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinRetenue] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [Code] nvarchar(20) NOT NULL,
                [Name] nvarchar(120) NOT NULL,
                [Description] nvarchar(500) NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_FinRetenue_IsActive] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinRetenue_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinRetenue] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinRetenue_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinRetenue_SchoolId_Code]
                ON [FinRetenue] ([SchoolId], [Code]) WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'FinRetenueConfiguration', N'U') IS NULL
        BEGIN
            CREATE TABLE [FinRetenueConfiguration] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [AcademicYearId] uniqueidentifier NOT NULL,
                [WithholdingTypeId] uniqueidentifier NOT NULL,
                [FeeTypeId] uniqueidentifier NOT NULL,
                [FeeInstallmentId] uniqueidentifier NULL,
                [PricingCategoryId] uniqueidentifier NULL,
                [CalculationMode] int NOT NULL,
                [Value] decimal(18,4) NOT NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_FinRetenueConfiguration_IsActive] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL CONSTRAINT [DF_FinRetenueConfiguration_IsDeleted] DEFAULT(0),
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FinRetenueConfiguration] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FinRetenueConfiguration_Schools] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                CONSTRAINT [FK_FinRetenueConfiguration_Years] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
                CONSTRAINT [FK_FinRetenueConfiguration_Type] FOREIGN KEY ([WithholdingTypeId]) REFERENCES [FinRetenue] ([Id]),
                CONSTRAINT [FK_FinRetenueConfiguration_FeeTypes] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeTypes] ([Id]),
                CONSTRAINT [FK_FinRetenueConfiguration_Installments] FOREIGN KEY ([FeeInstallmentId]) REFERENCES [FeeInstallments] ([Id]),
                CONSTRAINT [FK_FinRetenueConfiguration_Categories] FOREIGN KEY ([PricingCategoryId]) REFERENCES [FeePricingCategories] ([Id])
            );
            CREATE UNIQUE INDEX [IX_FinRetenueConfiguration_Unique]
                ON [FinRetenueConfiguration] (
                    [SchoolId],
                    [AcademicYearId],
                    [WithholdingTypeId],
                    [FeeTypeId],
                    [FeeInstallmentId],
                    [PricingCategoryId]
                ) WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_FinRetenueConfiguration_School_Year]
                ON [FinRetenueConfiguration] ([SchoolId], [AcademicYearId], [IsActive]);
        END
        """
    ];
}
