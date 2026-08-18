using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.CloudSync.DTOs;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace SchoolManagement.Setup.UnitTests;

/// <summary>
/// Scénario bout en bout : deux écoles locales, même Code cours, sync réelle via CloudSyncEngine.
/// </summary>
public sealed class CloudSyncMultiSchoolCoursesIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public CloudSyncMultiSchoolCoursesIntegrationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Two_schools_with_same_course_code_sync_to_cloud_without_cross_tenant_remapping()
    {
        if (!IntegrationSqlTestSupport.TryResolveSql(out var source, out var skipReason))
        {
            _output.WriteLine("SKIP: " + skipReason);
            return;
        }

        var masterCs = IntegrationSqlTestSupport.BuildMasterConnectionString(source);
        var cloudCs = IntegrationSqlTestSupport.BuildDatabaseConnectionString(
            source,
            IntegrationSqlTestSupport.CloudSyncCloudDatabaseName);
        var localACs = IntegrationSqlTestSupport.BuildDatabaseConnectionString(
            source,
            IntegrationSqlTestSupport.CloudSyncLocalADatabaseName);
        var localBCs = IntegrationSqlTestSupport.BuildDatabaseConnectionString(
            source,
            IntegrationSqlTestSupport.CloudSyncLocalBDatabaseName);

        var schoolA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var schoolB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var branchA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
        var branchB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbb001");
        var courseA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0aaa");
        var courseB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        string? configDir = null;
        try
        {
            await IntegrationSqlTestSupport.RecreateDatabaseAsync(
                masterCs, IntegrationSqlTestSupport.CloudSyncCloudDatabaseName, CancellationToken.None);
            await IntegrationSqlTestSupport.RecreateDatabaseAsync(
                masterCs, IntegrationSqlTestSupport.CloudSyncLocalADatabaseName, CancellationToken.None);
            await IntegrationSqlTestSupport.RecreateDatabaseAsync(
                masterCs, IntegrationSqlTestSupport.CloudSyncLocalBDatabaseName, CancellationToken.None);

            await IntegrationSqlTestSupport.ApplyFreshInstallSchemaAsync(cloudCs, CancellationToken.None);
            await IntegrationSqlTestSupport.ApplyFreshInstallSchemaAsync(localACs, CancellationToken.None);
            await IntegrationSqlTestSupport.ApplyFreshInstallSchemaAsync(localBCs, CancellationToken.None);

            await SeedLocalTenantAsync(localACs, schoolA, branchA, courseA, "École A Test", "HUM-A", "Géographie A");
            await SeedLocalTenantAsync(localBCs, schoolB, branchB, courseB, "École B Test", "HUM-B", "Géographie B");

            await EnqueueEntitySyncAsync(localACs, schoolA, branchA, courseA);
            await EnqueueEntitySyncAsync(localBCs, schoolB, branchB, courseB);

            configDir = CreateCloudConfigDirectory(cloudCs);
            var drainA = await DrainLocalOutboxAsync(localACs, configDir, maxUnits: 20);
            var errorsA = await ReadOutboxErrorsAsync(localACs);
            var drainB = await DrainLocalOutboxAsync(localBCs, configDir, maxUnits: 20);
            var errorsB = await ReadOutboxErrorsAsync(localBCs);

            drainA.Skipped.Should().BeFalse($"{drainA.Message}\n{errorsA}");
            drainB.Skipped.Should().BeFalse($"{drainB.Message}\n{errorsB}");
            drainA.Success.Should().BeTrue($"{drainA.Message}\n{errorsA}");
            drainB.Success.Should().BeTrue($"{drainB.Message}\n{errorsB}");
            _output.WriteLine($"Drain A: {drainA.Message}");
            _output.WriteLine($"Drain B: {drainB.Message}");

            await using (var cloud = CreateContext(cloudCs))
            {
                var courses = await cloud.Courses
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(c => c.Code == "HUM-GEO")
                    .ToListAsync();

                courses.Should().HaveCount(2);
                courses.Select(c => c.SchoolId).Should().BeEquivalentTo([schoolA, schoolB]);
                courses.Single(c => c.Id == courseA).Name.Should().Be("Géographie A");
                courses.Single(c => c.Id == courseB).Name.Should().Be("Géographie B");

                var branches = await cloud.Branches
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(b => b.Id == branchA || b.Id == branchB)
                    .ToListAsync();
                branches.Should().HaveCount(2);
                branches.Single(b => b.Id == branchA).SchoolId.Should().Be(schoolA);
                branches.Single(b => b.Id == branchB).SchoolId.Should().Be(schoolB);
            }

            // Modification uniquement École A
            await using (var cn = new SqlConnection(localACs))
            {
                await cn.OpenAsync();
                await using var cmd = cn.CreateCommand();
                cmd.CommandText = """
                    UPDATE Courses
                    SET Name = N'Géographie A modifiée', UpdatedAt = SYSUTCDATETIME()
                    WHERE Id = @courseId
                    """;
                cmd.Parameters.AddWithValue("@courseId", courseA);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var localA = CreateContext(localACs))
            {
                await EnqueueChangesAsync(localA, schoolA,
                [
                    new CloudSyncChange("Courses", courseA, SyncOperationType.Update, "Entity", courseA, SyncPriority.Normal),
                ]);
            }

            var drainUpdate = await DrainLocalOutboxAsync(localACs, configDir, maxUnits: 10);
            drainUpdate.Success.Should().BeTrue(drainUpdate.Message);

            await using (var cloud = CreateContext(cloudCs))
            {
                var courseCount = await cloud.Courses
                    .IgnoreQueryFilters()
                    .CountAsync(c => c.Code == "HUM-GEO");
                courseCount.Should().Be(2, "aucune ligne Course supplémentaire ne doit être créée");

                (await cloud.Courses.IgnoreQueryFilters().SingleAsync(c => c.Id == courseA)).Name
                    .Should().Be("Géographie A modifiée");
                (await cloud.Courses.IgnoreQueryFilters().SingleAsync(c => c.Id == courseB)).Name
                    .Should().Be("Géographie B");

                var indexes = await IntegrationSqlTestSupport.ReadCourseIndexesAsync(cloudCs, CancellationToken.None);
                indexes.Select(i => i.Name).Should().NotContain("IX_Courses_Code");
            }

            await using (var localA = CreateContext(localACs))
            {
                var pending = await localA.SyncOutboxUnits
                    .CountAsync(u => !u.IsDeleted && u.Status != SyncOutboxStatus.Completed);
                pending.Should().Be(0, "l'outbox locale A doit être vidée");
            }
        }
        finally
        {
            if (configDir != null && Directory.Exists(configDir))
            {
                try { Directory.Delete(configDir, recursive: true); } catch { /* ignore */ }
            }

            await IntegrationSqlTestSupport.DropDatabaseAsync(
                masterCs, IntegrationSqlTestSupport.CloudSyncCloudDatabaseName, CancellationToken.None);
            await IntegrationSqlTestSupport.DropDatabaseAsync(
                masterCs, IntegrationSqlTestSupport.CloudSyncLocalADatabaseName, CancellationToken.None);
            await IntegrationSqlTestSupport.DropDatabaseAsync(
                masterCs, IntegrationSqlTestSupport.CloudSyncLocalBDatabaseName, CancellationToken.None);
        }
    }

    private static async Task SeedLocalTenantAsync(
        string connectionString,
        Guid schoolId,
        Guid branchId,
        Guid courseId,
        string schoolName,
        string branchCode,
        string courseName)
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Schools (Id, Name, Country, DefaultCurrency, IsActive, CreatedAt, IsDeleted)
            VALUES (@schoolId, @schoolName, N'RDC', 0, 1, SYSUTCDATETIME(), 0);

            INSERT INTO Branches (Id, SchoolId, Code, Name, IsActive, CreatedAt, IsDeleted)
            VALUES (@branchId, @schoolId, @branchCode, @branchCode, 1, SYSUTCDATETIME(), 0);

            INSERT INTO Courses (Id, SchoolId, BranchId, Code, Name, Coefficient, MaxScore, IsOptional, CreatedAt, IsDeleted)
            VALUES (@courseId, @schoolId, @branchId, N'HUM-GEO', @courseName, 1, 20, 0, SYSUTCDATETIME(), 0);
            """;
        cmd.Parameters.AddWithValue("@schoolId", schoolId);
        cmd.Parameters.AddWithValue("@schoolName", schoolName);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@branchCode", branchCode);
        cmd.Parameters.AddWithValue("@courseId", courseId);
        cmd.Parameters.AddWithValue("@courseName", courseName);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnqueueEntitySyncAsync(
        string connectionString,
        Guid schoolId,
        Guid branchId,
        Guid courseId)
    {
        await using var db = CreateContext(connectionString);
        db.OverrideTenantSchoolId = schoolId;
        db.SuppressCloudSyncEnqueue = true;
        db.IgnoreSchoolScope = true;

        var unit = new SyncOutboxUnit
        {
            SchoolId = schoolId,
            AggregateType = "EntityBatch",
            AggregateId = courseId,
            Priority = SyncPriority.Normal,
            Status = SyncOutboxStatus.Pending,
            ExpectedItemCount = 3,
            CreatedAt = DateTime.UtcNow,
        };
        unit.Items.Add(new SyncOutboxItem
        {
            TableName = "Schools",
            EntityId = schoolId,
            Operation = SyncOperationType.Insert,
            Status = SyncOutboxStatus.Pending,
            Sequence = 0,
            CreatedAt = DateTime.UtcNow,
        });
        unit.Items.Add(new SyncOutboxItem
        {
            TableName = "Branches",
            EntityId = branchId,
            Operation = SyncOperationType.Insert,
            Status = SyncOutboxStatus.Pending,
            Sequence = 1,
            CreatedAt = DateTime.UtcNow,
        });
        unit.Items.Add(new SyncOutboxItem
        {
            TableName = "Courses",
            EntityId = courseId,
            Operation = SyncOperationType.Insert,
            Status = SyncOutboxStatus.Pending,
            Sequence = 2,
            CreatedAt = DateTime.UtcNow,
        });
        db.SyncOutboxUnits.Add(unit);
        await db.SaveChangesAsync();
    }

    private static async Task EnqueueChangesAsync(
        SchoolDbContext db,
        Guid schoolId,
        IReadOnlyList<CloudSyncChange> changes)
    {
        db.OverrideTenantSchoolId = schoolId;
        var writer = new CloudSyncOutboxWriter();
        await writer.EnqueueAsync(db, changes, CancellationToken.None);
    }

    private static async Task<CloudSyncRunResultDto> DrainLocalOutboxAsync(
        string localConnectionString,
        string cloudConfigDirectory,
        int maxUnits)
    {
        var encryption = new EncryptionService();
        var factory = new DatabaseConnectionFactory();
        var cloudMgr = new CloudDatabaseConfigurationManager(cloudConfigDirectory, encryption);

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IEncryptionService>(encryption);
        services.AddSingleton(factory);
        services.AddSingleton(cloudMgr);
        services.AddDbContext<SchoolDbContext>(opts =>
            opts.UseSqlServer(localConnectionString, sql => sql.CommandTimeout(60)));
        services.AddScoped<ICloudSyncEngine, CloudSyncEngine>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<ICloudSyncEngine>();
        return await engine.DrainAsync(
            criticalOnly: false,
            maxUnits: maxUnits,
            control: new CloudSyncDrainControl(
                BypassActif: true,
                PendingOnly: true,
                VerifyCloudAfterCommit: true,
                RetryPendingOnDependencyError: true),
            cancellationToken: CancellationToken.None);
    }

    private static string CreateCloudConfigDirectory(string cloudConnectionString)
    {
        var dir = Path.Combine(Path.GetTempPath(), "erp-cloudsync-integ-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var builder = new SqlConnectionStringBuilder(cloudConnectionString);
        var (tcpHost, tcpPort) = ResolveTcpEndpoint(cloudConnectionString);
        var encryption = new EncryptionService();
        var mgr = new CloudDatabaseConfigurationManager(dir, encryption);
        mgr.SaveConfiguration(new CloudDatabaseConfiguration
        {
            Actif = true,
            Serveur = tcpHost,
            Port = tcpPort,
            Base = builder.InitialCatalog,
            Utilisateur = builder.IntegratedSecurity ? string.Empty : builder.UserID,
            Authentification = builder.IntegratedSecurity
                ? DatabaseAuthenticationMode.Windows
                : DatabaseAuthenticationMode.SqlServer,
            IntervalleMinutes = 5,
        }, builder.IntegratedSecurity ? string.Empty : builder.Password);

        return dir;
    }

    private static (string Host, int Port) ResolveTcpEndpoint(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!builder.DataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            builder.DataSource = "tcp:" + builder.DataSource;
        }

        using var cn = new SqlConnection(builder.ConnectionString);
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT local_net_address, local_tcp_port
            FROM sys.dm_exec_connections
            WHERE session_id = @@SPID
            """;
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("Impossible de résoudre le point de terminaison TCP SQL.");
        }

        var address = reader.IsDBNull(0) ? null : reader.GetString(0);
        if (reader.IsDBNull(1))
        {
            throw new InvalidOperationException("Port TCP SQL indisponible (connexion non-TCP).");
        }

        var port = reader.GetInt32(1);
        var host = address switch
        {
            null or "<local machine>" or "127.0.0.1" or "::1" => "127.0.0.1",
            _ => address,
        };
        return (host, port);
    }

    private static async Task<string> ReadOutboxErrorsAsync(string connectionString)
    {
        await using var db = CreateContext(connectionString);
        db.IgnoreSchoolScope = true;
        var units = await db.SyncOutboxUnits
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.Status != SyncOutboxStatus.Completed)
            .Select(u => new { u.Id, u.Status, u.LastError })
            .ToListAsync();
        return units.Count == 0
            ? "(aucune unité non Completed)"
            : string.Join(Environment.NewLine, units.Select(u => $"{u.Id} {u.Status}: {u.LastError}"));
    }

    private static SchoolDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(60))
            .Options;
        return new SchoolDbContext(options);
    }
}
