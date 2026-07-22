using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Ajoute Schools.DefaultFeeTypeId (frais principal de l'établissement).</summary>
public sealed class SchoolDefaultFeeSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<SchoolDefaultFeeSchemaInitializer> _logger;

    public SchoolDefaultFeeSchemaInitializer(
        string connectionString,
        ILogger<SchoolDefaultFeeSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureColumnAsync(
            connection,
            "Schools",
            "DefaultFeeTypeId",
            """
            ALTER TABLE [Schools] ADD [DefaultFeeTypeId] uniqueidentifier NULL;
            """,
            cancellationToken);

        // Index non unique pour les jointures.
        await using (var checkIndex = connection.CreateCommand())
        {
            checkIndex.CommandText = """
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Schools_DefaultFeeTypeId' AND object_id = OBJECT_ID(N'[Schools]')
                """;
            if (await checkIndex.ExecuteScalarAsync(cancellationToken) is null)
            {
                await using var createIndex = connection.CreateCommand();
                createIndex.CommandText = """
                    CREATE NONCLUSTERED INDEX [IX_Schools_DefaultFeeTypeId]
                    ON [Schools]([DefaultFeeTypeId]);
                    """;
                await createIndex.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        _logger.LogInformation("Schéma Schools.DefaultFeeTypeId vérifié.");
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
}
