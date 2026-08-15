using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UpdateAgent.Tests;

[CollectionDefinition("UpdateIntegrationSql")]
public sealed class UpdateIntegrationSqlCollection : ICollectionFixture<UpdateIntegrationSqlMarker>
{
}

public sealed class UpdateIntegrationSqlMarker
{
}

/// <summary>Lot 2B-5B — restore via procédure signée, login UpdateAgent labo, base isolée uniquement.</summary>
[Collection("UpdateIntegrationSql")]
[Trait("Category", "LiveSql")]
public sealed class RestoreSignedProcedureLabTests
{
    public const string IntegrationDatabase = "SchoolManagementRDC_UpdateIntegration";
    public const string AgentLogin = "ErpScolaireUA_Lab2B5B";
    public const string AgentPassword = "Lab2B5B-Agent-NotProd!9xK7";

    public static readonly string BackupRoot = Path.Combine(
        @"D:\Mes Projet\ERP_Administration_Scolaire_2026\logs",
        "ua-2b5b-backups");

    private static string AgentCs =>
        "Server=localhost\\HEROS_SQL19;Database=" + IntegrationDatabase
        + ";User Id=" + AgentLogin + ";Password=" + AgentPassword
        + ";TrustServerCertificate=True;Encrypt=True";

    private static string AgentMasterCs =>
        "Server=localhost\\HEROS_SQL19;Database=master;User Id=" + AgentLogin
        + ";Password=" + AgentPassword + ";TrustServerCertificate=True;Encrypt=True";

    private const string WindowsCs =
        @"Server=localhost\HEROS_SQL19;Database=SchoolManagementRDC_UpdateIntegration;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";

    [Fact]
    public async Task Agent_Cannot_Bypass_Signed_Restore()
    {
        Directory.CreateDirectory(BackupRoot);
        await using var school = new SqlConnection(AgentCs);
        await school.OpenAsync();
        Convert.ToString(await ScalarAsync(school, "SELECT DB_NAME()")).Should().Be(IntegrationDatabase);
        Convert.ToString(await ScalarAsync(school, "SELECT SUSER_SNAME()")).Should().Be(AgentLogin);

        var bak = Path.Combine(BackupRoot, "security-agent.bak");
        await using (var bakCmd = school.CreateCommand())
        {
            bakCmd.CommandTimeout = 120;
            bakCmd.CommandText =
                "BACKUP DATABASE [SchoolManagementRDC_UpdateIntegration] TO DISK = @p WITH COPY_ONLY, CHECKSUM, INIT;";
            bakCmd.Parameters.AddWithValue("@p", bak);
            await bakCmd.ExecuteNonQueryAsync();
        }

        await using var master = new SqlConnection(AgentMasterCs);
        await master.OpenAsync();
        Convert.ToString(await ScalarAsync(master, "SELECT DB_NAME()")).Should().Be("master");

        (await ErrorNumberAsync(
                master,
                "RESTORE DATABASE [SchoolManagementRDC_UpdateIntegration] FROM DISK = @p WITH REPLACE, CHECKSUM;",
                ("@p", bak)))
            .Should().Be(3110);

        (await ErrorNumberAsync(
                master,
                "ALTER AUTHORIZATION ON DATABASE::SchoolManagementRDC_UpdateIntegration TO [ErpScolaireUA_Lab2B5B];"))
            .Should().NotBe(0);

        (await ErrorNumberAsync(
                master,
                "CREATE LOGIN [ErpScolaireUA_ShouldNotExist] WITH PASSWORD = N'Tmp-NotUsed-2B5B!x';"))
            .Should().NotBe(0);

        (await ErrorNumberAsync(
                master,
                "CREATE DATABASE [ErpScolaireUA_ShouldNotExist];"))
            .Should().NotBe(0);

        (await ProcErrorAsync(master, "SchoolManagementRDC", bak)).Should().NotBe(0);
        (await ProcErrorAsync(master, "SchoolManagementRDC_Development", bak)).Should().NotBe(0);
        (await ProcErrorAsync(master, IntegrationDatabase, @"C:\Windows\Temp\ua-2b5b-forbidden.bak"))
            .Should().NotBe(0);
        (await ProcErrorAsync(master, IntegrationDatabase, @"\\localhost\share\ua-2b5b.bak"))
            .Should().NotBe(0);

        var otherBak = Path.Combine(BackupRoot, "model-header.bak");
        await using (var win = new SqlConnection(WindowsCs))
        {
            await win.OpenAsync();
            await using var modelBak = win.CreateCommand();
            modelBak.CommandTimeout = 120;
            modelBak.CommandText = "BACKUP DATABASE [model] TO DISK = @p WITH COPY_ONLY, CHECKSUM, INIT;";
            modelBak.Parameters.AddWithValue("@p", otherBak);
            await modelBak.ExecuteNonQueryAsync();
        }

        (await ProcErrorAsync(master, IntegrationDatabase, otherBak)).Should().NotBe(0);
    }

