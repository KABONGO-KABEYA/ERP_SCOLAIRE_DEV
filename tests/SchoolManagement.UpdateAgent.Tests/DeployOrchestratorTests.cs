using FluentAssertions;
using SchoolManagement.UpdateAgent.Tests.Support;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UpdateAgent.Tests;

public sealed class DeployOrchestratorTests
{
    [Fact]
    public async Task Full_Deploy_Stop_Backup_Migration_Swap_Start_Health_Completed()
    {
        using var ws = new TempWorkspace();
        var (_, state, cred) = DeployHarness.SeedVerified(ws, to: 2);
        var db = new FakeDb { Schema = 1 };
        var svc = new FakeApiService();
        var health = new FakeHealth();
        var before = File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "ServeurDonnees.txt"));
        var cloud = File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "ServeurDonneesCloud.txt"));
        var files = File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "ServeurFichiers.txt"));
        var identity = File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "ServerIdentity.json"));
        var result = await DeployHarness.Create(ws, db, svc, health, new FakeDisk()).RunAsync(state, cred, CancellationToken.None);
        result.LastResult.Should().Be(AgentResults.Completed);
        result.Phase.Should().Be(DeployPhases.Idle);
        result.BackupFilePath.Should().NotBeNull();
        File.Exists(result.BackupFilePath!).Should().BeTrue();
        result.BackupBytes.Should().BeGreaterThan(0);
        result.BackupTakenAtUtc.Should().NotBeNull();
        svc.StopCalls.Should().BeGreaterThan(0);
        svc.StartCalls.Should().BeGreaterThan(0);
        health.Calls.Should().BeGreaterThanOrEqualTo(3);
        db.ApplyCalls.Should().Be(1);
        db.Schema.Should().Be(2);
        db.RestoreCalls.Should().Be(0);
        Directory.Exists(ws.Paths.ApiPrevious()).Should().BeTrue();
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "ServeurDonnees.txt")).Should().Be(before);
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "ServeurDonneesCloud.txt")).Should().Be(cloud);
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "ServeurFichiers.txt")).Should().Be(files);
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "ServerIdentity.json")).Should().Be(identity);
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "SchoolManagement.API.dll")).Should().Contain("1.2.0");
    }

    [Fact]
    public async Task Backup_Success_Persists_Path_Size_Utc()
    {
        using var ws = new TempWorkspace();
        var executor = new RecordingExecutor { WriteFile = true };
        var backup = new SqlSchoolDatabaseBackup(
            executor,
            new FakeDisk(),
            ws.Paths.Backups,
            "SchoolDb",
            minFreeBytes: 1,
            minBackupBytes: 1,
            fileNameFactory: (_, _, _, _) => "fixed.bak");
        var result = await backup.CreateVerifiedBackupAsync("1.2.0", 1, 1, CancellationToken.None);
        result.IntegrityVerified.Should().BeTrue();
        result.ByteSize.Should().BeGreaterThan(0);
        File.Exists(result.BackupFilePath).Should().BeTrue();
        executor.BackupSql.Should().Contain("COPY_ONLY");
        executor.VerifySql.Should().Contain("VERIFYONLY");
    }

    [Fact]
    public async Task Backup_Absent_Fails()
    {
        using var ws = new TempWorkspace();
        var backup = new SqlSchoolDatabaseBackup(
            new RecordingExecutor { WriteFile = false },
            new FakeDisk(),
            ws.Paths.Backups,
            "SchoolDb",
            1,
            1,
            (_, _, _, _) => "missing.bak");
        var act = async () => await backup.CreateVerifiedBackupAsync("1.2.0", 1, 1, CancellationToken.None);
        await act.Should().ThrowAsync<MigrationException>().WithMessage("*absent*");
    }

    [Fact]
    public async Task VerifyOnly_Failure_Deletes_Temp_Bak()
    {
        using var ws = new TempWorkspace();
        var exec = new RecordingExecutor { WriteFile = true, VerifyFails = true };
        var backup = new SqlSchoolDatabaseBackup(exec, new FakeDisk(), ws.Paths.Backups, "SchoolDb", 1, 1, (_, _, _, _) => "badverify.bak");
        var act = async () => await backup.CreateVerifiedBackupAsync("1.2.0", 1, 1, CancellationToken.None);
        await act.Should().ThrowAsync<MigrationException>().WithMessage("*VERIFYONLY*");
        File.Exists(Path.Combine(ws.Paths.Backups, "badverify.bak")).Should().BeFalse();
    }

    [Fact]
    public async Task Disk_Insufficient_Fails_Preflight()
    {
        using var ws = new TempWorkspace();
        var (_, state, cred) = DeployHarness.SeedVerified(ws);
        var db = new FakeDb();
        var opt = CycleFactory.Options(ws);
        opt.MinFreeDiskBytes = 5_000_000;
        var result = await DeployHarness.Create(ws, db, new FakeApiService(), new FakeHealth(), new FakeDisk { Available = 10 }, opt)
            .RunAsync(state, cred, CancellationToken.None);
        result.Phase.Should().Be(DeployPhases.PreflightFailed);
        db.BackupCalls.Should().Be(0);
    }

    [Fact]
    public async Task Migration_Success_And_Already_At_Target_Skips_Apply()
    {
        using var ws = new TempWorkspace();
        var (_, state, cred) = DeployHarness.SeedVerified(ws, to: 1);
        var db = new FakeDb { Schema = 1 };
        await DeployHarness.Create(ws, db, new FakeApiService(), new FakeHealth(), new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        db.ApplyCalls.Should().Be(0);
        db.Schema.Should().Be(1);
    }

    [Fact]
    public async Task Migration_Failure_Restarts_Old_Api_Without_Sql_Restore()
    {
        using var ws = new TempWorkspace();
        var (_, state, cred) = DeployHarness.SeedVerified(ws, to: 2);
        var db = new FakeDb { Schema = 1, ApplyFails = true };
        var svc = new FakeApiService();
        var marker = File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "SchoolManagement.API.dll"));
        var result = await DeployHarness.Create(ws, db, svc, new FakeHealth(), new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        result.Phase.Should().Be(DeployPhases.MigrationFailed);
        db.RestoreCalls.Should().Be(0);
        svc.StartCalls.Should().BeGreaterThan(0);
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "SchoolManagement.API.dll")).Should().Be(marker);
    }

    [Fact]
    public async Task Stop_Service_Failure()
    {
        using var ws = new TempWorkspace();
        var (_, state, cred) = DeployHarness.SeedVerified(ws);
        var result = await DeployHarness.Create(ws, new FakeDb(), new FakeApiService { StopFails = true }, new FakeHealth(), new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        result.Phase.Should().Be(DeployPhases.ApiStopFailed);
    }

    [Fact]
    public async Task Start_Service_Failure_Rolls_Back_Files()
    {
        using var ws = new TempWorkspace();
        var (_, state, cred) = DeployHarness.SeedVerified(ws, to: 1);
        var db = new FakeDb { Schema = 1 };
        var svc = new FakeApiService { StartFailCount = 1 };
        var old = File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "SchoolManagement.API.dll"));
        var result = await DeployHarness.Create(ws, db, svc, new FakeHealth(), new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        result.Phase.Should().BeOneOf(DeployPhases.RollbackSucceeded, DeployPhases.RollbackFailed);
        db.RestoreCalls.Should().Be(0);
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "SchoolManagement.API.dll")).Should().Be(old);
    }

    [Fact]
    public async Task Health_Ko_Triggers_Rollback()
    {
        using var ws = new TempWorkspace();
        var (_, state, cred) = DeployHarness.SeedVerified(ws);
        var health = new FakeHealth { Next = new HealthProbeResult { Error = "status ≠ ok" } };
        var result = await DeployHarness.Create(ws, new FakeDb(), new FakeApiService(), health, new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        result.LastResult.Should().BeOneOf(DeployPhases.RollbackSucceeded, DeployPhases.HealthCheckFailed, DeployPhases.RollbackFailed);
        result.Phase.Should().BeOneOf(DeployPhases.RollbackSucceeded, DeployPhases.RollbackFailed);
    }

    [Fact]
    public async Task Health_Wrong_Version_Protocol_Schema_Instance()
    {
        using var ws = new TempWorkspace();
        var probe = new ApiHealthProbe(new HttpClient(new ScriptedHandler
        {
            Responder = (_, _) => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(
                    """{"status":"ok","protocolVersion":1,"version":"9.9.9","schemaVersion":9,"identity":{"serverInstanceId":"00000000-0000-0000-0000-000000000000"}}"""),
            },
        }));
        var instance = Guid.NewGuid();
        var r = await probe.CheckOnceAsync("http://127.0.0.1/api/health", "1.2.0", 2, 1, instance, CancellationToken.None);
        r.Ok.Should().BeFalse();
        r.Error.Should().Contain("ProtocolVersion");
    }

    [Fact]
    public async Task Health_Wrong_Protocol_Only()
    {
        var instance = Guid.NewGuid();
        var probe = ProbeJson(HealthJson(instance, protocol: 1, version: "1.2.0", schema: 1));
        (await probe.CheckOnceAsync("http://x/", "1.2.0", 2, 1, instance, CancellationToken.None)).Error.Should().Contain("ProtocolVersion");
    }

    [Fact]
    public async Task Health_Http_Not_Ok()
    {
        var probe = new ApiHealthProbe(new HttpClient(new ScriptedHandler
        {
            Responder = (_, _) => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable),
        }));
        var r = await probe.CheckOnceAsync("http://x/", "1.2.0", 2, 1, Guid.NewGuid(), CancellationToken.None);
        r.Ok.Should().BeFalse();
        r.Error.Should().Contain("HTTP");
    }

    [Fact]
    public async Task Health_Wrong_Version_Only()
    {
        var instance = Guid.NewGuid();
        var probe = ProbeJson(HealthJson(instance, protocol: 2, version: "9.9.9", schema: 1));
        (await probe.CheckOnceAsync("http://x/", "1.2.0", 2, 1, instance, CancellationToken.None)).Error.Should().Contain("version");
    }

    [Fact]
    public async Task Health_Wrong_Schema_Only()
    {
        var instance = Guid.NewGuid();
        var probe = ProbeJson(HealthJson(instance, protocol: 2, version: "1.2.0", schema: 9));
        (await probe.CheckOnceAsync("http://x/", "1.2.0", 2, 1, instance, CancellationToken.None)).Error.Should().Contain("SchemaVersion");
    }

    [Fact]
    public async Task Health_Wrong_ServerInstanceId()
    {
        var probe = ProbeJson("""{"status":"ok","protocolVersion":2,"version":"1.2.0","schemaVersion":1,"identity":{"serverInstanceId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}}""");
        (await probe.CheckOnceAsync("http://x/", "1.2.0", 2, 1, Guid.NewGuid(), CancellationToken.None)).Error.Should().Contain("ServerInstanceId");
    }

    [Fact]
    public void Swap_Success_Keeps_Previous()
    {
        using var ws = new TempWorkspace();
        var (extract, _, _) = DeployHarness.SeedVerified(ws);
        var swap = new ApiDirectorySwapper(ws.Paths);
        swap.PrepareIncoming(Path.Combine(extract, "api"), "1.2.0");
        swap.SwapToIncoming("1.2.0");
        Directory.Exists(ws.Paths.ApiPrevious()).Should().BeTrue();
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "ServeurDonnees.txt")).Should().Contain("SERVEUR");
        swap.Inspect("1.2.0").Kind.Should().Be(ApiLayoutKind.AlreadyTarget);
    }

    [Fact]
    public void Swap_Interrupted_Resumes_Incoming_Rename()
    {
        using var ws = new TempWorkspace();
        var (extract, _, _) = DeployHarness.SeedVerified(ws);
        var swap = new ApiDirectorySwapper(ws.Paths);
        swap.PrepareIncoming(Path.Combine(extract, "api"), "1.2.0");
        Directory.Move(ws.ApiInstallRoot, ws.Paths.ApiPrevious());
        swap.Inspect("1.2.0").Kind.Should().Be(ApiLayoutKind.ResumeIncomingRename);
        swap.SwapToIncoming("1.2.0");
        Directory.Exists(ws.ApiInstallRoot).Should().BeTrue();
    }

    [Fact]
    public void Ambiguous_Layout_Does_Not_Guess()
    {
        using var ws = new TempWorkspace();
        Directory.Delete(ws.ApiInstallRoot, recursive: true);
        var swap = new ApiDirectorySwapper(ws.Paths);
        swap.Inspect("1.2.0").Kind.Should().Be(ApiLayoutKind.Ambiguous);
    }

    [Fact]
    public async Task Resume_After_Crash_At_BackupCreated_Does_Not_Redo_Backup()
    {
        using var ws = new TempWorkspace();
        var (extract, state, cred) = DeployHarness.SeedVerified(ws, to: 1);
        var db = new FakeDb { Schema = 1 };
        new ApiDirectorySwapper(ws.Paths).PrepareIncoming(Path.Combine(extract, "api"), "1.2.0");
        state.Phase = DeployPhases.BackupCreated;
        state.SchemaBefore = 1;
        state.BackupFilePath = Path.Combine(ws.Paths.Backups, "keep.bak");
        File.WriteAllBytes(state.BackupFilePath, "bak"u8.ToArray());
        state.BackupBytes = 3;
        var result = await DeployHarness.Create(ws, db, new FakeApiService(), new FakeHealth(), new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        result.LastResult.Should().Be(AgentResults.Completed);
        db.BackupCalls.Should().Be(0);
        db.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task Resume_Completed_Goes_Idle_Without_Work()
    {
        using var ws = new TempWorkspace();
        var (_, state, cred) = DeployHarness.SeedVerified(ws);
        var db = new FakeDb();
        state.Phase = DeployPhases.Completed;
        var result = await DeployHarness.Create(ws, db, new FakeApiService(), new FakeHealth(), new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        result.Phase.Should().Be(DeployPhases.Idle);
        db.BackupCalls.Should().Be(0);
        db.ApplyCalls.Should().Be(0);
    }

    [Fact]
    public async Task Ambiguous_Layout_During_Swap_Is_RollbackFailed_Without_Deletes()
    {
        using var ws = new TempWorkspace();
        var (extract, state, cred) = DeployHarness.SeedVerified(ws);
        Directory.CreateDirectory(Path.Combine(ws.InstallParent, "Api.Incoming-other"));
        Directory.Delete(ws.ApiInstallRoot, recursive: true);
        Directory.CreateDirectory(ws.Paths.ApiIncoming("1.2.0"));
        File.Copy(Path.Combine(extract, "api", "SchoolManagement.API.dll"), Path.Combine(ws.Paths.ApiIncoming("1.2.0"), "x.dll"));
        state.Phase = DeployPhases.MigrationSucceeded;
        state.SchemaBefore = 1;
        state.SchemaAfter = 1;
        var incomingBefore = Directory.Exists(ws.Paths.ApiIncoming("1.2.0"));
        var extraBefore = Directory.Exists(Path.Combine(ws.InstallParent, "Api.Incoming-other"));
        var result = await DeployHarness.Create(ws, new FakeDb(), new FakeApiService(), new FakeHealth(), new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        result.Phase.Should().Be(DeployPhases.RollbackFailed);
        incomingBefore.Should().BeTrue();
        Directory.Exists(ws.Paths.ApiIncoming("1.2.0")).Should().BeTrue();
        extraBefore.Should().BeTrue();
        Directory.Exists(Path.Combine(ws.InstallParent, "Api.Incoming-other")).Should().BeTrue();
    }

    [Fact]
    public void RollbackRequired_Backup_Is_Kept()
    {
        using var ws = new TempWorkspace();
        var current = Path.Combine(ws.Paths.Backups, "current.bak");
        File.WriteAllText(current, "x");
        File.WriteAllText(Path.Combine(ws.Paths.Backups, "noise.bak"), "y");
        var state = new AgentState
        {
            Phase = DeployPhases.RollbackRequired,
            BackupFilePath = current,
        };
        BackupRetention.Prune(ws.Paths.Backups, state);
        File.Exists(current).Should().BeTrue();
        File.Exists(Path.Combine(ws.Paths.Backups, "noise.bak")).Should().BeFalse();
    }

    [Fact]
    public async Task Resume_After_Crash_At_MigrationSucceeded_Does_Not_Reapply()
    {
        using var ws = new TempWorkspace();
        var (extract, state, cred) = DeployHarness.SeedVerified(ws);
        var db = new FakeDb { Schema = 1 };
        var swap = new ApiDirectorySwapper(ws.Paths);
        swap.PrepareIncoming(Path.Combine(extract, "api"), "1.2.0");
        state.Phase = DeployPhases.MigrationSucceeded;
        state.SchemaBefore = 1;
        state.SchemaAfter = 1;
        state.BackupFilePath = Path.Combine(ws.Paths.Backups, "keep.bak");
        File.WriteAllBytes(state.BackupFilePath, "bak"u8.ToArray());
        state.BackupBytes = 3;
        var result = await DeployHarness.Create(ws, db, new FakeApiService(), new FakeHealth(), new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        result.LastResult.Should().Be(AgentResults.Completed);
        db.ApplyCalls.Should().Be(0);
        db.BackupCalls.Should().Be(0);
    }

    [Fact]
    public async Task Rollback_Sql_When_Schema_Advanced_And_New_Api_Fails()
    {
        using var ws = new TempWorkspace();
        var (_, state, cred) = DeployHarness.SeedVerified(ws, to: 2);
        var db = new FakeDb { Schema = 1 };
        var svc = new FakeApiService { StartFailCount = 1 };
        var oldDll = File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "SchoolManagement.API.dll"));
        var result = await DeployHarness.Create(ws, db, svc, new FakeHealth(), new FakeDisk())
            .RunAsync(state, cred, CancellationToken.None);
        db.ApplyCalls.Should().Be(1);
        db.RestoreCalls.Should().Be(1);
        db.Schema.Should().Be(1);
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "SchoolManagement.API.dll")).Should().Be(oldDll);
        result.Phase.Should().Be(DeployPhases.RollbackSucceeded);
        SchoolBackupPathGuard.EnsureAllowed(db.LastRestorePath!, ws.Paths.Backups, db.LastRestorePath!);
    }

    [Fact]
    public async Task Current_Backup_Not_Pruned_Until_Completed()
    {
        using var ws = new TempWorkspace();
        var state = new AgentState
        {
            Phase = DeployPhases.BackupCreated,
            BackupFilePath = Path.Combine(ws.Paths.Backups, "current.bak"),
            CompletedBackupPaths =
            [
                Path.Combine(ws.Paths.Backups, "old1.bak"),
                Path.Combine(ws.Paths.Backups, "old2.bak"),
                Path.Combine(ws.Paths.Backups, "old3.bak"),
                Path.Combine(ws.Paths.Backups, "old4.bak"),
            ],
        };
        foreach (var p in state.CompletedBackupPaths.Concat([state.BackupFilePath]))
        {
            File.WriteAllText(p, "x");
        }

        BackupRetention.Prune(ws.Paths.Backups, state);
        File.Exists(state.BackupFilePath).Should().BeTrue();
        File.Exists(state.CompletedBackupPaths[0]).Should().BeFalse();
        File.Exists(state.CompletedBackupPaths[3]).Should().BeTrue();
    }

    [Fact]
    public void Api_Previous_Kept_After_Completed_Swap()
    {
        using var ws = new TempWorkspace();
        var (extract, _, _) = DeployHarness.SeedVerified(ws);
        var swap = new ApiDirectorySwapper(ws.Paths);
        swap.PrepareIncoming(Path.Combine(extract, "api"), "1.2.0");
        swap.SwapToIncoming("1.2.0");
        Directory.Exists(ws.Paths.ApiPrevious()).Should().BeTrue();
        File.Exists(Path.Combine(ws.Paths.ApiPrevious(), "SchoolManagement.API.dll")).Should().BeTrue();
    }

    private static string HealthJson(Guid instance, int protocol, string version, int schema) =>
        "{\"status\":\"ok\",\"protocolVersion\":" + protocol
        + ",\"version\":\"" + version
        + "\",\"schemaVersion\":" + schema
        + ",\"identity\":{\"serverInstanceId\":\"" + instance.ToString("D") + "\"}}";

    private static ApiHealthProbe ProbeJson(string json) =>
        new(new HttpClient(new ScriptedHandler
        {
            Responder = (_, _) => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json),
            },
        }));
}

internal sealed class RecordingExecutor : ISqlBackupExecutor
{
    public string DatabaseName => "SchoolDb";
    public bool WriteFile { get; set; } = true;
    public bool VerifyFails { get; set; }
    public string? BackupSql { get; private set; }
    public string? VerifySql { get; private set; }

    public Task BackupCopyOnlyAsync(string absoluteBakPath, CancellationToken cancellationToken)
    {
        BackupSql = SqlBackupCommands.BackupCopyOnly(DatabaseName, absoluteBakPath);
        if (WriteFile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteBakPath)!);
            File.WriteAllBytes(absoluteBakPath, "bak-data"u8.ToArray());
        }

        return Task.CompletedTask;
    }

    public Task VerifyOnlyAsync(string absoluteBakPath, CancellationToken cancellationToken)
    {
        VerifySql = SqlBackupCommands.VerifyOnly(absoluteBakPath);
        if (VerifyFails)
        {
            throw new MigrationException("VERIFYONLY échoue");
        }

        return Task.CompletedTask;
    }

    public Task RestoreReplaceAsync(string databaseName, string absoluteBakPath, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
