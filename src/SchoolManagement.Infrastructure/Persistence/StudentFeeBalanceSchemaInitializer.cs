using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Migre StudentFeeBalances vers une ligne par ClassFeeAmount (ClassFeeAmountId),
/// puis retire AcademicYearId / FeeTypeId.
/// Les scripts sensibles utilisent du SQL dynamique pour rester ré-exécutables
/// après suppression des anciennes colonnes (évite erreur 207 à la compilation).
/// </summary>
public sealed class StudentFeeBalanceSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<StudentFeeBalanceSchemaInitializer> _logger;

    public StudentFeeBalanceSchemaInitializer(
        string connectionString,
        ILogger<StudentFeeBalanceSchemaInitializer> logger)
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
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Schéma StudentFeeBalances vérifié (ClassFeeAmountId, index unique StudentId+ClassFeeAmountId).");
    }

    private static readonly string[] Scripts =
    [
        // 1) Ajouter ClassFeeAmountId (nullable le temps de la migration).
        """
        IF OBJECT_ID(N'StudentFeeBalances', N'U') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'ClassFeeAmountId') IS NULL
            ALTER TABLE [StudentFeeBalances] ADD [ClassFeeAmountId] uniqueidentifier NULL;
        """,

        // 2) Migrer les soldes agrégés → une ligne par tranche (SQL dynamique).
        """
        IF OBJECT_ID(N'StudentFeeBalances', N'U') IS NOT NULL
           AND OBJECT_ID(N'ClassFeeAmounts', N'U') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'ClassFeeAmountId') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'AcademicYearId') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'FeeTypeId') IS NOT NULL
           AND EXISTS (SELECT 1 FROM [StudentFeeBalances] WHERE [ClassFeeAmountId] IS NULL AND [IsDeleted] = 0)
        EXEC(N'
            ;WITH Legacy AS (
                SELECT
                    b.[Id] AS OldBalanceId,
                    b.[StudentId],
                    b.[AcademicYearId],
                    b.[FeeTypeId],
                    b.[AmountDue] AS LegacyDue,
                    b.[AmountPaid] AS LegacyPaid,
                    b.[Currency],
                    b.[CreatedAt],
                    b.[CreatedBy],
                    e.[FeePricingCategoryId],
                    cr.[PedagogicalClassId]
                FROM [StudentFeeBalances] b
                INNER JOIN [Enrollments] e
                    ON e.[StudentId] = b.[StudentId]
                   AND e.[AcademicYearId] = b.[AcademicYearId]
                   AND e.[IsActive] = 1
                   AND e.[IsDeleted] = 0
                INNER JOIN [ClassRooms] cr ON cr.[Id] = e.[ClassRoomId] AND cr.[IsDeleted] = 0
                WHERE b.[ClassFeeAmountId] IS NULL
                  AND b.[IsDeleted] = 0
                  AND cr.[PedagogicalClassId] IS NOT NULL
            ),
            Targets AS (
                SELECT
                    l.*,
                    cfa.[Id] AS ClassFeeAmountId,
                    cfa.[Amount] AS TariffAmount,
                    cfa.[SortOrder],
                    SUM(cfa.[Amount]) OVER (
                        PARTITION BY l.OldBalanceId
                        ORDER BY cfa.[SortOrder], cfa.[Id]
                        ROWS UNBOUNDED PRECEDING) AS CumDue,
                    SUM(cfa.[Amount]) OVER (
                        PARTITION BY l.OldBalanceId
                        ORDER BY cfa.[SortOrder], cfa.[Id]
                        ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING) AS PrevCumDue
                FROM Legacy l
                INNER JOIN [ClassFeeAmounts] cfa
                    ON cfa.[AcademicYearId] = l.[AcademicYearId]
                   AND cfa.[PedagogicalClassId] = l.[PedagogicalClassId]
                   AND cfa.[FeePricingCategoryId] = l.[FeePricingCategoryId]
                   AND cfa.[FeeTypeId] = l.[FeeTypeId]
                   AND cfa.[IsDeleted] = 0
            )
            INSERT INTO [StudentFeeBalances] (
                [Id], [StudentId], [ClassFeeAmountId], [AmountDue], [AmountPaid], [Currency],
                [CreatedAt], [CreatedBy], [UpdatedAt], [UpdatedBy],
                [IsDeleted], [DeletedAt], [DeletedBy],
                [AcademicYearId], [FeeTypeId])
            SELECT
                NEWID(),
                t.[StudentId],
                t.[ClassFeeAmountId],
                t.[TariffAmount],
                CASE
                    WHEN t.[LegacyPaid] >= t.[CumDue] THEN t.[TariffAmount]
                    WHEN t.[LegacyPaid] > ISNULL(t.[PrevCumDue], 0)
                        THEN t.[LegacyPaid] - ISNULL(t.[PrevCumDue], 0)
                    ELSE 0
                END,
                t.[Currency],
                t.[CreatedAt],
                t.[CreatedBy],
                SYSUTCDATETIME(),
                NULL,
                0, NULL, NULL,
                t.[AcademicYearId],
                t.[FeeTypeId]
            FROM Targets t
            WHERE NOT EXISTS (
                SELECT 1
                FROM [StudentFeeBalances] x
                WHERE x.[StudentId] = t.[StudentId]
                  AND x.[ClassFeeAmountId] = t.[ClassFeeAmountId]
                  AND x.[IsDeleted] = 0);

            UPDATE b
            SET b.[IsDeleted] = 1,
                b.[DeletedAt] = SYSUTCDATETIME(),
                b.[DeletedBy] = NULL
            FROM [StudentFeeBalances] b
            WHERE b.[ClassFeeAmountId] IS NULL
              AND b.[IsDeleted] = 0;
        ');
        """,

        // 3) Soft-delete les orphelins non migrables.
        """
        IF OBJECT_ID(N'StudentFeeBalances', N'U') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'ClassFeeAmountId') IS NOT NULL
            UPDATE [StudentFeeBalances]
            SET [IsDeleted] = 1,
                [DeletedAt] = SYSUTCDATETIME(),
                [DeletedBy] = NULL
            WHERE [ClassFeeAmountId] IS NULL
              AND [IsDeleted] = 0;
        """,

        // 4) FK vers ClassFeeAmounts.
        """
        IF OBJECT_ID(N'StudentFeeBalances', N'U') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'ClassFeeAmountId') IS NOT NULL
           AND OBJECT_ID(N'ClassFeeAmounts', N'U') IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StudentFeeBalances_ClassFeeAmounts')
           AND NOT EXISTS (
               SELECT 1 FROM [StudentFeeBalances]
               WHERE [ClassFeeAmountId] IS NULL AND [IsDeleted] = 0)
        BEGIN
            DELETE FROM [StudentFeeBalances]
            WHERE [ClassFeeAmountId] IS NULL;

            ALTER TABLE [StudentFeeBalances] ALTER COLUMN [ClassFeeAmountId] uniqueidentifier NOT NULL;

            ALTER TABLE [StudentFeeBalances] WITH CHECK
            ADD CONSTRAINT [FK_StudentFeeBalances_ClassFeeAmounts]
                FOREIGN KEY ([ClassFeeAmountId]) REFERENCES [ClassFeeAmounts] ([Id]);
        END
        """,

        // 5) Index unique filtré.
        """
        IF OBJECT_ID(N'StudentFeeBalances', N'U') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'ClassFeeAmountId') IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM sys.indexes
               WHERE name = N'IX_StudentFeeBalances_StudentId_ClassFeeAmountId'
                 AND object_id = OBJECT_ID(N'StudentFeeBalances'))
            CREATE UNIQUE INDEX [IX_StudentFeeBalances_StudentId_ClassFeeAmountId]
                ON [StudentFeeBalances] ([StudentId], [ClassFeeAmountId])
                WHERE [IsDeleted] = 0;
        """,

        // 6) Index ClassFeeAmountId.
        """
        IF OBJECT_ID(N'StudentFeeBalances', N'U') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'ClassFeeAmountId') IS NOT NULL
           AND NOT EXISTS (
               SELECT 1 FROM sys.indexes
               WHERE name = N'IX_StudentFeeBalances_ClassFeeAmountId'
                 AND object_id = OBJECT_ID(N'StudentFeeBalances'))
            CREATE INDEX [IX_StudentFeeBalances_ClassFeeAmountId]
                ON [StudentFeeBalances] ([ClassFeeAmountId]);
        """,

        // 7) Supprimer l'ancien index unique.
        """
        IF EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_StudentFeeBalances_StudentId_AcademicYearId_FeeTypeId'
              AND object_id = OBJECT_ID(N'StudentFeeBalances'))
            DROP INDEX [IX_StudentFeeBalances_StudentId_AcademicYearId_FeeTypeId] ON [StudentFeeBalances];
        """,

        // 8) Supprimer AcademicYearId / FeeTypeId (SQL dynamique).
        """
        IF OBJECT_ID(N'StudentFeeBalances', N'U') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'AcademicYearId') IS NOT NULL
        BEGIN
            DECLARE @fkYear sysname =
                (SELECT TOP 1 fk.name
                 FROM sys.foreign_keys fk
                 INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                 INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                 WHERE fk.parent_object_id = OBJECT_ID(N'StudentFeeBalances')
                   AND c.name = N'AcademicYearId');
            IF @fkYear IS NOT NULL
                EXEC(N'ALTER TABLE [StudentFeeBalances] DROP CONSTRAINT [' + @fkYear + N']');

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_StudentFeeBalances_AcademicYearId'
                  AND object_id = OBJECT_ID(N'StudentFeeBalances'))
                DROP INDEX [IX_StudentFeeBalances_AcademicYearId] ON [StudentFeeBalances];

            EXEC(N'ALTER TABLE [StudentFeeBalances] DROP COLUMN [AcademicYearId]');
        END
        """,
        """
        IF OBJECT_ID(N'StudentFeeBalances', N'U') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'FeeTypeId') IS NOT NULL
        BEGIN
            DECLARE @fkFee sysname =
                (SELECT TOP 1 fk.name
                 FROM sys.foreign_keys fk
                 INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                 INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                 WHERE fk.parent_object_id = OBJECT_ID(N'StudentFeeBalances')
                   AND c.name = N'FeeTypeId');
            IF @fkFee IS NOT NULL
                EXEC(N'ALTER TABLE [StudentFeeBalances] DROP CONSTRAINT [' + @fkFee + N']');

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_StudentFeeBalances_FeeTypeId'
                  AND object_id = OBJECT_ID(N'StudentFeeBalances'))
                DROP INDEX [IX_StudentFeeBalances_FeeTypeId] ON [StudentFeeBalances];

            EXEC(N'ALTER TABLE [StudentFeeBalances] DROP COLUMN [FeeTypeId]');
        END
        """,

        // 9) Recréer la vue situation financière.
        """
        IF OBJECT_ID(N'dbo.vw_SituationFinanciereEleve', N'V') IS NOT NULL
            DROP VIEW dbo.vw_SituationFinanciereEleve;
        """,
        """
        IF OBJECT_ID(N'StudentFeeBalances', N'U') IS NOT NULL
           AND COL_LENGTH(N'StudentFeeBalances', N'ClassFeeAmountId') IS NOT NULL
           AND OBJECT_ID(N'ClassFeeAmounts', N'U') IS NOT NULL
           AND OBJECT_ID(N'dbo.vw_SituationFinanciereEleve', N'V') IS NULL
        EXEC(N'
        CREATE VIEW dbo.vw_SituationFinanciereEleve
        AS
        SELECT
            s.Id AS StudentId,
            s.RegistrationNumber,
            s.LastName,
            s.FirstName,
            cfa.AcademicYearId,
            ay.Label AS AcademicYearLabel,
            cfa.FeeTypeId,
            ft.Code AS FeeTypeCode,
            ft.Name AS FeeTypeName,
            cfa.FeeInstallmentId,
            fi.Name AS FeeInstallmentName,
            sfb.ClassFeeAmountId,
            sfb.Currency,
            sfb.AmountDue,
            sfb.AmountPaid,
            (sfb.AmountDue - sfb.AmountPaid) AS Balance,
            CASE
                WHEN sfb.AmountPaid >= sfb.AmountDue THEN N''À jour''
                WHEN sfb.AmountPaid > 0 THEN N''Partiel''
                ELSE N''Débiteur''
            END AS PaymentStatus
        FROM StudentFeeBalances sfb
        INNER JOIN Students s ON s.Id = sfb.StudentId AND s.IsDeleted = 0
        INNER JOIN ClassFeeAmounts cfa ON cfa.Id = sfb.ClassFeeAmountId AND cfa.IsDeleted = 0
        INNER JOIN AcademicYears ay ON ay.Id = cfa.AcademicYearId AND ay.IsDeleted = 0
        INNER JOIN FeeTypes ft ON ft.Id = cfa.FeeTypeId AND ft.IsDeleted = 0
        LEFT JOIN FeeInstallments fi ON fi.Id = cfa.FeeInstallmentId AND fi.IsDeleted = 0
        WHERE sfb.IsDeleted = 0;
        ');
        """
    ];
}
