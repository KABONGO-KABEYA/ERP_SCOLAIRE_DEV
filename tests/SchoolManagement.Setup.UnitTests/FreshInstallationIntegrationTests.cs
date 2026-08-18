using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace SchoolManagement.Setup.UnitTests;

/// <summary>
/// Reproduit le chemin Setup : DB vide → baseline → initializers → compléments post-baseline.
/// </summary>
public sealed class FreshInstallationIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public FreshInstallationIntegrationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Fresh_install_produces_complete_schema_without_duplicate_object_errors()
    {
        if (!IntegrationSqlTestSupport.TryResolveSql(out var source, out var skipReason))
        {
            _output.WriteLine("SKIP: " + skipReason);
            return;
        }

        var masterCs = IntegrationSqlTestSupport.BuildMasterConnectionString(source);
        var testCs = IntegrationSqlTestSupport.BuildDatabaseConnectionString(
            source,
            IntegrationSqlTestSupport.FreshInstallDatabaseName);

        try
        {
            await IntegrationSqlTestSupport.RecreateDatabaseAsync(
                masterCs,
                IntegrationSqlTestSupport.FreshInstallDatabaseName,
                CancellationToken.None);

            // 1) baseline (001_InitialCreate_EF.sql)
            await IntegrationSqlTestSupport.ApplyBaselineAsync(testCs, CancellationToken.None);

            var historyAfterBaseline = await IntegrationSqlTestSupport.ReadMigrationHistoryAsync(
                testCs,
                CancellationToken.None);
            historyAfterBaseline.Should().Equal(ReinstallTestSqlSupport.InitialCreateMigrationId);
            _output.WriteLine("__EFMigrationsHistory après baseline: " + string.Join(", ", historyAfterBaseline));

            // 2) initializers API (simulation du pré-contrôle EnsureApiCanStartAsync)
            await IntegrationSqlTestSupport.ApplyCurriculumSchemaAsync(testCs, CancellationToken.None);
            await IntegrationSqlTestSupport.ApplyCloudSyncSchemaAsync(testCs, CancellationToken.None);

            // 3) compléments Setup post-baseline
            await IntegrationSqlTestSupport.ApplySetupPostBaselineAsync(testCs, CancellationToken.None);

            // Idempotence : rejouer sans erreur "already exists"
            var replay = async () =>
            {
                await IntegrationSqlTestSupport.ApplyCurriculumSchemaAsync(testCs, CancellationToken.None);
                await IntegrationSqlTestSupport.ApplyCloudSyncSchemaAsync(testCs, CancellationToken.None);
                await IntegrationSqlTestSupport.ApplySetupPostBaselineAsync(testCs, CancellationToken.None);
            };
            await replay.Should().NotThrowAsync("les initializers doivent être idempotents");

            var historyFinal = await IntegrationSqlTestSupport.ReadMigrationHistoryAsync(testCs, CancellationToken.None);
            historyFinal.Should().Equal(ReinstallTestSqlSupport.InitialCreateMigrationId);
            _output.WriteLine("__EFMigrationsHistory final: " + string.Join(", ", historyFinal));

            await AssertCriticalStructuresAsync(testCs);
            await AssertCoursesIndexIsTenantScopedAsync(testCs);
            await AssertCoursesGlobalIndexIsReplacedWhenPresentAsync(testCs);
            await AssertEfPendingMigrationsRemainArtifactsAsync(testCs);
        }
        finally
        {
            await IntegrationSqlTestSupport.DropDatabaseAsync(
                masterCs,
                IntegrationSqlTestSupport.FreshInstallDatabaseName,
                CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ApplyCriticalSchemaUpgrades_is_idempotent_on_baseline_database()
    {
        if (!IntegrationSqlTestSupport.TryResolveSql(out var source, out var skipReason))
        {
            _output.WriteLine("SKIP: " + skipReason);
            return;
        }

        var masterCs = IntegrationSqlTestSupport.BuildMasterConnectionString(source);
        var testCs = IntegrationSqlTestSupport.BuildDatabaseConnectionString(
            source,
            IntegrationSqlTestSupport.FreshInstallDatabaseName);

        try
        {
            await IntegrationSqlTestSupport.RecreateDatabaseAsync(
                masterCs,
                IntegrationSqlTestSupport.FreshInstallDatabaseName,
                CancellationToken.None);
            await IntegrationSqlTestSupport.ApplyBaselineAsync(testCs, CancellationToken.None);

            await InstallerEngine.ApplyCriticalSchemaUpgradesAsync(testCs, _ => { }, CancellationToken.None);
            await InstallerEngine.ApplyCriticalSchemaUpgradesAsync(testCs, _ => { }, CancellationToken.None);

            (await IntegrationSqlTestSupport.ScalarAsync(testCs,
                "SELECT CASE WHEN OBJECT_ID(N'dbo.RegistrationNumberCounters', N'U') IS NULL THEN 0 ELSE 1 END",
                CancellationToken.None)).Should().Be(1);
        }
        finally
        {
            await IntegrationSqlTestSupport.DropDatabaseAsync(
                masterCs,
                IntegrationSqlTestSupport.FreshInstallDatabaseName,
                CancellationToken.None);
        }
    }

    private static async Task AssertCriticalStructuresAsync(string connectionString)
    {
        (await IntegrationSqlTestSupport.ScalarAsync(connectionString,
            "SELECT CASE WHEN OBJECT_ID(N'dbo.Schools', N'U') IS NULL THEN 0 ELSE 1 END",
            CancellationToken.None)).Should().Be(1);

        (await IntegrationSqlTestSupport.ScalarAsync(connectionString,
            "SELECT CASE WHEN OBJECT_ID(N'dbo.RegistrationNumberCounters', N'U') IS NULL THEN 0 ELSE 1 END",
            CancellationToken.None)).Should().Be(1);

        (await IntegrationSqlTestSupport.ScalarAsync(connectionString,
            "SELECT CASE WHEN OBJECT_ID(N'dbo.Branches', N'U') IS NULL THEN 0 ELSE 1 END",
            CancellationToken.None)).Should().Be(1);

        (await IntegrationSqlTestSupport.ScalarAsync(connectionString,
            "SELECT CASE WHEN OBJECT_ID(N'dbo.SyncOutboxUnit', N'U') IS NULL THEN 0 ELSE 1 END",
            CancellationToken.None)).Should().Be(1);

        (await IntegrationSqlTestSupport.ScalarAsync(connectionString,
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RegistrationNumberCounters_SchoolId_Year' AND object_id = OBJECT_ID(N'dbo.RegistrationNumberCounters')) THEN 1 ELSE 0 END",
            CancellationToken.None)).Should().Be(1);

        (await IntegrationSqlTestSupport.ScalarAsync(connectionString,
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserRoleAssignments_UserId_RoleId' AND object_id = OBJECT_ID(N'dbo.UserRoleAssignments') AND has_filter = 1) THEN 1 ELSE 0 END",
            CancellationToken.None)).Should().Be(1);
    }

    private static async Task AssertCoursesIndexIsTenantScopedAsync(string connectionString)
    {
        var indexes = await IntegrationSqlTestSupport.ReadCourseIndexesAsync(connectionString, CancellationToken.None);
        indexes.Select(i => i.Name).Should().NotContain("IX_Courses_Code");
        indexes.Should().Contain(i =>
            i.Name == "IX_Courses_SchoolId_Code_ClassRoomId"
            && i.IsUnique
            && i.Filter != null
            && i.Filter.Contains("ClassRoomId", StringComparison.OrdinalIgnoreCase)
            && i.Filter.Contains("IsDeleted", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task AssertCoursesGlobalIndexIsReplacedWhenPresentAsync(string connectionString)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Courses_Code' AND object_id = OBJECT_ID(N'dbo.Courses'))
                BEGIN
                    CREATE UNIQUE INDEX [IX_Courses_Code] ON [Courses] ([Code]) WHERE [IsDeleted] = 0;
                END
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        await IntegrationSqlTestSupport.ApplyCurriculumSchemaAsync(connectionString, CancellationToken.None);
        await AssertCoursesIndexIsTenantScopedAsync(connectionString);
    }

    private static async Task AssertEfPendingMigrationsRemainArtifactsAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var context = new SchoolDbContext(options);
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        pending.Should().NotBeEmpty(
            "les migrations EF post-baseline restent des artefacts : le Setup n'appelle pas Database.Migrate()");
        pending.Should().Contain("20260812220000_AddRegistrationNumberCounters");
    }
}
