using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.UnitTests.CloudSync;

public sealed class SyncSqlFactAttribute : FactAttribute
{
    public SyncSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SCHOOL_SYNC_TEST_SQL")))
            Skip = "Définir SCHOOL_SYNC_TEST_SQL pour tester dans une base SQL jetable distincte des bases métier.";
    }
}

public sealed class CloudSyncTariffSqlTests
{
    [SyncSqlFact]
    public async Task Registration_counter_initialization_skips_orphan_school_ids_without_rewriting_students_or_existing_counters()
    {
        await using var db = await ScratchDatabase.CreateAsync();
        var schoolId = Guid.NewGuid();
        var orphanId = Guid.NewGuid();
        await db.ExecuteAsync($"""
            CREATE TABLE dbo.Schools (Id uniqueidentifier NOT NULL PRIMARY KEY);
            CREATE TABLE dbo.Students (Id uniqueidentifier NOT NULL PRIMARY KEY,
                SchoolId uniqueidentifier NOT NULL, RegistrationNumber nvarchar(80) NOT NULL);
            INSERT dbo.Schools VALUES ('{schoolId}');
            INSERT dbo.Students VALUES
                (NEWID(),'{schoolId}',N'ELV-2026-000042'),
                (NEWID(),'{schoolId}',N'ELV-2026-000003'),
                (NEWID(),'{orphanId}',N'ELV-2026-000999'),
                (NEWID(),'00000000-0000-0000-0000-000000000000',N'ELV-2026-000888'),
                (NEWID(),'{schoolId}',N'Ancien matricule');
            """);
        var before = await db.ScalarAsync<string>(
            "SELECT Id,SchoolId,RegistrationNumber FROM dbo.Students ORDER BY Id FOR JSON PATH");
        var initializer = new RegistrationNumberCounterSchemaInitializer(db.ConnectionString,
            NullLogger<RegistrationNumberCounterSchemaInitializer>.Instance);
        await initializer.EnsureCreatedAsync();
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.RegistrationNumberCounters")).Should().Be(1);
        (await db.ScalarAsync<int>("SELECT NextValue FROM dbo.RegistrationNumberCounters")).Should().Be(43);
        (await db.ScalarAsync<Guid>("SELECT SchoolId FROM dbo.RegistrationNumberCounters")).Should().Be(schoolId);
        await db.ExecuteAsync("UPDATE dbo.RegistrationNumberCounters SET NextValue=1000;");
        var counterId = await db.ScalarAsync<Guid>("SELECT Id FROM dbo.RegistrationNumberCounters");
        await initializer.EnsureCreatedAsync();
        (await db.ScalarAsync<int>("SELECT NextValue FROM dbo.RegistrationNumberCounters")).Should().Be(1000);
        (await db.ScalarAsync<Guid>("SELECT Id FROM dbo.RegistrationNumberCounters")).Should().Be(counterId);
        (await db.ScalarAsync<string>(
            "SELECT Id,SchoolId,RegistrationNumber FROM dbo.Students ORDER BY Id FOR JSON PATH")).Should().Be(before);
    }

