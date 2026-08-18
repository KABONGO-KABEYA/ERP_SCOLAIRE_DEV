using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using SchoolManagement.Application.Auth.DTOs;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace SchoolManagement.IntegrationTests;

/// <summary>
/// Installation neuve : baseline seule → démarrage API (SchemaInitializers, dont RegistrationNumberCounters et UserRoleAssignments).
/// </summary>
public sealed class FreshInstallApiIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private FreshInstallWebApplicationFactory? _factory;
    private string? _skipReason;
    private string? _testCs;
    private bool _countersMissingBeforeApi;
    private bool _countersPresentAfterFirstApiStart;
    private bool _roleIndexUnfilteredBeforeApi;
    private bool _roleIndexFilteredAfterFirstApiStart;
    private bool _secondApiStartSucceeded;

    public FreshInstallApiIntegrationTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        if (!FreshInstallSqlSupport.TryResolveSql(out var source, out _skipReason))
        {
            return;
        }

        var masterCs = FreshInstallSqlSupport.BuildMasterConnectionString(source);
        _testCs = FreshInstallSqlSupport.BuildDatabaseConnectionString(
            source,
            FreshInstallSqlSupport.DatabaseName);

        await FreshInstallSqlSupport.RecreateDatabaseAsync(
            masterCs,
            FreshInstallSqlSupport.DatabaseName,
            CancellationToken.None);
        await FreshInstallSqlSupport.ApplyBaselineAsync(_testCs, CancellationToken.None);

        _countersMissingBeforeApi = await FreshInstallSqlSupport.TableExistsAsync(
            _testCs, "dbo.RegistrationNumberCounters", CancellationToken.None) == 0;
        _roleIndexUnfilteredBeforeApi = await FreshInstallSqlSupport.IndexIsUniqueFilteredAsync(
            _testCs,
            "dbo.UserRoleAssignments",
            "IX_UserRoleAssignments_UserId_RoleId",
            CancellationToken.None) == 0;

        _factory = new FreshInstallWebApplicationFactory(_testCs);
        _ = _factory.CreateClient();

        _countersPresentAfterFirstApiStart = await FreshInstallSqlSupport.TableExistsAsync(
            _testCs, "dbo.RegistrationNumberCounters", CancellationToken.None) == 1;
        _roleIndexFilteredAfterFirstApiStart = await FreshInstallSqlSupport.IndexIsUniqueFilteredAsync(
            _testCs,
            "dbo.UserRoleAssignments",
            "IX_UserRoleAssignments_UserId_RoleId",
            CancellationToken.None) == 1;

        _factory.Dispose();
        _factory = new FreshInstallWebApplicationFactory(_testCs);
        _ = _factory.CreateClient();
        _secondApiStartSucceeded = true;
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();

        if (FreshInstallSqlSupport.TryResolveSql(out var source, out _))
        {
            var masterCs = FreshInstallSqlSupport.BuildMasterConnectionString(source);
            await FreshInstallSqlSupport.DropDatabaseAsync(
                masterCs,
                FreshInstallSqlSupport.DatabaseName,
                CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Api_start_applies_post_baseline_schema_without_setup_and_is_idempotent()
    {
        if (_skipReason != null)
        {
            _output.WriteLine("SKIP: " + _skipReason);
            return;
        }

        _countersMissingBeforeApi.Should().BeTrue(
            "la baseline ne doit pas contenir RegistrationNumberCounters");
        _countersPresentAfterFirstApiStart.Should().BeTrue(
            "le démarrage API doit créer RegistrationNumberCounters");
        _roleIndexUnfilteredBeforeApi.Should().BeTrue(
            "la baseline pose IX_UserRoleAssignments_UserId_RoleId sans filtre IsDeleted");
        _roleIndexFilteredAfterFirstApiStart.Should().BeTrue(
            "le démarrage API doit poser l'index UNIQUE filtré UserRoleAssignments");
        _secondApiStartSucceeded.Should().BeTrue(
            "un second démarrage API doit rester idempotent");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_returns_ok_on_fresh_install_database()
    {
        if (_skipReason != null)
        {
            _output.WriteLine("SKIP: " + _skipReason);
            return;
        }

        var client = _factory!.CreateClient();
        var response = await client.GetAsync("/api/v1/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Registration_number_endpoint_returns_success_not_500()
    {
        if (_skipReason != null)
        {
            _output.WriteLine("SKIP: " + _skipReason);
            return;
        }

        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Admin@2026"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        auth!.Data!.AccessToken.Should().NotBeNullOrEmpty();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Data.AccessToken);

        var response = await client.GetAsync("/api/v1/enrollment-wizard/registration-number");
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<GeneratedRegistrationNumberDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.RegistrationNumber.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine("RegistrationNumber=" + body.Data.RegistrationNumber);
    }
}

internal sealed class FreshInstallWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _fileStorageRoot =
        Path.Combine(Path.GetTempPath(), "erp-fresh-integ-files-" + Guid.NewGuid().ToString("N"));

    public FreshInstallWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
        Directory.CreateDirectory(_fileStorageRoot);
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        Environment.SetEnvironmentVariable("FILE_STORAGE_ROOT", _fileStorageRoot);
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "FreshInstallIntegrationTests-Jwt-Secret-Key-32chars!");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_fileStorageRoot))
        {
            try { Directory.Delete(_fileStorageRoot, recursive: true); } catch { /* ignore */ }
        }

        base.Dispose(disposing);
    }
}

