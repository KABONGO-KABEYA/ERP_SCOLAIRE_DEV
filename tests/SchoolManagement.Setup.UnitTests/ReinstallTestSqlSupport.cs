using System.Security.Principal;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace SchoolManagement.Setup.UnitTests;

/// <summary>
/// Prépare une base SQL dédiée au test de réinstallation.
/// Ne se connecte jamais à SchoolManagementRDC_Production comme catalogue cible.
/// </summary>
internal static class ReinstallTestSqlSupport
{
    internal const string TestDatabaseName = "SchoolManagementRDC_SetupReinstallTest";
    internal const string ProductionDatabaseName = "SchoolManagementRDC_Production";
    internal const string InitialCreateMigrationId = "20260706114538_InitialCreate";
    internal const string BaselineSqlFileName = "001_InitialCreate_EF.sql";
    internal const string ServiceRegistryPath = @"SYSTEM\CurrentControlSet\Services\ErpScolaireApi";
    internal const string LocalSystemSidValue = "S-1-5-18";
    internal const string LocalSystemSqlSidHex = "0x010100000000000512000000";

    internal static string[] ReadServiceEnvironment()
    {
        using var key = Registry.LocalMachine.OpenSubKey(ServiceRegistryPath, writable: false)
            ?? throw new InvalidOperationException(
                "Clé registre du service ErpScolaireApi introuvable.");
        if (key.GetValue("Environment") is not string[] env || env.Length == 0)
            throw new InvalidOperationException(
                "Valeur Environment du service ErpScolaireApi absente.");
        return env;
    }

    internal static void WriteServiceEnvironment(string[] environment)
    {
        using var key = Registry.LocalMachine.OpenSubKey(ServiceRegistryPath, writable: true)
            ?? throw new InvalidOperationException(
                "Impossible d'ouvrir la clé registre ErpScolaireApi en écriture.");
        key.SetValue("Environment", environment, RegistryValueKind.MultiString);
    }

