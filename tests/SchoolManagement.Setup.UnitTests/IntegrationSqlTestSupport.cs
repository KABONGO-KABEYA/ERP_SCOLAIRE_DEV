using System.ServiceProcess;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Setup.UnitTests;

/// <summary>
/// Bases SQL dédiées aux tests d'intégration (jamais Production).
/// </summary>
internal static class IntegrationSqlTestSupport
{
    internal const string FreshInstallDatabaseName = "SchoolManagementRDC_Integ_FreshInstall";
    internal const string CloudSyncCloudDatabaseName = "SchoolManagementRDC_Integ_CloudSync_Cloud";
    internal const string CloudSyncLocalADatabaseName = "SchoolManagementRDC_Integ_CloudSync_LocalA";
    internal const string CloudSyncLocalBDatabaseName = "SchoolManagementRDC_Integ_CloudSync_LocalB";

    internal static bool TryResolveSql(out SqlConnectionStringBuilder builder, out string skipReason)
    {
        skipReason = string.Empty;
        builder = new SqlConnectionStringBuilder();

        try
        {
            if (ServiceControllerExists())
            {
                var env = ReinstallTestSqlSupport.ReadServiceEnvironment();
                builder = ReinstallTestSqlSupport.ParseDefaultConnection(env);
                return true;
            }
        }
        catch
        {
            // fallback below
        }

        foreach (var candidate in new[]
                 {
                     "Server=localhost\\HEROS_SQL19;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True",
                     "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True",
                     "Server=.\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True",
                 })
        {
            try
            {
                builder = new SqlConnectionStringBuilder(candidate);
                using var cn = new SqlConnection(builder.ConnectionString);
                cn.Open();
                return true;
            }
            catch
            {
                // try next
            }
        }

        skipReason = "SQL Server local indisponible (service ErpScolaireApi absent et instances localhost non joignables).";
        return false;
    }

    internal static string BuildMasterConnectionString(SqlConnectionStringBuilder source) =>
        ReinstallTestSqlSupport.BuildMasterConnectionString(source);

    internal static string BuildDatabaseConnectionString(SqlConnectionStringBuilder source, string databaseName)
    {
        EnsureSafeDatabaseName(databaseName);
        var builder = new SqlConnectionStringBuilder(source.ConnectionString)
        {
            InitialCatalog = databaseName,
        };
        return builder.ConnectionString;
    }

