using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SchoolManagement.Setup.UnitTests;

public sealed class DatabaseMigrationApplicationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApplyCriticalSchemaUpgrades_completes_missing_post_baseline_structures()
    {
        var originalEnvironment = ReinstallTestSqlSupport.ReadServiceEnvironment();
        var originalConnection = ReinstallTestSqlSupport.ParseDefaultConnection(originalEnvironment);
        var masterCs = ReinstallTestSqlSupport.BuildMasterConnectionString(originalConnection);
        var testCs = ReinstallTestSqlSupport.BuildTestConnectionString(
            originalConnection,
            ReinstallTestSqlSupport.TestDatabaseName);

        try
        {
            await ReinstallTestSqlSupport.RecreateTestDatabaseAsync(
                masterCs,
                ReinstallTestSqlSupport.TestDatabaseName,
                CancellationToken.None);
            await ReinstallTestSqlSupport.ApplyBaselineAsync(
                testCs,
                ReinstallTestSqlSupport.TestDatabaseName,
                CancellationToken.None);

            await InstallerEngine.ApplyCriticalSchemaUpgradesAsync(
                testCs,
                _ => { },
                CancellationToken.None);

            await using var cn = new SqlConnection(testCs);
            await cn.OpenAsync();

            (await ScalarAsync(cn,
                "SELECT CASE WHEN OBJECT_ID(N'dbo.RegistrationNumberCounters', N'U') IS NULL THEN 0 ELSE 1 END"))
                .Should().Be(1);

            (await ScalarAsync(cn,
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RegistrationNumberCounters_SchoolId_Year' AND object_id = OBJECT_ID(N'dbo.RegistrationNumberCounters')) THEN 1 ELSE 0 END"))
                .Should().Be(1);

            (await ScalarAsync(cn,
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserRoleAssignments_UserId_RoleId' AND object_id = OBJECT_ID(N'dbo.UserRoleAssignments') AND has_filter = 1) THEN 1 ELSE 0 END"))
                .Should().Be(1);

            (await ScalarAsync(cn,
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20260706114538_InitialCreate') THEN 1 ELSE 0 END"))
                .Should().Be(1);
        }
        finally
        {
            await ReinstallTestSqlSupport.DropTestDatabaseAsync(
                masterCs,
                ReinstallTestSqlSupport.TestDatabaseName,
                CancellationToken.None);
        }
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var value = await cmd.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }
}
