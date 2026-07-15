using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

public sealed class SchoolFeeSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<SchoolFeeSchemaInitializer> _logger;

    public SchoolFeeSchemaInitializer(string connectionString, ILogger<SchoolFeeSchemaInitializer> logger)
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

        _logger.LogInformation("Schéma frais scolaires vérifié (FeeTypes, FeeInstallments, FeeTypeInstallments, FeePricingCategories, ClassFeeAmounts).");
    }

    private static readonly string[] Scripts =
    [
        """
        IF COL_LENGTH('FeeTypes', 'IsActive') IS NULL
            ALTER TABLE [FeeTypes] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_FeeTypes_IsActive] DEFAULT(1);
        IF COL_LENGTH('FeeTypes', 'IsMandatory') IS NULL
            ALTER TABLE [FeeTypes] ADD [IsMandatory] bit NOT NULL CONSTRAINT [DF_FeeTypes_IsMandatory] DEFAULT(0);
        IF COL_LENGTH('FeeTypes', 'DefaultAmount') IS NOT NULL
        BEGIN
            DECLARE @FeeTypesDefaultAmountConstraint sysname;
            SELECT @FeeTypesDefaultAmountConstraint = dc.name
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'FeeTypes') AND c.name = N'DefaultAmount';
            IF @FeeTypesDefaultAmountConstraint IS NOT NULL
                EXEC(N'ALTER TABLE [FeeTypes] DROP CONSTRAINT [' + @FeeTypesDefaultAmountConstraint + N']');
            ALTER TABLE [FeeTypes] DROP COLUMN [DefaultAmount];
        END
        IF COL_LENGTH('FeeTypes', 'IsRecurring') IS NOT NULL
        BEGIN
            DECLARE @FeeTypesIsRecurringConstraint sysname;
            SELECT @FeeTypesIsRecurringConstraint = dc.name
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'FeeTypes') AND c.name = N'IsRecurring';
            IF @FeeTypesIsRecurringConstraint IS NOT NULL
                EXEC(N'ALTER TABLE [FeeTypes] DROP CONSTRAINT [' + @FeeTypesIsRecurringConstraint + N']');
            ALTER TABLE [FeeTypes] DROP COLUMN [IsRecurring];
        END
        """,
        """
        IF OBJECT_ID(N'FeeInstallments', N'U') IS NULL
        BEGIN
            CREATE TABLE [FeeInstallments] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [Name] nvarchar(150) NOT NULL,
                [SortOrder] int NOT NULL CONSTRAINT [DF_FeeInstallments_SortOrder] DEFAULT(0),
                [IsActive] bit NOT NULL CONSTRAINT [DF_FeeInstallments_IsActive] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FeeInstallments] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FeeInstallments_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id])
            );
            CREATE INDEX [IX_FeeInstallments_IsDeleted] ON [FeeInstallments] ([IsDeleted]);
            CREATE INDEX [IX_FeeInstallments_SchoolId_SortOrder] ON [FeeInstallments] ([SchoolId], [SortOrder]);
        END
        """,
        """
        IF OBJECT_ID(N'ClassFeeAmounts', N'U') IS NULL
        BEGIN
            CREATE TABLE [ClassFeeAmounts] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [AcademicYearId] uniqueidentifier NOT NULL,
                [PedagogicalClassId] uniqueidentifier NOT NULL,
                [FeeTypeId] uniqueidentifier NOT NULL,
                [FeeInstallmentId] uniqueidentifier NOT NULL,
                [Amount] decimal(18,2) NOT NULL,
                [DueDate] date NULL,
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_ClassFeeAmounts] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_ClassFeeAmounts_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                CONSTRAINT [FK_ClassFeeAmounts_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]),
                CONSTRAINT [FK_ClassFeeAmounts_PedagogicalClasses_PedagogicalClassId] FOREIGN KEY ([PedagogicalClassId]) REFERENCES [PedagogicalClasses] ([Id]),
                CONSTRAINT [FK_ClassFeeAmounts_FeeTypes_FeeTypeId] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeTypes] ([Id]),
                CONSTRAINT [FK_ClassFeeAmounts_FeeInstallments_FeeInstallmentId] FOREIGN KEY ([FeeInstallmentId]) REFERENCES [FeeInstallments] ([Id])
            );
            CREATE INDEX [IX_ClassFeeAmounts_IsDeleted] ON [ClassFeeAmounts] ([IsDeleted]);
            CREATE UNIQUE INDEX [IX_ClassFeeAmounts_Year_Class_FeeType_Installment]
                ON [ClassFeeAmounts] ([AcademicYearId], [PedagogicalClassId], [FeeTypeId], [FeeInstallmentId])
                WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF COL_LENGTH('ClassFeeAmounts', 'SortOrder') IS NULL
            ALTER TABLE [ClassFeeAmounts] ADD [SortOrder] int NOT NULL CONSTRAINT [DF_ClassFeeAmounts_SortOrder] DEFAULT(0);
        """,
        """
        IF OBJECT_ID(N'FeePricingCategories', N'U') IS NULL
        BEGIN
            CREATE TABLE [FeePricingCategories] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [Code] nvarchar(20) NOT NULL,
                [Name] nvarchar(150) NOT NULL,
                [Description] nvarchar(500) NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_FeePricingCategories_IsActive] DEFAULT(1),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FeePricingCategories] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FeePricingCategories_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id])
            );
            CREATE INDEX [IX_FeePricingCategories_IsDeleted] ON [FeePricingCategories] ([IsDeleted]);
            CREATE UNIQUE INDEX [IX_FeePricingCategories_School_Code]
                ON [FeePricingCategories] ([SchoolId], [Code])
                WHERE [IsDeleted] = 0;
        END
        """,
        """
        IF OBJECT_ID(N'ClassFeeAmounts', N'U') IS NOT NULL
           AND COL_LENGTH('ClassFeeAmounts', 'FeePricingCategoryId') IS NULL
            ALTER TABLE [ClassFeeAmounts] ADD [FeePricingCategoryId] uniqueidentifier NULL;
        """,
        """
        IF OBJECT_ID(N'ClassFeeAmounts', N'U') IS NOT NULL
           AND COL_LENGTH('ClassFeeAmounts', 'FeePricingCategoryId') IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM sys.foreign_keys
               WHERE name = N'FK_ClassFeeAmounts_FeePricingCategories_FeePricingCategoryId')
            ALTER TABLE [ClassFeeAmounts] ADD CONSTRAINT [FK_ClassFeeAmounts_FeePricingCategories_FeePricingCategoryId]
                FOREIGN KEY ([FeePricingCategoryId]) REFERENCES [FeePricingCategories] ([Id]);
        """,
        """
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ClassFeeAmounts_Year_Class_FeeType_Installment' AND object_id = OBJECT_ID(N'ClassFeeAmounts'))
            DROP INDEX [IX_ClassFeeAmounts_Year_Class_FeeType_Installment] ON [ClassFeeAmounts];
        """,
        """
        IF OBJECT_ID(N'ClassFeeAmounts', N'U') IS NOT NULL
           AND COL_LENGTH('ClassFeeAmounts', 'FeePricingCategoryId') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ClassFeeAmounts_Year_Class_Category_FeeType_Installment' AND object_id = OBJECT_ID(N'ClassFeeAmounts'))
            CREATE UNIQUE INDEX [IX_ClassFeeAmounts_Year_Class_Category_FeeType_Installment]
                ON [ClassFeeAmounts] ([AcademicYearId], [PedagogicalClassId], [FeePricingCategoryId], [FeeTypeId], [FeeInstallmentId])
                WHERE [IsDeleted] = 0 AND [FeePricingCategoryId] IS NOT NULL;
        """,
        """
        IF OBJECT_ID(N'FeeTypeInstallments', N'U') IS NULL
        BEGIN
            CREATE TABLE [FeeTypeInstallments] (
                [Id] uniqueidentifier NOT NULL,
                [SchoolId] uniqueidentifier NOT NULL,
                [FeeTypeId] uniqueidentifier NOT NULL,
                [FeeInstallmentId] uniqueidentifier NOT NULL,
                [SortOrder] int NOT NULL CONSTRAINT [DF_FeeTypeInstallments_SortOrder] DEFAULT(0),
                [CreatedAt] datetime2 NOT NULL,
                [CreatedBy] uniqueidentifier NULL,
                [UpdatedAt] datetime2 NULL,
                [UpdatedBy] uniqueidentifier NULL,
                [IsDeleted] bit NOT NULL,
                [DeletedAt] datetime2 NULL,
                [DeletedBy] uniqueidentifier NULL,
                CONSTRAINT [PK_FeeTypeInstallments] PRIMARY KEY ([Id]),
                CONSTRAINT [FK_FeeTypeInstallments_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]),
                CONSTRAINT [FK_FeeTypeInstallments_FeeTypes_FeeTypeId] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeTypes] ([Id]),
                CONSTRAINT [FK_FeeTypeInstallments_FeeInstallments_FeeInstallmentId] FOREIGN KEY ([FeeInstallmentId]) REFERENCES [FeeInstallments] ([Id])
            );
            CREATE INDEX [IX_FeeTypeInstallments_IsDeleted] ON [FeeTypeInstallments] ([IsDeleted]);
            CREATE UNIQUE INDEX [IX_FeeTypeInstallments_FeeType_Installment]
                ON [FeeTypeInstallments] ([FeeTypeId], [FeeInstallmentId])
                WHERE [IsDeleted] = 0;
            CREATE INDEX [IX_FeeTypeInstallments_FeeType_SortOrder]
                ON [FeeTypeInstallments] ([FeeTypeId], [SortOrder]);
        END
        """
    ];
}