    internal static async Task RecreateDatabaseAsync(
        string masterConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        EnsureSafeDatabaseName(databaseName);
        var safe = Bracket(databaseName);
        await using var cn = new SqlConnection(masterConnectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = cn.CreateCommand();
        cmd.CommandTimeout = 120;
        cmd.CommandText = $@"
IF DB_ID(N'{EscapeSql(databaseName)}') IS NOT NULL
BEGIN
  ALTER DATABASE {safe} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE {safe};
END;
CREATE DATABASE {safe};";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task DropDatabaseAsync(
        string masterConnectionString,
        string databaseName,
        CancellationToken cancellationToken)
    {
        EnsureSafeDatabaseName(databaseName);
        var safe = Bracket(databaseName);
        await using var cn = new SqlConnection(masterConnectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = cn.CreateCommand();
        cmd.CommandTimeout = 120;
        cmd.CommandText = $@"
IF DB_ID(N'{EscapeSql(databaseName)}') IS NOT NULL
BEGIN
  ALTER DATABASE {safe} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE {safe};
END;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task ApplyBaselineAsync(string connectionString, CancellationToken cancellationToken)
    {
        EnsureSafeCatalog(connectionString);
        var sql = await File.ReadAllTextAsync(FindBaselineSqlScript(), cancellationToken);
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync(cancellationToken);

        foreach (var batch in SplitSqlBatches(sql))
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandTimeout = 180;
            cmd.CommandText = "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;" + Environment.NewLine + batch;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    internal static async Task ApplySetupPostBaselineAsync(string connectionString, CancellationToken cancellationToken)
    {
        EnsureSafeCatalog(connectionString);
        await InstallerEngine.ApplyCriticalSchemaUpgradesAsync(connectionString, _ => { }, cancellationToken);
    }

    internal static async Task ApplyCurriculumSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        EnsureSafeCatalog(connectionString);
        var curriculum = new CurriculumSchemaInitializer(
            connectionString,
            NullLogger<CurriculumSchemaInitializer>.Instance);
        await curriculum.EnsureUpdatedAsync(cancellationToken);
    }

    internal static async Task ApplyCloudSyncSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        EnsureSafeCatalog(connectionString);
        var sync = new CloudSyncSchemaInitializer(
            connectionString,
            NullLogger<CloudSyncSchemaInitializer>.Instance);
        await sync.EnsureCreatedAsync(cancellationToken);
    }

    internal static async Task ApplyEfCompatibleSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        var schoolDefaultFee = new SchoolDefaultFeeSchemaInitializer(
            connectionString,
            NullLogger<SchoolDefaultFeeSchemaInitializer>.Instance);
        await schoolDefaultFee.EnsureCreatedAsync(cancellationToken);

        var courseCode = new CourseCodeSchemaInitializer(
            connectionString,
            NullLogger<CourseCodeSchemaInitializer>.Instance);
        await courseCode.EnsureUpdatedAsync(cancellationToken);

        var schoolTenancy = new SchoolTenancySchemaInitializer(
            connectionString,
            NullLogger<SchoolTenancySchemaInitializer>.Instance);
        await schoolTenancy.EnsureCreatedAsync(cancellationToken);
    }

    internal static async Task ApplyFreshInstallSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        await ApplyBaselineAsync(connectionString, cancellationToken);
        await ApplyCurriculumSchemaAsync(connectionString, cancellationToken);
        await ApplyCloudSyncSchemaAsync(connectionString, cancellationToken);
        await ApplyEfCompatibleSchemaAsync(connectionString, cancellationToken);
        await ApplySetupPostBaselineAsync(connectionString, cancellationToken);
    }

    internal static async Task<IReadOnlyList<string>> ReadMigrationHistoryAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(reader.GetString(0));
        }

        return list;
    }

    internal static async Task<IReadOnlyList<(string Name, bool IsUnique, string? Filter)>> ReadCourseIndexesAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT i.name, i.is_unique, i.filter_definition
            FROM sys.indexes i
            WHERE i.object_id = OBJECT_ID(N'dbo.Courses')
              AND i.name IS NOT NULL
            ORDER BY i.name
            """;
        var list = new List<(string, bool, string?)>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add((reader.GetString(0), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return list;
    }

    internal static async Task<int> ScalarAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static bool ServiceControllerExists()
    {
        try
        {
            return ServiceController.GetServices()
                .Any(s => s.ServiceName.Equals(InstallerEngine.ServiceName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureSafeDatabaseName(string name)
    {
        if (string.Equals(name, ReinstallTestSqlSupport.ProductionDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refus d'utiliser la base Production.");
        }

        if (!name.StartsWith("SchoolManagementRDC_Integ_", StringComparison.Ordinal)
            && !string.Equals(name, ReinstallTestSqlSupport.TestDatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Nom de base de test interdit : {name}");
        }
    }

    private static void EnsureSafeCatalog(string connectionString)
    {
        EnsureSafeDatabaseName(ReadCatalog(connectionString));
    }

    private static string ReadCatalog(string connectionString) =>
        new SqlConnectionStringBuilder(connectionString).InitialCatalog;

    private static string FindBaselineSqlScript()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "scripts", ReinstallTestSqlSupport.BaselineSqlFileName)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "scripts", ReinstallTestSqlSupport.BaselineSqlFileName)),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                return c;
            }
        }

        throw new FileNotFoundException($"{ReinstallTestSqlSupport.BaselineSqlFileName} introuvable.");
    }

    private static List<string> SplitSqlBatches(string sql)
    {
        var raw = System.Text.RegularExpressions.Regex.Split(
            sql,
            @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var batches = new List<string>();
        foreach (var batch in raw)
        {
            var text = batch.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"^\s*USE\s+\[[^\]]+\]\s*;?\s*",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
            text = text.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                batches.Add(text);
            }
        }

        return batches;
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");

    private static string Bracket(string identifier) => "[" + identifier.Replace("]", "]]") + "]";
}
