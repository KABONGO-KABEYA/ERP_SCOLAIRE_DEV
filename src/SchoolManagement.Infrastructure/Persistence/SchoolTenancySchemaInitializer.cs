using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Isolation multi-école : ajoute et remplit SchoolId sur les tables qui en étaient dépourvues
/// (périodes, sync, audit, historique de connexion, caisse), puis rend la colonne obligatoire.
/// Idempotent — rejouable à chaque démarrage, comme les autres initialiseurs de schéma.
/// </summary>
public sealed class SchoolTenancySchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<SchoolTenancySchemaInitializer> _logger;

    public SchoolTenancySchemaInitializer(
        string connectionString,
        ILogger<SchoolTenancySchemaInitializer> logger)
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

        _logger.LogInformation("Schéma isolation multi-école vérifié (SchoolId sur périodes, sync, audit, caisse).");
    }

    private static readonly string[] Scripts =
    [
        AddNullableSchoolId("AcademicPeriods"),
        AddNullableSchoolId("SyncWatermark"),
        AddNullableSchoolId("SyncJournal"),
        AddNullableSchoolId("SyncOutboxUnit"),
        AddNullableSchoolId("AuditEntries"),
        AddNullableSchoolId("LoginHistory"),
        AddNullableSchoolId("CashMovements"),

        // Backfill par chaîne d'appartenance quand elle existe, sinon école par défaut.
        """
        IF OBJECT_ID(N'AcademicPeriods', N'U') IS NOT NULL
           AND OBJECT_ID(N'AcademicYears', N'U') IS NOT NULL
           AND COL_LENGTH(N'AcademicPeriods', N'SchoolId') IS NOT NULL
            UPDATE ap
            SET ap.SchoolId = ay.SchoolId
            FROM [AcademicPeriods] ap
            INNER JOIN [AcademicYears] ay ON ay.Id = ap.AcademicYearId
            WHERE ap.SchoolId IS NULL;
        """,
        """
        IF OBJECT_ID(N'CashMovements', N'U') IS NOT NULL
           AND OBJECT_ID(N'Payments', N'U') IS NOT NULL
           AND COL_LENGTH(N'CashMovements', N'SchoolId') IS NOT NULL
            UPDATE cm
            SET cm.SchoolId = p.SchoolId
            FROM [CashMovements] cm
            INNER JOIN [Payments] p ON p.Id = cm.PaymentId
            WHERE cm.SchoolId IS NULL AND cm.PaymentId IS NOT NULL;
        """,
        """
        IF OBJECT_ID(N'Schools', N'U') IS NOT NULL
        BEGIN
            DECLARE @DefaultSchool uniqueidentifier =
                (SELECT TOP 1 Id FROM [Schools] WHERE IsDeleted = 0 ORDER BY CreatedAt);

            IF @DefaultSchool IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'AcademicPeriods', N'SchoolId') IS NOT NULL
                    UPDATE [AcademicPeriods] SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
                IF COL_LENGTH(N'SyncWatermark', N'SchoolId') IS NOT NULL
                    UPDATE [SyncWatermark] SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
                IF COL_LENGTH(N'SyncJournal', N'SchoolId') IS NOT NULL
                    UPDATE [SyncJournal] SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
                IF COL_LENGTH(N'SyncOutboxUnit', N'SchoolId') IS NOT NULL
                    UPDATE [SyncOutboxUnit] SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
                IF COL_LENGTH(N'AuditEntries', N'SchoolId') IS NOT NULL
                    UPDATE [AuditEntries] SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
                IF COL_LENGTH(N'LoginHistory', N'SchoolId') IS NOT NULL
                    UPDATE [LoginHistory] SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
                IF COL_LENGTH(N'CashMovements', N'SchoolId') IS NOT NULL
                    UPDATE [CashMovements] SET SchoolId = @DefaultSchool WHERE SchoolId IS NULL;
            END
        END
        """,

        // Colonne obligatoire une fois le backfill terminé (aucune ligne orpheline ne doit subsister).
        MakeSchoolIdRequired("AcademicPeriods"),
        MakeSchoolIdRequired("SyncWatermark"),
        MakeSchoolIdRequired("SyncJournal"),
        MakeSchoolIdRequired("SyncOutboxUnit"),
        MakeSchoolIdRequired("AuditEntries"),
        MakeSchoolIdRequired("LoginHistory"),
        MakeSchoolIdRequired("CashMovements"),

        SchoolIdIndex("AcademicPeriods"),
        SchoolIdIndex("SyncJournal"),
        SchoolIdIndex("SyncOutboxUnit"),
        SchoolIdIndex("AuditEntries"),
        SchoolIdIndex("LoginHistory"),
        SchoolIdIndex("CashMovements"),

        // L'unicité du watermark devient (école, table) : chaque école suit sa propre progression.
        """
        IF OBJECT_ID(N'SyncWatermark', N'U') IS NOT NULL
           AND COL_LENGTH(N'SyncWatermark', N'SchoolId') IS NOT NULL
        BEGIN
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SyncWatermark_TableName'
                       AND object_id = OBJECT_ID(N'SyncWatermark'))
                DROP INDEX [IX_SyncWatermark_TableName] ON [SyncWatermark];

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SyncWatermark_SchoolId_TableName'
                           AND object_id = OBJECT_ID(N'SyncWatermark'))
                CREATE UNIQUE INDEX [IX_SyncWatermark_SchoolId_TableName]
                    ON [SyncWatermark] ([SchoolId], [TableName]) WHERE [IsDeleted] = 0;
        END
        """
    ];

    private static string AddNullableSchoolId(string table) =>
        $"""
        IF OBJECT_ID(N'{table}', N'U') IS NOT NULL
           AND COL_LENGTH(N'{table}', N'SchoolId') IS NULL
            ALTER TABLE [{table}] ADD [SchoolId] uniqueidentifier NULL;
        """;

    private static string MakeSchoolIdRequired(string table) =>
        $"""
        IF OBJECT_ID(N'{table}', N'U') IS NOT NULL
           AND COL_LENGTH(N'{table}', N'SchoolId') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM [{table}] WHERE [SchoolId] IS NULL)
           AND EXISTS (SELECT 1 FROM sys.columns
                       WHERE object_id = OBJECT_ID(N'{table}') AND name = N'SchoolId' AND is_nullable = 1)
            ALTER TABLE [{table}] ALTER COLUMN [SchoolId] uniqueidentifier NOT NULL;
        """;

    private static string SchoolIdIndex(string table) =>
        $"""
        IF OBJECT_ID(N'{table}', N'U') IS NOT NULL
           AND COL_LENGTH(N'{table}', N'SchoolId') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_{table}_SchoolId'
                           AND object_id = OBJECT_ID(N'{table}'))
            CREATE INDEX [IX_{table}_SchoolId] ON [{table}] ([SchoolId]);
        """;
}
