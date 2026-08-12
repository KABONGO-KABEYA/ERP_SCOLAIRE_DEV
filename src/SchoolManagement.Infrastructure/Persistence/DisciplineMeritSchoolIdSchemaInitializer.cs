using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Aligne DisciplineRecords et MeritRecords avec le modèle EF (migration AddPedagogicalStructure).
/// Idempotent : colonne SchoolId, backfill depuis Students.SchoolId, index composite.
/// </summary>
public sealed class DisciplineMeritSchoolIdSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DisciplineMeritSchoolIdSchemaInitializer> _logger;

    public DisciplineMeritSchoolIdSchemaInitializer(
        string connectionString,
        ILogger<DisciplineMeritSchoolIdSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureUpdatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureDisciplineRecordsAsync(connection, cancellationToken);
        await EnsureMeritRecordsAsync(connection, cancellationToken);

        _logger.LogInformation(
            "Schéma discipline/mérite vérifié (DisciplineRecords.SchoolId, MeritRecords.SchoolId).");
    }

    private static async Task EnsureDisciplineRecordsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            connection,
            "DisciplineRecords",
            "SchoolId",
            """
            ALTER TABLE [DisciplineRecords]
            ADD [SchoolId] uniqueidentifier NOT NULL
                CONSTRAINT [DF_DisciplineRecords_SchoolId] DEFAULT '00000000-0000-0000-0000-000000000000';
            """,
            cancellationToken);

        await DropIndexIfExistsAsync(
            connection,
            "DisciplineRecords",
            "IX_DisciplineRecords_StudentId_IncidentDate",
            cancellationToken);

        await BackfillFromStudentsAsync(connection, "DisciplineRecords", cancellationToken);

        await EnsureIndexAsync(
            connection,
            "IX_DisciplineRecords_SchoolId_StudentId_IncidentDate",
            """
            CREATE INDEX [IX_DisciplineRecords_SchoolId_StudentId_IncidentDate]
                ON [DisciplineRecords] ([SchoolId], [StudentId], [IncidentDate]);
            """,
            cancellationToken);

        await EnsureIndexAsync(
            connection,
            "IX_DisciplineRecords_StudentId",
            """
            CREATE INDEX [IX_DisciplineRecords_StudentId]
                ON [DisciplineRecords] ([StudentId]);
            """,
            cancellationToken);
    }

    private static async Task EnsureMeritRecordsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(
            connection,
            "MeritRecords",
            "SchoolId",
            """
            ALTER TABLE [MeritRecords]
            ADD [SchoolId] uniqueidentifier NOT NULL
                CONSTRAINT [DF_MeritRecords_SchoolId] DEFAULT '00000000-0000-0000-0000-000000000000';
            """,
            cancellationToken);

        await BackfillFromStudentsAsync(connection, "MeritRecords", cancellationToken);

        await EnsureIndexAsync(
            connection,
            "IX_MeritRecords_SchoolId_StudentId_AwardDate",
            """
            CREATE INDEX [IX_MeritRecords_SchoolId_StudentId_AwardDate]
                ON [MeritRecords] ([SchoolId], [StudentId], [AwardDate]);
            """,
            cancellationToken);
    }

    private static async Task BackfillFromStudentsAsync(
        SqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var backfill = connection.CreateCommand();
        backfill.CommandText = $"""
            UPDATE dr
            SET dr.[SchoolId] = s.[SchoolId]
            FROM [{tableName}] dr
            INNER JOIN [Students] s ON s.[Id] = dr.[StudentId]
            WHERE dr.[SchoolId] = '00000000-0000-0000-0000-000000000000';
            """;
        await backfill.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqlConnection connection,
        string tableName,
        string columnName,
        string alterSql,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = """
            SELECT 1
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @table AND COLUMN_NAME = @column
            """;
        check.Parameters.AddWithValue("@table", tableName);
        check.Parameters.AddWithValue("@column", columnName);

        if (await check.ExecuteScalarAsync(cancellationToken) is not null)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = alterSql;
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DropIndexIfExistsAsync(
        SqlConnection connection,
        string tableName,
        string indexName,
        CancellationToken cancellationToken)
    {
        await using var drop = connection.CreateCommand();
        drop.CommandText = """
            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = @index AND object_id = OBJECT_ID(@table))
            DROP INDEX [@indexPlaceholder] ON [@tablePlaceholder];
            """;
        drop.CommandText = drop.CommandText
            .Replace("@indexPlaceholder", indexName)
            .Replace("@tablePlaceholder", tableName);
        drop.Parameters.AddWithValue("@index", indexName);
        drop.Parameters.AddWithValue("@table", tableName);
        await drop.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureIndexAsync(
        SqlConnection connection,
        string indexName,
        string createSql,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = """
            SELECT 1 FROM sys.indexes WHERE name = @index
            """;
        check.Parameters.AddWithValue("@index", indexName);

        if (await check.ExecuteScalarAsync(cancellationToken) is not null)
        {
            return;
        }

        await using var create = connection.CreateCommand();
        create.CommandText = createSql;
        await create.ExecuteNonQueryAsync(cancellationToken);
    }
}
