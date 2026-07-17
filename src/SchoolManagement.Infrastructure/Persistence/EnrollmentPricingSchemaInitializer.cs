using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Ajoute Enrollments.FeePricingCategoryId, assure la catégorie GENERAL par école, et backfille les inscriptions.
/// </summary>
public sealed class EnrollmentPricingSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<EnrollmentPricingSchemaInitializer> _logger;

    public EnrollmentPricingSchemaInitializer(
        string connectionString,
        ILogger<EnrollmentPricingSchemaInitializer> logger)
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
            "Schéma inscription / catégorie tarifaire vérifié (Enrollments.FeePricingCategoryId, catégorie {Code}).",
            FeePricingCategoryCodes.General);
    }

    private static readonly string[] Scripts =
    [
        // 1) Assurer une catégorie GENERAL active par école.
        $$"""
        IF OBJECT_ID(N'FeePricingCategories', N'U') IS NOT NULL
        BEGIN
            INSERT INTO [FeePricingCategories] (
                [Id], [SchoolId], [Code], [Name], [Description], [IsActive],
                [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy],
                [IsDeleted], [DeletedAt], [DeletedBy])
            SELECT
                NEWID(),
                s.[Id],
                N'{{FeePricingCategoryCodes.General}}',
                N'Générale',
                N'Catégorie tarifaire par défaut (inscription)',
                1,
                SYSUTCDATETIME(),
                NULL,
                NULL,
                NULL,
                0,
                NULL,
                NULL
            FROM [Schools] s
            WHERE s.[IsDeleted] = 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM [FeePricingCategories] c
                  WHERE c.[SchoolId] = s.[Id]
                    AND c.[IsDeleted] = 0
                    AND UPPER(c.[Code]) = N'{{FeePricingCategoryCodes.General}}');
        END
        """,
        // 2) Ajouter la colonne nullable (batch séparé : SQL Server compile le batch avant ALTER).
        """
        IF OBJECT_ID(N'Enrollments', N'U') IS NOT NULL
           AND COL_LENGTH(N'Enrollments', N'FeePricingCategoryId') IS NULL
            ALTER TABLE [Enrollments] ADD [FeePricingCategoryId] uniqueidentifier NULL;
        """,
        // 3) Backfill GENERAL.
        $$"""
        IF OBJECT_ID(N'Enrollments', N'U') IS NOT NULL
           AND COL_LENGTH(N'Enrollments', N'FeePricingCategoryId') IS NOT NULL
        BEGIN
            UPDATE e
            SET e.[FeePricingCategoryId] = c.[Id]
            FROM [Enrollments] e
            INNER JOIN [Students] st ON st.[Id] = e.[StudentId]
            INNER JOIN [FeePricingCategories] c
                ON c.[SchoolId] = st.[SchoolId]
               AND c.[IsDeleted] = 0
               AND UPPER(c.[Code]) = N'{{FeePricingCategoryCodes.General}}'
            WHERE e.[FeePricingCategoryId] IS NULL;

            UPDATE e
            SET e.[FeePricingCategoryId] = x.[Id]
            FROM [Enrollments] e
            INNER JOIN [Students] st ON st.[Id] = e.[StudentId]
            CROSS APPLY (
                SELECT TOP (1) c.[Id]
                FROM [FeePricingCategories] c
                WHERE c.[SchoolId] = st.[SchoolId]
                  AND c.[IsDeleted] = 0
                  AND c.[IsActive] = 1
                ORDER BY c.[Name]
            ) x
            WHERE e.[FeePricingCategoryId] IS NULL;
        END
        """,
        // 4) NOT NULL + FK + index.
        """
        IF OBJECT_ID(N'Enrollments', N'U') IS NOT NULL
           AND COL_LENGTH(N'Enrollments', N'FeePricingCategoryId') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM [Enrollments] WHERE [FeePricingCategoryId] IS NULL)
                THROW 50001, N'Impossible de forcer FeePricingCategoryId NOT NULL : inscriptions sans catégorie.', 1;

            IF EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'Enrollments')
                  AND name = N'FeePricingCategoryId'
                  AND is_nullable = 1)
                ALTER TABLE [Enrollments] ALTER COLUMN [FeePricingCategoryId] uniqueidentifier NOT NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = N'FK_Enrollments_FeePricingCategories')
                ALTER TABLE [Enrollments] WITH CHECK
                ADD CONSTRAINT [FK_Enrollments_FeePricingCategories]
                FOREIGN KEY ([FeePricingCategoryId]) REFERENCES [FeePricingCategories] ([Id]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Enrollments_AcademicYearId_FeePricingCategoryId'
                  AND object_id = OBJECT_ID(N'Enrollments'))
                CREATE INDEX [IX_Enrollments_AcademicYearId_FeePricingCategoryId]
                    ON [Enrollments] ([AcademicYearId], [FeePricingCategoryId]);
        END
        """
    ];
}