    [Fact]
    public async Task Agent_Backup_Migrate_Restore_Via_Signed_Procedure()
    {
        Directory.CreateDirectory(BackupRoot);
        await using (var opened = new SqlConnection(AgentCs))
        {
            await opened.OpenAsync();
            await ResetIntegrationAsync(opened);
        }

        var backup = new SqlSchoolDatabaseBackup(
            new SqlCommandBackupExecutor(AgentCs),
            new DriveDiskSpaceChecker(),
            BackupRoot,
            IntegrationDatabase,
            minFreeBytes: 1,
            minBackupBytes: 1);

        var bak = await backup.CreateVerifiedBackupAsync("1.2.0-2b5b", 1, 2, CancellationToken.None);
        bak.IntegrityVerified.Should().BeTrue();

        var package = CreateProbePackage();
        try
        {
            var applied = await new MigrationManager(AgentCs).ApplyPackageAsync(package);
            applied.CurrentVersion.Should().Be(2);
            await using (var afterMig = new SqlConnection(AgentCs))
            {
                await afterMig.OpenAsync();
                (await ScalarIntAsync(afterMig, "SELECT SchemaVersion FROM dbo.AppSchemaVersion WHERE Id = 1"))
                    .Should().Be(2);
                (await ScalarIntAsync(
                    afterMig,
                    "SELECT CASE WHEN OBJECT_ID(N'dbo.UpdateAgentIntegrationProbe', N'U') IS NULL THEN 0 ELSE 1 END"))
                    .Should().Be(1);
            }

            await backup.RestoreQuiescedBackupAsync(
                new SchoolDatabaseRestoreRequest(
                    bak.BackupFilePath,
                    bak.BackupFilePath,
                    BackupRoot,
                    IntegrationDatabase),
                CancellationToken.None);

            await using var afterRestore = new SqlConnection(AgentCs);
            await afterRestore.OpenAsync();
            (await ScalarIntAsync(afterRestore, "SELECT SchemaVersion FROM dbo.AppSchemaVersion WHERE Id = 1"))
                .Should().Be(1);
            (await ScalarIntAsync(
                afterRestore,
                "SELECT CASE WHEN OBJECT_ID(N'dbo.UpdateAgentIntegrationProbe', N'U') IS NULL THEN 0 ELSE 1 END"))
                .Should().Be(0);
            Convert.ToString(await ScalarAsync(afterRestore, "SELECT SUSER_SNAME()")).Should().Be(AgentLogin);
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
        }
    }

    [Fact]
    public void Agent_Is_Not_Owner_Or_Sysadmin()
    {
        using var master = new SqlConnection(AgentMasterCs);
        master.Open();
        Convert.ToInt32(Scalar(master, "SELECT IS_SRVROLEMEMBER(N'sysadmin')")).Should().Be(0);
        Convert.ToInt32(Scalar(master, "SELECT IS_SRVROLEMEMBER(N'dbcreator')")).Should().Be(0);
        using var school = new SqlConnection(AgentCs);
        school.Open();
        Convert.ToInt32(Scalar(school, "SELECT CASE WHEN IS_MEMBER(N'db_owner') = 1 THEN 1 ELSE 0 END"))
            .Should().Be(0);
        Convert.ToString(Scalar(
                master,
                "SELECT SUSER_SNAME(owner_sid) FROM sys.databases WHERE name = N'SchoolManagementRDC_UpdateIntegration'"))
            .Should().Be("ErpScolaireRestoreOwner_Lab");
    }

    private static async Task<int> ProcErrorAsync(SqlConnection master, string databaseName, string bak)
    {
        try
        {
            await using var cmd = master.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = SqlBackupCommands.SignedRestoreProcedure;
            cmd.Parameters.Add("@DatabaseName", SqlDbType.NVarChar, 128).Value = databaseName;
            cmd.Parameters.Add("@BackupPath", SqlDbType.NVarChar, 512).Value = bak;
            await cmd.ExecuteNonQueryAsync();
            return 0;
        }
        catch (SqlException ex)
        {
            return ex.Number;
        }
    }

    private static async Task<int> ErrorNumberAsync(
        SqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.AddWithValue(name, value);
            }

            await cmd.ExecuteNonQueryAsync();
            return 0;
        }
        catch (SqlException ex)
        {
            return ex.Number;
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
        var dir = Path.Combine(Path.GetTempPath(), "ua-2b5b-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sqlPath = Path.Combine(dir, "Migration1_2.sql");
        File.WriteAllText(sqlPath, """
            CREATE TABLE dbo.UpdateAgentIntegrationProbe
            (
                Id INT NOT NULL CONSTRAINT PK_UpdateAgentIntegrationProbe PRIMARY KEY,
                Note NVARCHAR(100) NOT NULL
            );
            INSERT INTO dbo.UpdateAgentIntegrationProbe (Id, Note) VALUES (1, N'Lot 2B-5B probe');
            """);
        var hash = ArtifactHash.Sha256File(sqlPath);
        var manifest = new MigrationManifest
        {
            SchemaVersion = 2,
            FromSchemaVersion = 1,
            ToSchemaVersion = 2,
            ReleaseVersion = "1.2.0-2b5b",
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

    private static async Task<object?> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }

    private static object? Scalar(SqlConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private static async Task<int> ScalarIntAsync(SqlConnection connection, string sql) =>
        Convert.ToInt32(await ScalarAsync(connection, sql));
}
