using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SchoolManagement.Updates;

/// <summary>
/// Moteur officiel des évolutions versionnées du schéma SQL école.
/// Reçoit uniquement un package local déjà vérifié — aucun téléchargement.
/// Non branché au démarrage de l'API (Lot 2B-1).
/// </summary>
public sealed class MigrationManager
{
    /// <summary>Baseline officielle : bases actuelles (initialiseurs) = 1.</summary>
    public const int BaselineSchemaVersion = 1;

    internal const string XactAbortSql = "SET XACT_ABORT ON;";

    private static readonly Regex GoSplitter = new(
        @"^\s*GO\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _connectionString;
    private readonly Action<string>? _log;

    public MigrationManager(string connectionString, Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Chaîne de connexion SQL requise.", nameof(connectionString));
        }

        _connectionString = connectionString;
        _log = log;
    }

    public static string FileNameFor(int fromVersion, int toVersion) =>
        $"Migration{fromVersion}_{toVersion}.sql";

    public async Task EnsureMetaTableAsync(CancellationToken cancellationToken = default)
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
                INSERT INTO dbo.AppSchemaVersion (Id, SchemaVersion) VALUES (1, 1);
            END
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureMetaTableAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(
            "SELECT SchemaVersion FROM dbo.AppSchemaVersion WHERE Id = 1;",
            connection);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is int i ? i : Convert.ToInt32(result);
    }

    /// <summary>
    /// Applique un package local (manifest + MigrationN_N+1.sql).
    /// Cible &lt; version actuelle → refus. Chaîne incomplète → refus avant toute exécution.
    /// </summary>
    public async Task<MigrationApplyResult> ApplyPackageAsync(
        string packageDirectory,
        CancellationToken cancellationToken = default)
    {
        var package = MigrationPackage.Load(packageDirectory);
        var current = await GetSchemaVersionAsync(cancellationToken);
        var manifest = package.Manifest;

        if (current < BaselineSchemaVersion)
        {
            throw new MigrationException(
                $"AppSchemaVersion={current} est inférieur à la baseline {BaselineSchemaVersion}.");
        }

        if (current > manifest.ToSchemaVersion)
        {
            throw new MigrationException(
                $"Cible {manifest.ToSchemaVersion} inférieure à la version actuelle {current}.");
        }

        if (current < manifest.FromSchemaVersion)
        {
            throw new MigrationException(
                $"Package fromSchemaVersion={manifest.FromSchemaVersion} trop élevé pour la version actuelle {current}.");
        }

        if (current == manifest.ToSchemaVersion)
        {
            _log?.Invoke($"Schéma déjà à {current} — aucune migration.");
            return new MigrationApplyResult(current, current, Array.Empty<string>());
        }

        var applied = new List<string>();
        for (var from = current; from < manifest.ToSchemaVersion; from++)
        {
            var to = from + 1;
            var fileName = FileNameFor(from, to);
            var path = package.GetMigrationPath(fileName);
            if (!File.Exists(path))
            {
                throw new MigrationException($"Migration manquante : {fileName}");
            }

            var script = await File.ReadAllTextAsync(path, cancellationToken);
            try
            {
                await ExecuteInTransactionAsync(script, to, cancellationToken);
            }
            catch (Exception ex) when (ex is not MigrationException)
            {
                throw new MigrationException(
                    $"Échec migration {from} → {to}. AppSchemaVersion reste {from}.",
                    ex);
            }

            applied.Add(fileName);
            _log?.Invoke($"Migration schéma {from} → {to} appliquée.");
        }

        var now = await GetSchemaVersionAsync(cancellationToken);
        return new MigrationApplyResult(current, now, applied);
    }

    internal static IReadOnlyList<string> SplitBatches(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return Array.Empty<string>();
        }

        return GoSplitter.Split(script)
            .Select(b => b.Trim())
            .Where(b => b.Length > 0)
            .ToList();
    }

    private async Task ExecuteInTransactionAsync(
        string script,
        int newVersion,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var xact = new SqlCommand(XactAbortSql, connection, tx) { CommandTimeout = 120 })
            {
                await xact.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var batch in SplitBatches(script))
            {
                await using var cmd = new SqlCommand(batch, connection, tx) { CommandTimeout = 120 };
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var versionCmd = new SqlCommand(
                             """
                             UPDATE dbo.AppSchemaVersion
                             SET SchemaVersion = @v, UpdatedAtUtc = SYSUTCDATETIME()
                             WHERE Id = 1;
                             """,
                             connection,
                             tx))
            {
                versionCmd.Parameters.AddWithValue("@v", newVersion);
                var updated = await versionCmd.ExecuteNonQueryAsync(cancellationToken);
                if (updated != 1)
                {
                    throw new MigrationException("Impossible de mettre à jour AppSchemaVersion (ligne Id=1 absente).");
                }
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _log?.Invoke("Rollback migration : " + rollbackEx.Message);
            }

            throw;
        }
    }
}