    [SyncSqlFact]
    public async Task Empty_cloud_schema_and_destination_replay_survive_restart_without_changing_local_school()
    {
        await using var localDb = await ScratchDatabase.CreateAsync();
        await using var cloudDb = await ScratchDatabase.CreateAsync();
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(localDb.ConnectionString, sql => sql.EnableRetryOnFailure(3)).Options;
        await using var remote = new SchoolDbContext(new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(cloudDb.ConnectionString).Options) { SuppressCloudSyncEnqueue = true };
        var school = new School { Name = "École temporaire" };
        await using (var local = new SchoolDbContext(options) { SuppressCloudSyncEnqueue = true })
        {
            await CloudSyncDestinationSchema.EnsureEmptyCloudSchemaAsync(local);
            local.Add(school);
            await local.SaveChangesAsync();
        }
        // Le schéma courant doit pouvoir être créé dans une base SQL entièrement vide.
        (await CloudSyncDestinationSchema.EnsureEmptyCloudSchemaAsync(remote)).Should().BeTrue();
        (await CloudSyncDestinationSchema.EnsureEmptyCloudSchemaAsync(remote)).Should().BeFalse();
        await localDb.ExecuteAsync("""
            CREATE TRIGGER dbo.FailDestinationPreparation ON dbo.SyncDestination AFTER INSERT AS
            BEGIN
                THROW 51003, 'Échec volontaire du test de transaction', 1;
            END;
            """);
        await using (var interrupted = new SchoolDbContext(options) { SuppressCloudSyncEnqueue = true })
        {
            var prepare = () => CloudSyncEngine.PrepareDestinationAsync(interrupted, remote);
            await prepare.Should().ThrowAsync<DbUpdateException>();
        }
        (await localDb.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.SyncOutboxUnit")).Should().Be(0);
        (await localDb.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.SyncDestination")).Should().Be(0);
        await localDb.ExecuteAsync("DROP TRIGGER dbo.FailDestinationPreparation;");
        await using (var local = new SchoolDbContext(options) { SuppressCloudSyncEnqueue = true })
        {
            (await CloudSyncEngine.PrepareDestinationAsync(local, remote)).Should().BeTrue();
        }
        await using (var restarted = new SchoolDbContext(options) { SuppressCloudSyncEnqueue = true })
        {
            (await CloudSyncEngine.PrepareDestinationAsync(restarted, remote)).Should().BeFalse();
            var item = await restarted.Set<SchoolManagement.Domain.Entities.Sync.SyncOutboxItem>()
                .SingleAsync(x => x.TableName == "Schools");
            await CloudSyncEngine.ApplyTypedItemAsync<School>(restarted, remote, item, default);
            await remote.SaveChangesAsync();
            (await restarted.Schools.SingleAsync()).Name.Should().Be(school.Name);
        }
        (await remote.Schools.SingleAsync()).Id.Should().Be(school.Id);
        (await localDb.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.SyncDestination")).Should().Be(1);
    }

    [SyncSqlFact]
    public async Task Identity_schema_is_replayable_and_mapping_survives_new_local_context()
    {
        await using var db = await ScratchDatabase.CreateAsync();
        var initializer = new CloudSyncSchemaInitializer(db.ConnectionString,
            NullLogger<CloudSyncSchemaInitializer>.Instance);
        await initializer.EnsureCreatedAsync();
        await initializer.EnsureCreatedAsync();
        await using var remote = CloudSyncTariffIdentityTests.CreateContext();
        var tariff = CloudSyncTariffIdentityTests.CreateTariff();
        var cloud = CloudSyncTariffIdentityTests.CopyTariff(tariff);
        remote.Add(cloud);
        await remote.SaveChangesAsync();
        var options = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(db.ConnectionString).Options;
        await using (var local = new SchoolDbContext(options))
        {
            var copy = CloudSyncTariffIdentityTests.CopyTariff(tariff, tariff.Id);
            await CloudSyncNaturalKey.PrepareForCloudAsync(local, remote, copy, default);
            copy.Id.Should().Be(cloud.Id);
        }
        await using (var local = new SchoolDbContext(options))
        {
            var deleted = CloudSyncTariffIdentityTests.CopyTariff(tariff, tariff.Id);
            deleted.IsDeleted = true;
            await CloudSyncNaturalKey.PrepareForCloudAsync(local, remote, deleted, default);
            deleted.Id.Should().Be(cloud.Id);
        }
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.SyncEntityIdentity")).Should().Be(1);
    }

    [SyncSqlFact]
    public async Task Audit_migration_preserves_valid_values_and_is_idempotent()
    {
        await using var db = await ScratchDatabase.CreateAsync();
        await db.ExecuteAsync(AuditTableSql);
        var user = Guid.NewGuid();
        await db.ExecuteAsync($"INSERT dbo.EnrollmentPricingCategoryHistory VALUES (N'{user}', NULL, N'{user}');");
        await db.ExecuteAsync(EnrollmentPricingSchemaInitializer.AuditColumnsMigrationSql);
        await db.ExecuteAsync(EnrollmentPricingSchemaInitializer.AuditColumnsMigrationSql);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EnrollmentPricingCategoryHistory') AND system_type_id=TYPE_ID('uniqueidentifier')"))
            .Should().Be(3);
        (await db.ScalarAsync<Guid>("SELECT CreatedBy FROM dbo.EnrollmentPricingCategoryHistory")).Should().Be(user);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.EnrollmentPricingCategoryHistory WHERE UpdatedBy IS NULL AND DeletedBy=CreatedBy"))
            .Should().Be(1);
    }

    [SyncSqlFact]
    public async Task Audit_migration_rolls_back_all_columns_on_invalid_value()
    {
        await using var db = await ScratchDatabase.CreateAsync();
        await db.ExecuteAsync(AuditTableSql);
        await db.ExecuteAsync($"INSERT dbo.EnrollmentPricingCategoryHistory VALUES (N'{Guid.NewGuid()}', N'not-a-guid', NULL);");
        var migrate = () => db.ExecuteAsync(EnrollmentPricingSchemaInitializer.AuditColumnsMigrationSql);
        await migrate.Should().ThrowAsync<SqlException>().Where(x => x.Number == 51001);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.EnrollmentPricingCategoryHistory') AND system_type_id=TYPE_ID('nvarchar')"))
            .Should().Be(3);
        (await db.ScalarAsync<string>("SELECT UpdatedBy FROM dbo.EnrollmentPricingCategoryHistory")).Should().Be("not-a-guid");
    }

    [SyncSqlFact]
    public async Task Sql_unique_indexes_and_balance_foreign_key_survive_repeated_sync_with_different_ids()
    {
        await using var db = await ScratchDatabase.CreateAsync();
        await db.ExecuteAsync(FinanceTablesSql);
        await using var localDb = await ScratchDatabase.CreateAsync();
        await localDb.ExecuteAsync(FinanceTablesSql);
        await new CloudSyncSchemaInitializer(localDb.ConnectionString,
            NullLogger<CloudSyncSchemaInitializer>.Instance).EnsureCreatedAsync();
        await using var local = new SchoolDbContext(new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(localDb.ConnectionString).Options) { SuppressCloudSyncEnqueue = true };
        await using var remote = new SchoolDbContext(new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(db.ConnectionString).Options) { SuppressCloudSyncEnqueue = true };
        var tariff = CloudSyncTariffIdentityTests.CreateTariff();
        var cloudTariff = CloudSyncTariffIdentityTests.CopyTariff(tariff);
        var balance = new StudentFeeBalance { StudentId = Guid.NewGuid(), ClassFeeAmountId = tariff.Id,
            AmountDue = 40, AmountPaid = 15 };
        var cloudBalance = new StudentFeeBalance { StudentId = balance.StudentId,
            ClassFeeAmountId = cloudTariff.Id, AmountDue = 40 };
        local.AddRange(tariff, balance);
        remote.AddRange(cloudTariff, cloudBalance);
        await local.SaveChangesAsync();
        await remote.SaveChangesAsync();
        remote.ChangeTracker.Clear();

        for (var i = 0; i < 2; i++)
        {
            await CloudSyncEngine.UpsertAllAsync<ClassFeeAmount>(local, remote, default);
            await using var tx = await remote.Database.BeginTransactionAsync();
            var tariffItem = CloudSyncTariffIdentityTests.Item("ClassFeeAmounts", tariff.Id);
            var balanceItem = CloudSyncTariffIdentityTests.Item("StudentFeeBalances", balance.Id);
            await CloudSyncEngine.ApplyTypedItemAsync<ClassFeeAmount>(local, remote, tariffItem, default);
            await remote.SaveChangesAsync();
            remote.ChangeTracker.Clear();
            await CloudSyncEngine.ApplyTypedItemAsync<StudentFeeBalance>(local, remote, balanceItem, default);
            await remote.SaveChangesAsync();
            remote.ChangeTracker.Clear();
            await tx.CommitAsync();
            (await CloudSyncEngine.CountAppliedItemOnCloudAsync(local, remote, tariffItem, default)).Should().Be(1);
            (await CloudSyncEngine.CountAppliedItemOnCloudAsync(local, remote, balanceItem, default)).Should().Be(1);
        }

        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.ClassFeeAmounts")).Should().Be(1);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.StudentFeeBalances")).Should().Be(1);
        (await db.ScalarAsync<Guid>("SELECT ClassFeeAmountId FROM dbo.StudentFeeBalances")).Should().Be(cloudTariff.Id);
        (await db.ScalarAsync<decimal>("SELECT AmountDue FROM dbo.StudentFeeBalances")).Should().Be(40);
        (await db.ScalarAsync<decimal>("SELECT AmountPaid FROM dbo.StudentFeeBalances")).Should().Be(15);
        (await local.Set<ClassFeeAmount>().IgnoreQueryFilters().SingleAsync()).Id.Should().Be(tariff.Id);
    }

    private const string AuditTableSql = """
        CREATE TABLE dbo.EnrollmentPricingCategoryHistory (
            CreatedBy nvarchar(256) NULL, UpdatedBy nvarchar(256) NULL, DeletedBy nvarchar(256) NULL);
        """;

    private const string FinanceTablesSql = """
        CREATE TABLE dbo.ClassFeeAmounts (
            Id uniqueidentifier NOT NULL PRIMARY KEY, SchoolId uniqueidentifier NOT NULL,
            AcademicYearId uniqueidentifier NOT NULL, PedagogicalClassId uniqueidentifier NOT NULL,
            FeePricingCategoryId uniqueidentifier NOT NULL, FeeTypeId uniqueidentifier NOT NULL,
            FeeInstallmentId uniqueidentifier NOT NULL, Amount decimal(18,2) NOT NULL,
            DueDate date NULL, SortOrder int NOT NULL DEFAULT(0),
            CreatedAt datetime2 NOT NULL, CreatedBy uniqueidentifier NULL,
            UpdatedAt datetime2 NULL, UpdatedBy uniqueidentifier NULL,
            IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy uniqueidentifier NULL);
        CREATE UNIQUE INDEX IX_ClassFeeAmounts_Year_Class_Category_FeeType_Installment
            ON dbo.ClassFeeAmounts (AcademicYearId,PedagogicalClassId,FeePricingCategoryId,FeeTypeId,FeeInstallmentId)
            WHERE IsDeleted=0 AND FeePricingCategoryId IS NOT NULL;
        CREATE TABLE dbo.StudentFeeBalances (
            Id uniqueidentifier NOT NULL PRIMARY KEY, StudentId uniqueidentifier NOT NULL,
            ClassFeeAmountId uniqueidentifier NOT NULL REFERENCES dbo.ClassFeeAmounts(Id),
            AmountDue decimal(18,2) NOT NULL, AmountPaid decimal(18,2) NOT NULL, Currency int NOT NULL,
            CreatedAt datetime2 NOT NULL, CreatedBy uniqueidentifier NULL,
            UpdatedAt datetime2 NULL, UpdatedBy uniqueidentifier NULL,
            IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy uniqueidentifier NULL);
        CREATE UNIQUE INDEX IX_StudentFeeBalances_StudentId_ClassFeeAmountId
            ON dbo.StudentFeeBalances(StudentId,ClassFeeAmountId) WHERE IsDeleted=0;
        """;

    internal sealed class ScratchDatabase : IAsyncDisposable
    {
        private readonly string _master;
        private readonly string _name = "Codex_SyncTest_" + Guid.NewGuid().ToString("N");
        public string ConnectionString { get; }
        private ScratchDatabase(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
            _master = builder.ConnectionString;
            builder.InitialCatalog = _name;
            ConnectionString = builder.ConnectionString;
        }

        public static async Task<ScratchDatabase> CreateAsync()
        {
            var db = new ScratchDatabase(Environment.GetEnvironmentVariable("SCHOOL_SYNC_TEST_SQL")!);
            await using var cn = new SqlConnection(db._master);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{db._name}]";
            await cmd.ExecuteNonQueryAsync();
            return db;
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var cn = new SqlConnection(ConnectionString);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var cn = new SqlConnection(ConnectionString);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            return (T)(await cmd.ExecuteScalarAsync())!;
        }

        public async ValueTask DisposeAsync()
        {
            SqlConnection.ClearAllPools();
            await using var cn = new SqlConnection(_master);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}];";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
