using System.Net;
using FluentAssertions;
using SchoolManagement.UpdateAgent;
using SchoolManagement.UpdateAgent.Tests.Support;
using Xunit;

namespace SchoolManagement.UpdateAgent.Tests;

public sealed class AgentCycleTests
{
    private const string Secret = "unit-test-client-secret-DO-NOT-LOG-9f3c";

    [Fact]
    public async Task Token_Obtained_Then_No_Release()
    {
        using var ws = new TempWorkspace();
        var cred = CycleFactory.SampleCredential(Secret);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(cred);
        var bootstrap = new FakeBootstrap
        {
            Token = Token(cred),
            Check = new AgentCheckResult { StatusCode = HttpStatusCode.NoContent },
        };
        var acquire = new FakeAcquire();
        var log = new RecordingLogger<AgentCycle>();
        var state = await CycleFactory.Create(ws, CycleFactory.Options(ws), bootstrap, acquire, log)
            .RunAsync(CancellationToken.None);
        state.LastResult.Should().Be(AgentResults.NoRelease);
        acquire.Calls.Should().Be(0);
        bootstrap.TokenCalls.Should().Be(1);
        bootstrap.LastRequestBodyHint.Should().Contain("schoolId-omitted");
        LogsHaveNoSecret(log, ws);
    }

    [Fact]
    public async Task Wrong_Secret_Fails_Without_Logging_Secret()
    {
        using var ws = new TempWorkspace();
        var cred = CycleFactory.SampleCredential(Secret);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(cred);
        var bootstrap = new FakeBootstrap
        {
            TokenError = new AgentException("Token Bootstrap HTTP 401."),
        };
        var log = new RecordingLogger<AgentCycle>();
        var state = await CycleFactory.Create(ws, CycleFactory.Options(ws), bootstrap, new FakeAcquire(), log)
            .RunAsync(CancellationToken.None);
        state.LastResult.Should().Be(AgentResults.Failed);
        state.LastError.Should().Contain("401");
        LogsHaveNoSecret(log, ws);
    }

    [Fact]
    public async Task Expired_Jwt_Fails()
    {
        using var ws = new TempWorkspace();
        var cred = CycleFactory.SampleCredential(Secret);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(cred);
        var bootstrap = new FakeBootstrap
        {
            Token = Token(cred),
            CheckError = new AgentException("Check release HTTP 401."),
        };
        var state = await CycleFactory.Create(ws, CycleFactory.Options(ws), bootstrap, new FakeAcquire())
            .RunAsync(CancellationToken.None);
        state.LastResult.Should().Be(AgentResults.Failed);
        state.LastError.Should().Contain("401");
    }

    [Fact]
    public async Task Desktop_Only_Is_Ignored_Without_Download()
    {
        using var ws = new TempWorkspace();
        var cred = CycleFactory.SampleCredential(Secret);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(cred);
        var acquire = new FakeAcquire();
        var bootstrap = new FakeBootstrap
        {
            Token = Token(cred),
            Check = new AgentCheckResult
            {
                StatusCode = HttpStatusCode.OK,
                Body = new AgentReleaseCheckDto
                {
                    ReleaseId = Guid.NewGuid(),
                    Version = "1.2.0",
                    Artifact = CycleFactory.Artifact("Desktop", "1.2.0", "https://example.com/d.exe", new string('a', 64)),
                },
            },
        };
        var before = ws.SnapshotApiInstall();
        var state = await CycleFactory.Create(ws, CycleFactory.Options(ws), bootstrap, acquire)
            .RunAsync(CancellationToken.None);
        state.LastResult.Should().Be(AgentResults.IgnoredDesktopOnly);
        acquire.Calls.Should().Be(0);
        ws.SnapshotApiInstall().Should().Be(before);
    }

    [Fact]
    public async Task Api_Migration_Accepted_Persists_State()
    {
        using var ws = new TempWorkspace();
        var cred = CycleFactory.SampleCredential(Secret);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(cred);
        var releaseId = Guid.NewGuid();
        var sha = new string('a', 64);
        var bootstrap = new FakeBootstrap
        {
            Token = Token(cred),
            Check = new AgentCheckResult
            {
                StatusCode = HttpStatusCode.OK,
                Body = new AgentReleaseCheckDto
                {
                    ReleaseId = releaseId,
                    Version = "1.2.0",
                    ProtocolVersion = 2,
                    FromSchemaVersion = 1,
                    SchemaVersion = 1,
                    Api = CycleFactory.Artifact("Api", "1.2.0", "http://127.0.0.1/api.zip", sha, releaseId),
                    Migration = CycleFactory.Artifact("Migration", "1.2.0", "http://127.0.0.1/mig.zip", sha, releaseId),
                },
            },
        };
        var acquire = new FakeAcquire();
        var before = ws.SnapshotApiInstall();
        var state = await CycleFactory.Create(ws, CycleFactory.Options(ws), bootstrap, acquire)
            .RunAsync(CancellationToken.None);
        state.LastResult.Should().Be(AgentResults.Downloaded);
        state.TargetRelease.Should().Be("1.2.0");
        state.TargetSchemaVersion.Should().Be(1);
        state.CurrentRelease.Should().BeNull();
        acquire.Calls.Should().Be(1);
        var persisted = new AgentStateStore(ws.Paths).Load();
        persisted.LastResult.Should().Be(AgentResults.Downloaded);
        persisted.Phase.Should().Be(DeployPhases.Verified);
        persisted.TargetReleaseId.Should().Be(releaseId);
        ws.SnapshotApiInstall().Should().Be(before);
    }

