using FluentAssertions;
using Microsoft.Data.SqlClient;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UpdateAgent.Tests;

/// <summary>
/// Lot 2B-5A — restore réel depuis master sur la base isolée uniquement.
/// Ne jamais pointer vers SchoolManagementRDC / _Development / _Production.
/// </summary>
[Collection("UpdateIntegrationSql")]
public sealed class RestoreFromMasterIntegrationTests
{
    public const string IntegrationDatabase = "SchoolManagementRDC_UpdateIntegration";

    private const string SchoolCs =
        @"Server=localhost\HEROS_SQL19;Database=SchoolManagementRDC_UpdateIntegration;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";

    [Fact]
    public void Restore_Target_Is_Local_Config_Not_Bootstrap()
    {
        var builder = new SqlConnectionStringBuilder(SchoolCs);
        builder.InitialCatalog.Should().Be(IntegrationDatabase);
        SchoolBackupPathGuard.EnsureDatabaseName(builder.InitialCatalog);
        new SqlConnectionStringBuilder(SqlRestoreConnection.ToMaster(SchoolCs))
            .InitialCatalog.Should().Be("master");
    }

    [Fact]
    [Trait("Category", "LiveSql")]
    public async Task Backup_Migrate_Restore_From_Master_On_Integration_Database()
    {
        await using var opened = new SqlConnection(SchoolCs);
        await opened.OpenAsync();
        await using (var nameCmd = opened.CreateCommand())
        {
            nameCmd.CommandText = "SELECT DB_NAME()";
            Convert.ToString(await nameCmd.ExecuteScalarAsync()).Should().Be(IntegrationDatabase);
        }
        await ResetIntegrationAsync(opened);

        var sqlWritable = Path.Combine(
            @"D:\Mes Projet\ERP_Administration_Scolaire_2026\logs",
            "ua-2b5b-backups");
        Directory.CreateDirectory(sqlWritable);

        var backup = new SqlSchoolDatabaseBackup(
            new SqlCommandBackupExecutor(SchoolCs),
            new DriveDiskSpaceChecker(),
            sqlWritable,
            IntegrationDatabase,
            minFreeBytes: 1,
            minBackupBytes: 1);

        var bak = await backup.CreateVerifiedBackupAsync("1.2.0-test", 1, 2, CancellationToken.None);
        bak.IntegrityVerified.Should().BeTrue();
        bak.ByteSize.Should().BeGreaterThan(0);
        File.Exists(bak.BackupFilePath).Should().BeTrue();
        SchoolBackupPathGuard.EnsureAllowed(bak.BackupFilePath, sqlWritable, bak.BackupFilePath);

        var package = CreateProbePackage();
        try
        {
            var applied = await new MigrationManager(SchoolCs).ApplyPackageAsync(package);
            applied.CurrentVersion.Should().Be(2);
            await using (var afterMig = new SqlConnection(SchoolCs))
            {
                await afterMig.OpenAsync();
                (await ScalarIntAsync(afterMig, "SELECT SchemaVersion FROM dbo.AppSchemaVersion WHERE Id = 1")).Should().Be(2);
                (await ScalarIntAsync(
                    afterMig,
                    "SELECT CASE WHEN OBJECT_ID(N'dbo.UpdateAgentIntegrationProbe', N'U') IS NULL THEN 0 ELSE 1 END"))
                    .Should().Be(1);
            }

            await backup.RestoreQuiescedBackupAsync(
                new SchoolDatabaseRestoreRequest(
                    bak.BackupFilePath,
                    bak.BackupFilePath,
                    sqlWritable,
                    IntegrationDatabase),
                CancellationToken.None);

            await using var afterRestore = new SqlConnection(SchoolCs);
            await afterRestore.OpenAsync();
            (await ScalarIntAsync(afterRestore, "SELECT SchemaVersion FROM dbo.AppSchemaVersion WHERE Id = 1")).Should().Be(1);
            (await ScalarIntAsync(
                afterRestore,
                "SELECT CASE WHEN OBJECT_ID(N'dbo.UpdateAgentIntegrationProbe', N'U') IS NULL THEN 0 ELSE 1 END"))
                .Should().Be(0);
        }
        finally
        {
            try
            {
                Directory.Delete(package, recursive: true);
            }
            catch
            {
                // temp
            }

            try
            {
                if (File.Exists(bak.BackupFilePath))
                {
                    File.Delete(bak.BackupFilePath);
                }
            }
            catch
            {
                // SQL may still hold the file
            }
        }
    }

    private static async Task ResetIntegrationAsync(SqlConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            IF OBJECT_ID(N'dbo.UpdateAgentIntegrationProbe', N'U') IS NOT NULL
                DROP TABLE dbo.UpdateAgentIntegrationProbe;
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
            ELSE
                UPDATE dbo.AppSchemaVersion SET SchemaVersion = 1, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = 1;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static string CreateProbePackage()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ua-2b5a-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sqlPath = Path.Combine(dir, "Migration1_2.sql");
        File.WriteAllText(sqlPath, """
            CREATE TABLE dbo.UpdateAgentIntegrationProbe
            (
                Id INT NOT NULL CONSTRAINT PK_UpdateAgentIntegrationProbe PRIMARY KEY,
                Note NVARCHAR(100) NOT NULL
            );
            INSERT INTO dbo.UpdateAgentIntegrationProbe (Id, Note) VALUES (1, N'Lot 2B-5A probe');
            """);
        var hash = ArtifactHash.Sha256File(sqlPath);
        var manifest = new MigrationManifest
        {
            SchemaVersion = 2,
            FromSchemaVersion = 1,
            ToSchemaVersion = 2,
            ReleaseVersion = "1.2.0-test",
            Migrations = ["Migration1_2.sql"],
            Files = [new MigrationFileHash { Name = "Migration1_2.sql", Sha256 = hash }],
        };
        File.WriteAllText(
            Path.Combine(dir, MigrationPackage.ManifestFileName),
            System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            }));
        return dir;
    }

    private static async Task<int> ScalarIntAsync(SqlConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var value = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(value);
    }
}
