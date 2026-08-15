using FluentAssertions;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UpdateAgent.Tests;

public sealed class BackupPathAndSqlCommandTests
{
    [Fact]
    public void Restore_Uses_Signed_Procedure_Not_Raw_Sql()
    {
        SqlBackupCommands.SignedRestoreProcedure.Should().Be("dbo.ErpScolaire_RestoreSchoolDatabase");
        SqlBackupCommands.SignedVerifyProcedure.Should().Be("dbo.ErpScolaire_VerifySchoolBackup");
        SqlBackupCommands.BackupCopyOnly("SchoolDb", @"C:\ProgramData\ERP_SCOLAIRE\Backups\db.bak")
            .Should().NotContain("RESTORE DATABASE");
    }

    [Fact]
    public async Task RestoreReplace_Refuses_Mismatched_Target_Database()
    {
        var executor = new SqlCommandBackupExecutor(
            @"Server=localhost\HEROS_SQL19;Database=SchoolManagementRDC_UpdateIntegration;Trusted_Connection=True;");
        var act = () => executor.RestoreReplaceAsync(
            "SomeOtherDatabase",
            @"C:\ProgramData\ERP_SCOLAIRE\Backups\db.bak",
            CancellationToken.None);
        await act.Should().ThrowAsync<MigrationException>().WithMessage("*base ERP*");
    }

    [Fact]
    public void Restore_Connection_Uses_Master_Catalog()
    {
        var master = SqlRestoreConnection.ToMaster(
            @"Server=localhost\HEROS_SQL19;Database=SchoolManagementRDC_UpdateIntegration;Trusted_Connection=True;");
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(master);
        builder.InitialCatalog.Should().Be("master");
    }

    [Fact]
    public void Restore_Connection_Refuses_System_Database()
    {
        var act = () => SqlRestoreConnection.ToMaster(
            @"Server=localhost\HEROS_SQL19;Database=master;Trusted_Connection=True;");
        act.Should().Throw<MigrationException>().WithMessage("*système*");
    }

    [Fact]
    public void Restore_Connection_Refuses_Empty_Catalog()
    {
        var act = () => SqlRestoreConnection.ToMaster(@"Server=localhost\HEROS_SQL19;Trusted_Connection=True;");
        act.Should().Throw<MigrationException>();
    }

    [Fact]
    public void System_Database_Name_Is_Refused()
    {
        var act = () => SchoolBackupPathGuard.EnsureDatabaseName("master");
        act.Should().Throw<MigrationException>().WithMessage("*système*");
    }

    [Fact]
    public void Invalid_Database_Name_Is_Refused()
    {
        var act = () => SchoolBackupPathGuard.EnsureDatabaseName("master; DROP");
        act.Should().Throw<MigrationException>();
    }

    [Fact]
    public void Backup_Commands_Use_CopyOnly_And_Checksum()
    {
        var bak = @"C:\ProgramData\ERP_SCOLAIRE\Backups\db.bak";
        SqlBackupCommands.BackupCopyOnly("SchoolDb", bak).Should().Contain("COPY_ONLY").And.Contain("CHECKSUM");
        SqlBackupCommands.VerifyOnly(bak).Should().Contain("VERIFYONLY").And.Contain("CHECKSUM");
    }

    [Fact]
    public void Path_Outside_Whitelist_Is_Refused()
    {
        var root = Path.Combine(Path.GetTempPath(), "bak-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var expected = Path.Combine(root, "ok.bak");
        var act = () => SchoolBackupPathGuard.EnsureAllowed(@"C:\Windows\evil.bak", root, expected);
        act.Should().Throw<MigrationException>().WithMessage("*Backups*");
    }

    [Fact]
    public void Path_Of_Another_Release_Is_Refused()
    {
        var root = Path.Combine(Path.GetTempPath(), "bak-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var a = Path.Combine(root, "rel-1.2.0.bak");
        var b = Path.Combine(root, "rel-9.9.9.bak");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");
        var act = () => SchoolBackupPathGuard.EnsureAllowed(b, root, a);
        act.Should().Throw<MigrationException>().WithMessage("*autre release*");
    }

    [Fact]
    public void Unc_Path_Is_Refused()
    {
        var act = () => SchoolBackupPathGuard.EnsureAllowed(@"\\server\share\x.bak", @"\\server\share", @"\\server\share\x.bak");
        act.Should().Throw<MigrationException>();
    }

    [Fact]
    public void Compatible_Schema_Is_Accepted()
    {
        SchemaCompatibility.Ensure(1, 1, 3);
        SchemaCompatibility.Ensure(2, 1, 3);
        SchemaCompatibility.Ensure(3, 1, 3);
    }

    [Fact]
    public void Incompatible_Schema_Is_Refused()
    {
        var act = () => SchemaCompatibility.Ensure(1, 2, 3);
        act.Should().Throw<MigrationException>();
        act = () => SchemaCompatibility.Ensure(5, 1, 3);
        act.Should().Throw<MigrationException>();
    }
}