    [Fact]
    public async Task Resume_In_Progress_Does_Not_Redownload_Even_If_AutoDeploy_False()
    {
        using var ws = new TempWorkspace();
        var (extract, state, cred) = DeployHarness.SeedVerified(ws, to: 1);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(cred);
        new ApiDirectorySwapper(ws.Paths).PrepareIncoming(Path.Combine(extract, "api"), "1.2.0");
        state.Phase = DeployPhases.BackupCreated;
        state.SchemaBefore = 1;
        state.BackupFilePath = Path.Combine(ws.Paths.Backups, "keep.bak");
        File.WriteAllBytes(state.BackupFilePath, "bak"u8.ToArray());
        state.BackupBytes = 3;
        new AgentStateStore(ws.Paths).Save(state);

        var bootstrap = new FakeBootstrap();
        var acquire = new FakeAcquire();
        var db = new FakeDb { Schema = 1 };
        var opt = CycleFactory.Options(ws);
        opt.AutoDeploy = false;
        var result = await CycleFactory.Create(
                ws,
                opt,
                bootstrap,
                acquire,
                deploy: DeployHarness.Create(ws, db, new FakeApiService(), new FakeHealth(), new FakeDisk(), opt))
            .RunAsync(CancellationToken.None);

        result.LastResult.Should().Be(AgentResults.Completed);
        bootstrap.TokenCalls.Should().Be(0);
        acquire.Calls.Should().Be(0);
        db.BackupCalls.Should().Be(0);
    }

    [Fact]
    public async Task Verified_Without_AutoDeploy_Does_Not_Stop_Api()
    {
        using var ws = new TempWorkspace();
        var cred = CycleFactory.SampleCredential(Secret);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(cred);
        var (_, state, _) = DeployHarness.SeedVerified(ws);
        state.Phase = DeployPhases.Verified;
        new AgentStateStore(ws.Paths).Save(state);
        var bootstrap = new FakeBootstrap
        {
            Token = Token(cred),
            Check = new AgentCheckResult { StatusCode = HttpStatusCode.NoContent },
        };
        var svc = new FakeApiService();
        var db = new FakeDb();
        var opt = CycleFactory.Options(ws);
        opt.AutoDeploy = false;
        var result = await CycleFactory.Create(
                ws,
                opt,
                bootstrap,
                new FakeAcquire(),
                deploy: DeployHarness.Create(ws, db, svc, new FakeHealth(), new FakeDisk(), opt))
            .RunAsync(CancellationToken.None);
        result.LastResult.Should().Be(AgentResults.NoRelease);
        svc.StopCalls.Should().Be(0);
        db.BackupCalls.Should().Be(0);
    }

    [Fact]
    public async Task Idempotent_Reuse_Sets_Skipped()
    {
        using var ws = new TempWorkspace();
        var cred = CycleFactory.SampleCredential(Secret);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(cred);
        var releaseId = Guid.NewGuid();
        var sha = new string('a', 64);
        var bootstrap = new FakeBootstrap
        {
            Token = Token(cred),
            Check = new AgentCheckResult
            {
                StatusCode = HttpStatusCode.OK,
                Body = new AgentReleaseCheckDto
                {
                    ReleaseId = releaseId,
                    Version = "1.2.0",
                    ProtocolVersion = 2,
                    FromSchemaVersion = 1,
                    SchemaVersion = 1,
                    Api = CycleFactory.Artifact("Api", "1.2.0", "http://127.0.0.1/api.zip", sha, releaseId),
                    Migration = CycleFactory.Artifact("Migration", "1.2.0", "http://127.0.0.1/mig.zip", sha, releaseId),
                },
            },
        };
        var state = await CycleFactory.Create(
                ws,
                CycleFactory.Options(ws),
                bootstrap,
                new FakeAcquire { Reused = true })
            .RunAsync(CancellationToken.None);
        state.LastResult.Should().Be(AgentResults.SkippedIdempotent);
    }

    [Fact]
    public async Task Sanitize_Removes_Secret_From_Error()
    {
        AgentCycle.Sanitize("boom " + Secret, Secret).Should().Be("boom [redacted]");
    }

    private static AgentTokenResponse Token(AgentCredential cred) => new()
    {
        AccessToken = "jwt-ok",
        TokenType = "Bearer",
        ExpiresIn = 1800,
        SchoolId = cred.SchoolId,
        ClientId = cred.ClientId,
    };

    private static void LogsHaveNoSecret(RecordingLogger<AgentCycle> log, TempWorkspace ws)
    {
        string.Join('\n', log.Messages).Should().NotContain(Secret);
        if (File.Exists(ws.Paths.StateFile))
        {
            File.ReadAllText(ws.Paths.StateFile).Should().NotContain(Secret);
        }

        if (File.Exists(ws.Paths.CredentialFile))
        {
            File.ReadAllText(ws.Paths.CredentialFile).Should().NotContain(Secret);
        }
    }
}
