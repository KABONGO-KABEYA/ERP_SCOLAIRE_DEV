using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UnitTests.Updates;

public sealed class MigrationManagerTests
{
    private static string FixturesRoot =>
        Path.Combine(AppContext.BaseDirectory, "Updates", "Fixtures");

    [Fact]
    public void SplitBatches_Splits_On_GO()
    {
        var batches = MigrationManager.SplitBatches("""
            CREATE TABLE dbo.T (Id INT);
            GO
            ALTER TABLE dbo.T ADD Name NVARCHAR(20) NULL;
            GO
            """);

        batches.Should().HaveCount(2);
        batches[0].Should().Contain("CREATE TABLE");
        batches[1].Should().Contain("ALTER TABLE");
    }

    [Fact]
    public void XactAbort_Sql_Is_Set_On()
    {
        MigrationManager.XactAbortSql.Should().Be("SET XACT_ABORT ON;");
    }

    [Fact]
    public void Package_Refuses_Remote_Uri()
    {
        var act = () => MigrationPackage.Load("https://example.com/migrations");
        act.Should().Throw<MigrationException>().WithMessage("*local*");
    }

    [Fact]
    public void Package_Refuses_1_To_3_Without_1_2()
    {
        var dir = CreateTempPackage(new MigrationManifest
        {
            SchemaVersion = 3,
            FromSchemaVersion = 1,
            ToSchemaVersion = 3,
            Migrations = ["Migration1_2.sql", "Migration2_3.sql"],
        });
        File.WriteAllText(Path.Combine(dir, "Migration2_3.sql"), "SELECT 1;");
        var act = () => MigrationPackage.Load(dir);
        act.Should().Throw<MigrationException>().WithMessage("*Migration1_2.sql*");
    }

    [Fact]
    public void Package_Refuses_1_To_3_Without_2_3()
    {
        var dir = CreateTempPackage(new MigrationManifest
        {
            SchemaVersion = 3,
            FromSchemaVersion = 1,
            ToSchemaVersion = 3,
            Migrations = ["Migration1_2.sql", "Migration2_3.sql"],
        });
        File.Copy(Sql("Migration1_2.sql"), Path.Combine(dir, "Migration1_2.sql"));
        var act = () => MigrationPackage.Load(dir);
        act.Should().Throw<MigrationException>().WithMessage("*Migration2_3.sql*");
    }

