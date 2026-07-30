using Microsoft.Data.SqlClient;

namespace SchoolManagement.Updates;

/// <summary>
/// Applique des scripts SQL numérotés (MigrationN_N+1.sql) dans une transaction.
/// </summary>
public sealed class MigrationManager
{
    private readonly string _connectionString;
    private readonly string _migrationsDirectory;
    private readonly Action<string>? _log;

    public MigrationManager(string connectionString, string migrationsDirectory, Action<string>? log = null)
    {
        _connectionString = connectionString;
        _migrationsDirectory = migrationsDirectory;
        _log = log;
    }

    public async Task EnsureMetaTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.AppSchemaVersion', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AppSchemaVersion
                (
                    Id INT NOT NULL CONSTRAINT PK_AppSchemaVersion PRIMARY KEY CHECK (Id = 1),
                    SchemaVersion INT NOT NULL,
                    UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_AppSchemaVersion_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
                );
                INSERT INTO dbo.AppSchemaVersion (Id, SchemaVersion) VALUES (1, 0);
            END
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken)
    {
        await EnsureMetaTableAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand("SELECT SchemaVersion FROM dbo.AppSchemaVersion WHERE Id = 1;", connection);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is int i ? i : Convert.ToInt32(result);
    }

    public async Task ApplyPendingAsync(int targetSchemaVersion, CancellationToken cancellationToken)
    {
        var current = await GetSchemaVersionAsync(cancellationToken);
        if (current >= targetSchemaVersion)
        {
            return;
        }

        for (var from = current; from < targetSchemaVersion; from++)
        {
            var to = from + 1;
            var file = Path.Combine(_migrationsDirectory, $"Migration{from}_{to}.sql");
            if (!File.Exists(file))
            {
                throw new FileNotFoundException(
                    $"Migration manquante pour le schéma {from} → {to}.", file);
            }

            var script = await File.ReadAllTextAsync(file, cancellationToken);
            await ExecuteInTransactionAsync(script, to, cancellationToken);
            _log?.Invoke($"Migration schéma {from} → {to} appliquée.");
        }
    }

    private async Task ExecuteInTransactionAsync(string script, int newVersion, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var batch in SplitBatches(script))
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await using var cmd = new SqlCommand(batch, connection, tx) { CommandTimeout = 120 };
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var versionCmd = new SqlCommand(
                             "UPDATE dbo.AppSchemaVersion SET SchemaVersion = @v, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = 1;",
                             connection,
                             tx))
            {
                versionCmd.Parameters.AddWithValue("@v", newVersion);
                await versionCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static IEnumerable<string> SplitBatches(string script)
    {
        return System.Text.RegularExpressions.Regex.Split(
            script,
            @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
