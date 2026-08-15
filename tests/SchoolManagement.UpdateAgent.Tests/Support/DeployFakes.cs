using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent.Tests.Support;

internal sealed class FakeDisk : IDiskSpaceChecker
{
    public long Available { get; set; } = 10_000_000_000;

    public long GetAvailableBytes(string pathOnVolume) => Available;
}

internal sealed class FakeApiService : IApiWindowsService
{
    public string ServiceName => AgentServiceNames.ApiWindowsServiceName;
    public bool Running { get; set; } = true;
    public bool StopFails { get; set; }
    public int StartFailCount { get; set; }
    public int StopCalls { get; private set; }
    public int StartCalls { get; private set; }

    public Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        StopCalls++;
        if (StopFails)
        {
            throw new InvalidOperationException("arrêt service échoue");
        }

        Running = false;
        return Task.CompletedTask;
    }

    public Task StartAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        StartCalls++;
        if (StartFailCount > 0)
        {
            StartFailCount--;
            throw new InvalidOperationException("start service échoue");
        }

        Running = true;
        return Task.CompletedTask;
    }

    public Task<bool> IsRunningAsync(CancellationToken cancellationToken) => Task.FromResult(Running);
}

internal sealed class FakeHealth : IApiHealthProbe
{
    public HealthProbeResult Next { get; set; } = new() { Ok = true };
    public int Calls { get; private set; }

    public Task<HealthProbeResult> CheckOnceAsync(
        string url,
        string targetRelease,
        int targetProtocol,
        int targetSchema,
        Guid expectedInstanceId,
        CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Next);
    }
}

internal sealed class FakeDb : ISchoolDatabaseBackup, ISchoolDatabaseRestore, IMigrationEngine
{
    public int Schema { get; set; } = 1;
    public bool BackupWriteFile { get; set; } = true;
    public bool VerifyFails { get; set; }
    public bool ApplyFails { get; set; }
    public bool RestoreFails { get; set; }
    public int BackupCalls { get; private set; }
    public int VerifyCalls { get; private set; }
    public int ApplyCalls { get; private set; }
    public int RestoreCalls { get; private set; }
    public string? LastRestorePath { get; private set; }

    public Task<int> GetCurrentSchemaVersionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Schema);

    public Task<MigrationApplyResult> ApplyLocalPackageAsync(string packageDirectory, CancellationToken cancellationToken = default)
    {
        ApplyCalls++;
        if (ApplyFails)
        {
            throw new MigrationException("migration échoue");
        }

        var previous = Schema;
        var manifest = MigrationPackage.Load(packageDirectory);
        Schema = manifest.Manifest.ToSchemaVersion;
        return Task.FromResult(new MigrationApplyResult(previous, Schema, []));
    }

    public Task<SchoolDatabaseBackupResult> CreateVerifiedBackupAsync(CancellationToken cancellationToken = default) =>
        CreateVerifiedBackupAsync("0.0.0", 1, 1, cancellationToken);

    public Task<SchoolDatabaseBackupResult> CreateVerifiedBackupAsync(
        string releaseVersion,
        int fromSchema,
        int toSchema,
        CancellationToken cancellationToken = default)
    {
        BackupCalls++;
        Directory.CreateDirectory(LastBackupDir);
        var path = Path.Combine(
            LastBackupDir,
            SchoolBackupPathGuard.BuildFileName("SchoolManagementRDC_Test", releaseVersion, fromSchema, toSchema));
        if (BackupWriteFile)
        {
            File.WriteAllBytes(path, "bak"u8.ToArray());
        }

        VerifyCalls++;
        if (VerifyFails)
        {
            TryDelete(path);
            throw new MigrationException("VERIFYONLY échoue");
        }

        if (!File.Exists(path))
        {
            throw new MigrationException("Backup absent après BACKUP DATABASE.");
        }

        return Task.FromResult(new SchoolDatabaseBackupResult(path, DateTime.UtcNow, new FileInfo(path).Length, true));
    }

    public string LastBackupDir { get; set; } = "";

    public Task RestoreQuiescedBackupAsync(SchoolDatabaseRestoreRequest request, CancellationToken cancellationToken = default)
    {
        RestoreCalls++;
        LastRestorePath = SchoolBackupPathGuard.EnsureAllowed(
            request.CandidatePath,
            request.BackupsRoot,
            request.ExpectedPathFromState);
        if (RestoreFails)
        {
            throw new MigrationException("restore échoue");
        }

        Schema = 1;
        return Task.CompletedTask;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }
}

internal static class DeployHarness
{
    public static (string ExtractRoot, AgentState State, AgentCredential Cred) SeedVerified(
        TempWorkspace ws,
        string version = "1.2.0",
        int from = 1,
        int to = 1)
    {
        var extract = Path.Combine(ws.Paths.Staging, "extract", Guid.NewGuid().ToString("N"));
        var pack = Path.Combine(ws.Root, "pack-" + Guid.NewGuid().ToString("N"));
        TestPackages.WriteZips(pack, version, from, to);
        Directory.CreateDirectory(Path.Combine(extract, "api"));
        Directory.CreateDirectory(Path.Combine(extract, "migration"));
        foreach (var file in Directory.GetFiles(Path.Combine(pack, "api-src")))
        {
            File.Copy(file, Path.Combine(extract, "api", Path.GetFileName(file)), overwrite: true);
        }

        foreach (var file in Directory.GetFiles(Path.Combine(pack, "mig-src")))
        {
            File.Copy(file, Path.Combine(extract, "migration", Path.GetFileName(file)), overwrite: true);
        }

        File.WriteAllText(Path.Combine(extract, "api", "SchoolManagement.API.dll"), "api-marker-" + version);
        var cred = CycleFactory.SampleCredential("deploy-secret-not-in-logs");
        var state = new AgentState
        {
            Phase = DeployPhases.Verified,
            TargetRelease = version,
            TargetReleaseId = Guid.NewGuid(),
            FromSchemaVersion = from,
            TargetSchemaVersion = to,
            ProtocolVersion = 2,
            ExtractRoot = extract,
        };
        return (extract, state, cred);
    }

    public static DeployOrchestrator Create(
        TempWorkspace ws,
        FakeDb db,
        FakeApiService service,
        FakeHealth health,
        FakeDisk disk,
        AgentOptions? options = null)
    {
        db.LastBackupDir = ws.Paths.Backups;
        var opt = options ?? CycleFactory.Options(ws);
        return new DeployOrchestrator(
            ws.Paths,
            new AgentStateStore(ws.Paths),
            opt,
            db,
            db,
            db,
            service,
            new ApiDirectorySwapper(ws.Paths),
            health,
            disk,
            NullLogger<DeployOrchestrator>.Instance);
    }
}
