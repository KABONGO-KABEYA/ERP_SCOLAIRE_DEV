using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Bootstrap.API.Contracts;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;
using SchoolManagement.Bootstrap.API.Security;
using SchoolManagement.UpdateAgent;
using SchoolManagement.UpdateAgent.Tests.Support;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UpdateAgent.Tests;

public sealed class BootstrapIntegrationTests : IAsyncLifetime
{
    private readonly TestBootstrapFactory _factory = new();
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private ArtifactStaticServer? _files;
    private string _packDir = string.Empty;

    public async Task InitializeAsync()
    {
        _packDir = Path.Combine(Path.GetTempPath(), "ua-art-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_packDir);
        _files = await ArtifactStaticServer.StartAsync(_packDir);
    }

    public async Task DisposeAsync()
    {
        if (_files is not null)
        {
            await _files.DisposeAsync();
        }

        _factory.Dispose();
        try
        {
            if (Directory.Exists(_packDir))
            {
                Directory.Delete(_packDir, recursive: true);
            }
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public async Task Token_Check_Download_Sha_Staging_Leaves_Api_Untouched()
    {
        using var ws = new TempWorkspace();
        var before = ws.SnapshotApiInstall();
        var schoolId = await SeedSchoolAsync("ECOLE UA");
        var created = await CreateCredentialAsync(schoolId);
        var zips = TestPackages.WriteZips(_packDir, "1.4.0");
        await PublishPairAsync(
            "1.4.0",
            $"{_files!.BaseUrl}/api.zip",
            $"{_files.BaseUrl}/migration.zip",
            zips.ApiSha,
            zips.MigrationSha,
            new FileInfo(zips.ApiZip).Length,
            new FileInfo(zips.MigrationZip).Length,
            schoolId);

        var store = new AgentCredentialStore(ws.Paths, new DpapiSecretProtector());
        store.Save(new AgentCredential
        {
            ClientId = created.ClientId,
            ClientSecret = created.ClientSecret,
            CredentialVersion = created.CredentialVersion,
            SchoolId = schoolId,
        });

        var bootstrapHttp = _factory.CreateClient();
        var bootstrap = new BootstrapAgentClient(bootstrapHttp, new RecordingLogger<BootstrapAgentClient>());
        var downloadHttp = new HttpClient();
        var download = new DownloadManager(
            downloadHttp,
            uri => UpdateUrlGuard.IsAllowed(uri, ["127.0.0.1", "localhost"], allowHttpForLocalHosts: true));
        var acquire = new PackageAcquireService(ws.Paths, download, ["127.0.0.1", "localhost"]);
        var options = CycleFactory.Options(ws, channel: "DEV");
        options.BootstrapBaseUrl = bootstrapHttp.BaseAddress!.ToString();
        var log = new RecordingLogger<AgentCycle>();
        var cycle = new AgentCycle(
            ws.Paths,
            store,
            new AgentStateStore(ws.Paths),
            bootstrap,
            acquire,
            options,
            log);

        var state = await cycle.RunAsync(CancellationToken.None);
        state.LastResult.Should().Be(AgentResults.Downloaded);
        state.TargetRelease.Should().Be("1.4.0");
        File.Exists(Path.Combine(ws.Paths.Packages, "1.4.0", "api.zip")).Should().BeTrue();
        File.Exists(Path.Combine(ws.Paths.Packages, "1.4.0", "migration.zip")).Should().BeTrue();
        Directory.GetFiles(ws.Paths.Staging, "tmp-*.zip").Should().BeEmpty();
        string.Join('\n', log.Messages).Should().NotContain(created.ClientSecret);
        File.ReadAllText(ws.Paths.StateFile).Should().NotContain(created.ClientSecret);
        ws.SnapshotApiInstall().Should().Be(before);
        File.ReadAllText(Path.Combine(ws.ApiInstallRoot, "SchoolManagement.API.dll")).Should().Be("api-marker-v1");
    }

    [Fact]
    public async Task Real_Wrong_Secret_Fails()
    {
        using var ws = new TempWorkspace();
        var schoolId = await SeedSchoolAsync("ECOLE BAD");
        var created = await CreateCredentialAsync(schoolId);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(new AgentCredential
        {
            ClientId = created.ClientId,
            ClientSecret = "not-the-secret",
            CredentialVersion = 1,
            SchoolId = schoolId,
        });
        var bootstrap = new BootstrapAgentClient(_factory.CreateClient(), new RecordingLogger<BootstrapAgentClient>());
        var state = await new AgentCycle(
                ws.Paths,
                new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()),
                new AgentStateStore(ws.Paths),
                bootstrap,
                new FakeAcquire(),
                CycleFactory.Options(ws, "DEV"),
                new RecordingLogger<AgentCycle>())
            .RunAsync(CancellationToken.None);
        state.LastResult.Should().Be(AgentResults.Failed);
        state.LastError.Should().Contain("401");
    }

    [Fact]
    public async Task Real_Expired_Jwt_Is_Rejected()
    {
        var schoolId = await SeedSchoolAsync("ECOLE EXP");
        var created = await CreateCredentialAsync(schoolId);
        var expired = UpdateAgentJwt.Create(
            TestBootstrapFactory.TestAgentJwtSigningKey,
            created.ClientId,
            schoolId,
            serverInstanceId: null,
            expiresUtc: DateTime.UtcNow.AddMinutes(-5));
        var client = new BootstrapAgentClient(_factory.CreateClient(), new RecordingLogger<BootstrapAgentClient>());
        var act = async () => await client.CheckReleaseAsync(expired, "DEV", CancellationToken.None);
        await act.Should().ThrowAsync<AgentException>().WithMessage("*401*");
    }

    [Fact]
    public async Task Desktop_Only_Published_Yields_NoRelease()
    {
        using var ws = new TempWorkspace();
        var schoolId = await SeedSchoolAsync("ECOLE DESK");
        var created = await CreateCredentialAsync(schoolId);
        new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()).Save(new AgentCredential
        {
            ClientId = created.ClientId,
            ClientSecret = created.ClientSecret,
            CredentialVersion = created.CredentialVersion,
            SchoolId = schoolId,
        });
        await PublishDesktopOnlyAsync("1.8.0");
        var bootstrap = new BootstrapAgentClient(_factory.CreateClient(), new RecordingLogger<BootstrapAgentClient>());
        var acquire = new FakeAcquire();
        var state = await new AgentCycle(
                ws.Paths,
                new AgentCredentialStore(ws.Paths, new DpapiSecretProtector()),
                new AgentStateStore(ws.Paths),
                bootstrap,
                acquire,
                CycleFactory.Options(ws, "DEV"),
                new RecordingLogger<AgentCycle>())
            .RunAsync(CancellationToken.None);
        state.LastResult.Should().Be(AgentResults.NoRelease);
        acquire.Calls.Should().Be(0);
    }

    private HttpClient Provisioner()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            AgentProvisionKeyAuthorizationFilter.HeaderName,
            TestBootstrapFactory.TestAgentProvisionApiKey);
        return client;
    }

    private HttpClient Publisher()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            ReleasePublishKeyAuthorizationFilter.HeaderName,
            TestBootstrapFactory.TestReleasePublishApiKey);
        return client;
    }

    private async Task<UpdateAgentCredentialSecretResponse> CreateCredentialAsync(Guid schoolId)
    {
        var response = await Provisioner().PostAsJsonAsync("/api/v1/agent/credentials", new { schoolId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UpdateAgentCredentialSecretResponse>(_json))!;
    }

    private async Task PublishPairAsync(
        string version,
        string apiUrl,
        string migUrl,
        string apiSha,
        string migSha,
        long apiSize,
        long migSize,
        Guid schoolId)
    {
        var request = new CreateUpdateReleaseRequest
        {
            Version = version,
            Channel = "DEV",
            ProtocolVersion = 2,
            FromSchemaVersion = 1,
            SchemaVersion = 1,
            MinimumDesktopVersion = "1.0.0",
            MinimumApiVersion = "1.0.0",
            Artifacts =
            [
                new CreateUpdateReleaseArtifactRequest
                {
                    Type = "Api",
                    Version = version,
                    Url = apiUrl,
                    Size = apiSize,
                    Sha256 = apiSha,
                },
                new CreateUpdateReleaseArtifactRequest
                {
                    Type = "Migration",
                    Version = version,
                    Url = migUrl,
                    Size = migSize,
                    Sha256 = migSha,
                },
            ],
            Targets = [new CreateUpdateReleaseTargetRequest { SchoolId = schoolId }],
        };
        var client = Publisher();
        var created = await client.PostAsJsonAsync("/api/v1/releases", request);
        created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json))!;
        (await client.PutAsJsonAsync($"/api/v1/releases/{draft.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();
    }

    private async Task PublishDesktopOnlyAsync(string version)
    {
        var request = new CreateUpdateReleaseRequest
        {
            Version = version,
            Channel = "DEV",
            ProtocolVersion = 1,
            FromSchemaVersion = 1,
            SchemaVersion = 1,
            MinimumDesktopVersion = "1.0.0",
            MinimumApiVersion = "1.0.0",
            Artifacts =
            [
                new CreateUpdateReleaseArtifactRequest
                {
                    Type = "Desktop",
                    Version = version,
                    Url = "http://127.0.0.1/DesktopSetup.exe",
                    Size = 12,
                    Sha256 = new string('a', 64),
                },
            ],
        };
        var client = Publisher();
        var created = await client.PostAsJsonAsync("/api/v1/releases", request);
        created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json))!;
        (await client.PutAsJsonAsync($"/api/v1/releases/{draft.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();
    }

    private async Task<Guid> SeedSchoolAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
        var id = Guid.NewGuid();
        db.SchoolRegistry.Add(new BootstrapSchoolRegistryEntry
        {
            SchoolId = id,
            SchoolName = name,
            ActivationBaseUrl = "http://127.0.0.1:5096",
            CloudBaseUrl = "http://127.0.0.1:1804",
            ServerInstanceId = Guid.NewGuid(),
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return id;
    }
}