    internal static SqlConnectionStringBuilder ParseDefaultConnection(string[] environment)
    {
        var raw = environment.FirstOrDefault(v =>
            v.StartsWith("ConnectionStrings__Default=", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Default absent de l'environnement du service.");
        var cs = raw["ConnectionStrings__Default=".Length..];
        return new SqlConnectionStringBuilder(cs);
    }

    internal static string[] WithTestCatalog(string[] environment, string testDatabaseName)
    {
        EnsureTestDatabaseName(testDatabaseName);
        var copy = (string[])environment.Clone();
        for (var i = 0; i < copy.Length; i++)
        {
            if (!copy[i].StartsWith("ConnectionStrings__Default=", StringComparison.OrdinalIgnoreCase))
                continue;

            var builder = new SqlConnectionStringBuilder(copy[i]["ConnectionStrings__Default=".Length..]);
            builder.InitialCatalog = testDatabaseName;
            if (IsProductionCatalog(builder.InitialCatalog))
                throw new InvalidOperationException("Refus : le catalogue de test pointe vers Production.");
            copy[i] = "ConnectionStrings__Default=" + builder.ConnectionString;
            return copy;
        }

        throw new InvalidOperationException("ConnectionStrings__Default introuvable pour le retarget de test.");
    }

    internal static string BuildMasterConnectionString(SqlConnectionStringBuilder source)
    {
        var builder = new SqlConnectionStringBuilder(source.ConnectionString)
        {
            InitialCatalog = "master",
        };
        return builder.ConnectionString;
    }

    internal static string BuildTestConnectionString(SqlConnectionStringBuilder source, string testDatabaseName)
    {
        EnsureTestDatabaseName(testDatabaseName);
        var builder = new SqlConnectionStringBuilder(source.ConnectionString)
        {
            InitialCatalog = testDatabaseName,
        };
        if (IsProductionCatalog(builder.InitialCatalog))
            throw new InvalidOperationException("Refus : connexion de test vers Production.");
        return builder.ConnectionString;
    }

    internal static async Task RecreateTestDatabaseAsync(string masterConnectionString, string testDatabaseName, CancellationToken ct)
    {
        EnsureTestDatabaseName(testDatabaseName);
        var safe = Bracket(testDatabaseName);
        await using var cn = new SqlConnection(masterConnectionString);
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandTimeout = 120;
        cmd.CommandText = $@"
IF DB_ID(N'{EscapeSql(testDatabaseName)}') IS NOT NULL
BEGIN
  ALTER DATABASE {safe} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE {safe};
END;
CREATE DATABASE {safe};";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task DropTestDatabaseAsync(string masterConnectionString, string testDatabaseName, CancellationToken ct)
    {
        EnsureTestDatabaseName(testDatabaseName);
        var safe = Bracket(testDatabaseName);
        await using var cn = new SqlConnection(masterConnectionString);
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandTimeout = 120;
        cmd.CommandText = $@"
IF DB_ID(N'{EscapeSql(testDatabaseName)}') IS NOT NULL
BEGIN
  ALTER DATABASE {safe} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE {safe};
END;";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task ApplyBaselineAsync(string testConnectionString, string testDatabaseName, CancellationToken ct)
    {
        EnsureTestDatabaseName(testDatabaseName);
        AssertNotProductionCatalog(testConnectionString);

        var sql = await File.ReadAllTextAsync(FindBaselineSqlScript(), ct);
        await using var cn = new SqlConnection(testConnectionString);
        await cn.OpenAsync(ct);

        var batches = SplitSqlBatches(sql);
        var batchIndex = 0;
        foreach (var batch in batches)
        {
            batchIndex++;
            await using var cmd = cn.CreateCommand();
            cmd.CommandTimeout = 180;
            cmd.CommandText = "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;" + Environment.NewLine + batch;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    internal static async Task VerifyBaselineAsync(string testConnectionString, string testDatabaseName, CancellationToken ct)
    {
        EnsureTestDatabaseName(testDatabaseName);
        AssertNotProductionCatalog(testConnectionString);

        await using var cn = new SqlConnection(testConnectionString);
        await cn.OpenAsync(ct);

        if (await ScalarAsync(cn, "SELECT CASE WHEN OBJECT_ID(N'dbo.Schools', N'U') IS NULL THEN 0 ELSE 1 END", ct) != 1)
            throw new InvalidOperationException("dbo.Schools absente de la base de test après 001.");

        if (await ScalarAsync(cn, "SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL THEN 0 ELSE 1 END", ct) != 1)
            throw new InvalidOperationException("dbo.__EFMigrationsHistory absente de la base de test après 001.");

        if (await ScalarAsync(cn,
                $"SELECT COUNT(1) FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'{EscapeSql(InitialCreateMigrationId)}'",
                ct) < 1)
        {
            throw new InvalidOperationException(
                $"Migration {InitialCreateMigrationId} absente de __EFMigrationsHistory sur la base de test.");
        }
    }

    /// <summary>
    /// Nom localisé de LocalSystem (S-1-5-18), ex. AUTORITE NT\Système ou NT AUTHORITY\SYSTEM.
    /// </summary>
    internal static string ResolveLocalSystemAccountName()
    {
        var sid = new SecurityIdentifier(LocalSystemSidValue);
        return ((NTAccount)sid.Translate(typeof(NTAccount))).Value;
    }

    internal static async Task GrantSystemAccessAsync(string masterConnectionString, string testDatabaseName, CancellationToken ct)
    {
        EnsureTestDatabaseName(testDatabaseName);
        var db = Bracket(testDatabaseName);
        var windowsName = ResolveLocalSystemAccountName();

        await using var cn = new SqlConnection(masterConnectionString);
        await cn.OpenAsync(ct);

        string? sqlLoginName;
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText =
                $"SELECT name FROM sys.server_principals WHERE sid = {LocalSystemSqlSidHex}";
            sqlLoginName = (string?)await cmd.ExecuteScalarAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(sqlLoginName))
        {
            throw new InvalidOperationException(
                "Login SQL LocalSystem introuvable (SID S-1-5-18). " +
                $"Compte Windows résolu : {windowsName}. " +
                "Ce test n'en crée pas : le login serveur doit déjà exister.");
        }

        var login = Bracket(sqlLoginName);
        var loginLiteral = EscapeSql(sqlLoginName);

        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $@"
USE {db};
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE sid = {LocalSystemSqlSidHex})
  CREATE USER {login} FOR LOGIN {login};
IF IS_ROLEMEMBER(N'db_owner', N'{loginLiteral}') = 0
  ALTER ROLE [db_owner] ADD MEMBER {login};";
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    internal static void EnsureTestDatabaseName(string name)
    {
        if (!string.Equals(name, TestDatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Nom de base de test interdit : {name}");
        if (IsProductionCatalog(name))
            throw new InvalidOperationException("Refus d'utiliser SchoolManagementRDC_Production.");
    }

    private static bool IsProductionCatalog(string catalog) =>
        string.Equals(catalog, ProductionDatabaseName, StringComparison.OrdinalIgnoreCase);

    private static void AssertNotProductionCatalog(string connectionString)
    {
        var catalog = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (IsProductionCatalog(catalog))
            throw new InvalidOperationException("Refus : commande SQL dirigée vers Production.");
        EnsureTestDatabaseName(catalog);
    }

    private static async Task<int> ScalarAsync(SqlConnection cn, string commandText, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = commandText;
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static string FindBaselineSqlScript()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "scripts", BaselineSqlFileName)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dist", "setup", "payload", "sql", BaselineSqlFileName)),
            Path.Combine(@"C:\Temp\ERP_Scolaire_Setup\payload\sql", BaselineSqlFileName),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        throw new FileNotFoundException(
            $"{BaselineSqlFileName} introuvable. Présence attendue dans database/scripts/.");
    }

    private static List<string> SplitSqlBatches(string sql)
    {
        var raw = Regex.Split(
            sql,
            @"^\s*GO\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        var batches = new List<string>();
        foreach (var batch in raw)
        {
            var text = batch.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            text = Regex.Replace(
                text,
                @"^\s*USE\s+\[[^\]]+\]\s*;?\s*",
                "",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            text = text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            batches.Add(text);
        }

        return batches;
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");

    private static string Bracket(string identifier) => "[" + identifier.Replace("]", "]]") + "]";
}