    [Fact]
    public void Package_Refuses_Manifest_Skipping_Step()
    {
        var dir = CreateTempPackage(new MigrationManifest
        {
            SchemaVersion = 3,
            FromSchemaVersion = 1,
            ToSchemaVersion = 3,
            Migrations = ["Migration1_3.sql"],
        });
        var act = () => MigrationPackage.Load(dir);
        act.Should().Throw<MigrationException>().WithMessage("*chaîne*");
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Apply_1_To_1_Is_NoOp()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var manager = new MigrationManager(db.ConnectionString);
        (await manager.GetSchemaVersionAsync()).Should().Be(1);

        var dir = CreateTempPackage(new MigrationManifest
        {
            SchemaVersion = 1,
            FromSchemaVersion = 1,
            ToSchemaVersion = 1,
            Migrations = [],
        });
        var result = await manager.ApplyPackageAsync(dir);
        result.PreviousVersion.Should().Be(1);
        result.CurrentVersion.Should().Be(1);
        result.AppliedMigrations.Should().BeEmpty();
        (await manager.GetSchemaVersionAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Apply_1_To_2_Runs_Migration1_2()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var manager = new MigrationManager(db.ConnectionString);
        var dir = CreateChainPackage(from: 1, to: 2);
        var result = await manager.ApplyPackageAsync(dir);
        result.AppliedMigrations.Should().Equal("Migration1_2.sql");
        result.CurrentVersion.Should().Be(2);
        (await db.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Lot2B1_Probe")).Should().Be(1);
        (await db.ScalarAsync<string>("SELECT Step FROM dbo.Lot2B1_Probe WHERE Id = 1")).Should().Be("after-1-2");
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Apply_1_To_3_Runs_1_2_Then_2_3()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var manager = new MigrationManager(db.ConnectionString);
        var result = await manager.ApplyPackageAsync(Chain13Package());
        result.AppliedMigrations.Should().Equal("Migration1_2.sql", "Migration2_3.sql");
        result.CurrentVersion.Should().Be(3);
        (await db.ScalarAsync<string>("SELECT Step3 FROM dbo.Lot2B1_Probe WHERE Id = 1")).Should().Be("after-2-3");
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Apply_2_To_3_Runs_Only_Migration2_3()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var manager = new MigrationManager(db.ConnectionString);
        await manager.ApplyPackageAsync(CreateChainPackage(1, 2));
        var result = await manager.ApplyPackageAsync(CreateChainPackage(2, 3));
        result.PreviousVersion.Should().Be(2);
        result.AppliedMigrations.Should().Equal("Migration2_3.sql");
        result.CurrentVersion.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Apply_3_To_2_Is_Refused()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var manager = new MigrationManager(db.ConnectionString);
        await manager.ApplyPackageAsync(Chain13Package());
        var dir = CreateChainPackage(1, 2);
        var act = async () => await manager.ApplyPackageAsync(dir);
        (await act.Should().ThrowAsync<MigrationException>()).WithMessage("*inférieure*");
        (await manager.GetSchemaVersionAsync()).Should().Be(3);
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Missing_Migration1_2_Is_Refused_Before_Sql()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var dir = CreateTempPackage(new MigrationManifest
        {
            SchemaVersion = 2,
            FromSchemaVersion = 1,
            ToSchemaVersion = 2,
            Migrations = ["Migration1_2.sql"],
        });
        var manager = new MigrationManager(db.ConnectionString);
        var act = async () => await manager.ApplyPackageAsync(dir);
        (await act.Should().ThrowAsync<MigrationException>()).WithMessage("*Migration1_2.sql*");
        (await manager.GetSchemaVersionAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Missing_Migration2_3_Is_Refused_Before_Sql()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var dir = CreateTempPackage(new MigrationManifest
        {
            SchemaVersion = 3,
            FromSchemaVersion = 1,
            ToSchemaVersion = 3,
            Migrations = ["Migration1_2.sql", "Migration2_3.sql"],
        });
        File.Copy(Sql("Migration1_2.sql"), Path.Combine(dir, "Migration1_2.sql"));
        var manager = new MigrationManager(db.ConnectionString);
        var act = async () => await manager.ApplyPackageAsync(dir);
        (await act.Should().ThrowAsync<MigrationException>()).WithMessage("*Migration2_3.sql*");
        (await manager.GetSchemaVersionAsync()).Should().Be(1);
        (await db.TableExistsAsync("Lot2B1_Probe")).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Failed_Migration1_2_RollsBack_Version_Stays_1()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var dir = CreateTempPackage(new MigrationManifest
        {
            SchemaVersion = 2,
            FromSchemaVersion = 1,
            ToSchemaVersion = 2,
            Migrations = ["Migration1_2.sql"],
        });
        File.Copy(Sql("Migration1_2_Fail.sql"), Path.Combine(dir, "Migration1_2.sql"), overwrite: true);
        var manager = new MigrationManager(db.ConnectionString);
        var act = async () => await manager.ApplyPackageAsync(dir);
        await act.Should().ThrowAsync<MigrationException>();
        (await manager.GetSchemaVersionAsync()).Should().Be(1);
        (await db.TableExistsAsync("Lot2B1_Fail12")).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Failed_Migration2_3_Keeps_Version_2()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var manager = new MigrationManager(db.ConnectionString);
        await manager.ApplyPackageAsync(CreateChainPackage(1, 2));

        var dir = CreateTempPackage(new MigrationManifest
        {
            SchemaVersion = 3,
            FromSchemaVersion = 2,
            ToSchemaVersion = 3,
            Migrations = ["Migration2_3.sql"],
        });
        File.Copy(Sql("Migration2_3_Fail.sql"), Path.Combine(dir, "Migration2_3.sql"), overwrite: true);
        var act = async () => await manager.ApplyPackageAsync(dir);
        await act.Should().ThrowAsync<MigrationException>();
        (await manager.GetSchemaVersionAsync()).Should().Be(2);
        (await db.TableExistsAsync("Lot2B1_Probe")).Should().BeTrue();
        (await db.ColumnExistsAsync("Lot2B1_Probe", "FailCol")).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Restart_After_Commit_1_2_Can_Resume_2_3()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var first = new MigrationManager(db.ConnectionString);
        await first.ApplyPackageAsync(CreateChainPackage(1, 2));
        (await first.GetSchemaVersionAsync()).Should().Be(2);

        var resumed = new MigrationManager(db.ConnectionString);
        var result = await resumed.ApplyPackageAsync(Chain13Package());
        result.PreviousVersion.Should().Be(2);
        result.AppliedMigrations.Should().Equal("Migration2_3.sql");
        result.CurrentVersion.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Transactional_Ddl_And_GO_Apply_Together()
    {
        await using var db = await SqlMigrationDatabase.RequireAsync();
        var manager = new MigrationManager(db.ConnectionString);
        await manager.ApplyPackageAsync(Chain13Package());
        (await db.ColumnExistsAsync("Lot2B1_Probe", "Step3")).Should().BeTrue();
        MigrationManager.SplitBatches(await File.ReadAllTextAsync(Sql("Migration2_3.sql")))
            .Should().HaveCount(2);
    }

    [Fact]
    public void Baseline_Is_One()
    {
        MigrationManager.BaselineSchemaVersion.Should().Be(1);
    }

    private static string Sql(string fileName) => Path.Combine(FixturesRoot, "sql", fileName);

    private static string Chain13Package() => Path.Combine(FixturesRoot, "packages", "chain-1-3");

    private static string CreateChainPackage(int from, int to)
    {
        var names = new List<string>();
        for (var v = from; v < to; v++)
        {
            names.Add(MigrationManager.FileNameFor(v, v + 1));
        }

        var dir = CreateTempPackage(new MigrationManifest
        {
            SchemaVersion = to,
            FromSchemaVersion = from,
            ToSchemaVersion = to,
            Migrations = names,
        });
        foreach (var name in names)
        {
            File.Copy(Sql(name), Path.Combine(dir, name), overwrite: true);
        }

        return dir;
    }

    private static string CreateTempPackage(MigrationManifest manifest)
    {
        var dir = Path.Combine(Path.GetTempPath(), "lot2b1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });
        File.WriteAllText(Path.Combine(dir, MigrationPackage.ManifestFileName), json);
        return dir;
    }
}

internal sealed class SqlMigrationDatabase : IAsyncDisposable
{
    private static readonly string[] MasterCandidates =
    [
        @"Server=(localdb)\mssqllocaldb;Database=master;Trusted_Connection=True;TrustServerCertificate=True",
        @"Server=localhost\HEROS_SQL19;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True",
    ];

    private readonly string _masterConnectionString;
    private readonly string _databaseName;

    private SqlMigrationDatabase(string masterConnectionString, string databaseName, string connectionString)
    {
        _masterConnectionString = masterConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<SqlMigrationDatabase> RequireAsync()
    {
        var db = await TryCreateAsync();
        Assert.True(
            db is not null,
            "SQL Server requis pour Lot 2B-1 (LocalDB ou localhost\\HEROS_SQL19).");
        return db!;
    }

    public static async Task<SqlMigrationDatabase?> TryCreateAsync()
    {
        foreach (var master in MasterCandidates)
        {
            try
            {
                var name = "Lot2B1_Mig_" + Guid.NewGuid().ToString("N")[..12];
                await using (var cn = new SqlConnection(master))
                {
                    await cn.OpenAsync();
                    await using var cmd = cn.CreateCommand();
                    cmd.CommandText = $"CREATE DATABASE [{name}]";
                    await cmd.ExecuteNonQueryAsync();
                }

                var builder = new SqlConnectionStringBuilder(master) { InitialCatalog = name };
                return new SqlMigrationDatabase(master, name, builder.ConnectionString);
            }
            catch (SqlException)
            {
                // try next instance
            }
            catch (InvalidOperationException)
            {
            }
        }

        return null;
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var cn = new SqlConnection(ConnectionString);
        await cn.OpenAsync();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        var value = await cmd.ExecuteScalarAsync();
        return (T)Convert.ChangeType(value, typeof(T))!;
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        var count = await ScalarAsync<int>(
            $"SELECT CASE WHEN OBJECT_ID(N'dbo.{tableName}', N'U') IS NULL THEN 0 ELSE 1 END");
        return count == 1;
    }

    public async Task<bool> ColumnExistsAsync(string tableName, string columnName)
    {
        var count = await ScalarAsync<int>(
            $"SELECT CASE WHEN COL_LENGTH(N'dbo.{tableName}', N'{columnName}') IS NULL THEN 0 ELSE 1 END");
        return count == 1;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var cn = new SqlConnection(_masterConnectionString);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"""
                IF DB_ID(N'{_databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_databaseName}];
                END
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