internal static class FreshInstallSqlSupport
{
    internal const string DatabaseName = "SchoolManagementRDC_Integ_FreshInstallApi";

    internal static bool TryResolveSql(out SqlConnectionStringBuilder builder, out string skipReason)
    {
        skipReason = string.Empty;
        builder = new SqlConnectionStringBuilder();

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

        skipReason = "SQL Server local indisponible pour FreshInstallApiIntegrationTests.";
        return false;
    }

    internal static string BuildMasterConnectionString(SqlConnectionStringBuilder source) =>
        new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = "master" }.ConnectionString;

    internal static string BuildDatabaseConnectionString(SqlConnectionStringBuilder source, string databaseName)
    {
        EnsureSafeDatabaseName(databaseName);
        return new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = databaseName }.ConnectionString;
    }

    internal static async Task RecreateDatabaseAsync(string masterCs, string databaseName, CancellationToken ct)
    {
        EnsureSafeDatabaseName(databaseName);
        var safe = "[" + databaseName.Replace("]", "]]") + "]";
        await using var cn = new SqlConnection(masterCs);
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandTimeout = 120;
        cmd.CommandText = $@"
IF DB_ID(N'{databaseName.Replace("'", "''")}') IS NOT NULL
BEGIN
  ALTER DATABASE {safe} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE {safe};
END;
CREATE DATABASE {safe};";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task DropDatabaseAsync(string masterCs, string databaseName, CancellationToken ct)
    {
        EnsureSafeDatabaseName(databaseName);
        var safe = "[" + databaseName.Replace("]", "]]") + "]";
        await using var cn = new SqlConnection(masterCs);
        await cn.OpenAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandTimeout = 120;
        cmd.CommandText = $@"
IF DB_ID(N'{databaseName.Replace("'", "''")}') IS NOT NULL
BEGIN
  ALTER DATABASE {safe} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  DROP DATABASE {safe};
END;";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task<int> TableExistsAsync(
        string connectionString,
        string objectName,
        CancellationToken cancellationToken)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT CASE WHEN OBJECT_ID(N'{objectName.Replace("'", "''")}', N'U') IS NULL THEN 0 ELSE 1 END";
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    internal static async Task<int> IndexIsUniqueFilteredAsync(
        string connectionString,
        string tableName,
        string indexName,
        CancellationToken cancellationToken)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync(cancellationToken);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = @indexName
                  AND object_id = OBJECT_ID(@tableName)
                  AND is_unique = 1
                  AND has_filter = 1
                  AND filter_definition LIKE N'%IsDeleted%')
            THEN 1 ELSE 0 END
            """;
        cmd.Parameters.AddWithValue("@indexName", indexName);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    internal static async Task ApplyBaselineAsync(string connectionString, CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(FindBaselineSqlScript(), ct);
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync(ct);
        foreach (var batch in SplitSqlBatches(sql))
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandTimeout = 180;
            cmd.CommandText = "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;" + Environment.NewLine + batch;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static string FindBaselineSqlScript()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "scripts", "001_InitialCreate_EF.sql")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "database", "scripts", "001_InitialCreate_EF.sql")),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                return c;
            }
        }

        throw new FileNotFoundException("001_InitialCreate_EF.sql introuvable.");
    }

    private static IEnumerable<string> SplitSqlBatches(string sql)
    {
        foreach (var batch in System.Text.RegularExpressions.Regex.Split(
                     sql,
                     @"^\s*GO\s*$",
                     System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase))
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
                yield return text;
            }
        }
    }

    internal static async Task ApplySetupPostBaselineAsync(string connectionString, CancellationToken cancellationToken)
    {
        var registrationSchema = new RegistrationNumberCounterSchemaInitializer(
            connectionString,
            NullLogger<RegistrationNumberCounterSchemaInitializer>.Instance);
        await registrationSchema.EnsureCreatedAsync(cancellationToken);

        var userRoleAssignmentSchema = new UserRoleAssignmentSchemaInitializer(
            connectionString,
            NullLogger<UserRoleAssignmentSchemaInitializer>.Instance);
        await userRoleAssignmentSchema.EnsureUpdatedAsync(cancellationToken);
    }

    private static void EnsureSafeDatabaseName(string name)
    {
        if (!name.StartsWith("SchoolManagementRDC_Integ_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Nom de base interdit : {name}");
        }
    }
}
