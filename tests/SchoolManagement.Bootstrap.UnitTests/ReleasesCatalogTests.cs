using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Bootstrap.API.Contracts;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;
using SchoolManagement.Bootstrap.API.Security;
using SchoolManagement.Bootstrap.API.Services;
using Xunit;

namespace SchoolManagement.Bootstrap.UnitTests;

public sealed class ReleasesCatalogTests : IDisposable
{
    private static int _versionSeq;
    private const string ValidSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HttpsUrl = "https://example.com/release/DesktopSetup.exe";

    private readonly BootstrapWebApplicationFactory _factory = new();
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Create_Draft_Returns201()
    {
        var client = Publisher();
        var version = UniqueVersion();
        var response = await client.PostAsJsonAsync("/api/v1/releases", DraftBody(version));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json);
        body!.Status.Should().Be("Draft");
        body.Version.Should().Be(version);
        body.Channel.Should().Be("PROD");
        body.Artifacts.Should().ContainSingle(a => a.Type == "Desktop" && a.Sha256 == ValidSha);
    }

    [Fact]
    public async Task Create_InvalidVersion_Returns400()
    {
        var client = Publisher();
        var response = await client.PostAsJsonAsync("/api/v1/releases", DraftBody("not-a-version"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_DuplicateChannelVersion_Returns409()
    {
        var client = Publisher();
        var version = UniqueVersion();
        (await client.PostAsJsonAsync("/api/v1/releases", DraftBody(version))).StatusCode.Should().Be(HttpStatusCode.Created);
        var response = await client.PostAsJsonAsync("/api/v1/releases", DraftBody(version));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Publish_Valid_ReturnsPublished()
    {
        var client = Publisher();
        var created = await CreateDraftAsync(client, UniqueVersion());
        var published = await client.PutAsJsonAsync(
            $"/api/v1/releases/{created.ReleaseId}/status",
            new { status = "Published" });
        published.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await published.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json);
        body!.Status.Should().Be("Published");
        body.PublishedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_Api_Without_Migration_Returns400()
    {
        var client = Publisher();
        var response = await client.PostAsJsonAsync("/api/v1/releases", DraftBody(UniqueVersion(), artifactType: "Api"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(_json);
        error!.Error.Should().Contain("Api");
        error.Error.Should().Contain("Migration");
    }

    [Fact]
    public async Task Create_Migration_Without_Api_Returns400()
    {
        var client = Publisher();
        var response = await client.PostAsJsonAsync(
            "/api/v1/releases",
            DraftBody(UniqueVersion(), artifactType: "Migration"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Artifact_Version_Mismatch_Returns400()
    {
        var client = Publisher();
        var body = DraftBody(UniqueVersion());
        body.Artifacts[0].Version = "9.9.9";
        var response = await client.PostAsJsonAsync("/api/v1/releases", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(_json);
        error!.Error.Should().Contain("version");
    }

    [Fact]
    public async Task Publish_Api_Migration_Without_Desktop_Succeeds()
    {
        var client = Publisher();
        var version = UniqueVersion();
        var created = await client.PostAsJsonAsync("/api/v1/releases", PairBody(version));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await created.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json);
        var published = await client.PutAsJsonAsync(
            $"/api/v1/releases/{draft!.ReleaseId}/status",
            new { status = "Published" });
        published.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await published.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json);
        body!.Artifacts.Should().Contain(a => a.Type == "Api");
        body.Artifacts.Should().Contain(a => a.Type == "Migration");
        body.FromSchemaVersion.Should().Be(1);
        body.SchemaVersion.Should().Be(3);
        body.ProtocolVersion.Should().Be(2);
    }

    [Fact]
    public async Task Public_Check_Desktop_Unchanged_When_Pair_Also_Present()
    {
        var client = Publisher();
        var version = UniqueVersion();
        var create = await client.PostAsJsonAsync("/api/v1/releases", PairBody(version, includeDesktop: true));
        create.EnsureSuccessStatusCode();
        var draft = await create.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json);
        (await client.PutAsJsonAsync($"/api/v1/releases/{draft!.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();

        var check = await Anonymous().GetAsync("/api/v1/releases/check?channel=PROD&artifactType=Desktop");
        check.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await check.Content.ReadFromJsonAsync<UpdateReleaseCheckResponse>(_json);
        body!.ReleaseId.Should().Be(draft.ReleaseId);
        body.Artifact.Type.Should().Be("Desktop");
    }

    [Fact]
    public async Task Public_Check_Desktop_NoContent_When_Only_Api_Migration()
    {
        var client = Publisher();
        var create = await client.PostAsJsonAsync("/api/v1/releases", PairBody(UniqueVersion()));
        create.EnsureSuccessStatusCode();
        var draft = await create.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json);
        (await client.PutAsJsonAsync($"/api/v1/releases/{draft!.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();

        var check = await Anonymous().GetAsync("/api/v1/releases/check?channel=PROD");
        check.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Create_Api_Pair_With_Wrong_Protocol_Returns400()
    {
        var client = Publisher();
        var body = PairBody(UniqueVersion());
        body.ProtocolVersion = 1;
        var response = await client.PostAsJsonAsync("/api/v1/releases", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>(_json);
        error!.Error.Should().Contain("ProtocolVersion");
    }

    [Fact]
    public async Task Create_InvalidSha_Returns400()
    {
        var client = Publisher();
        var body = DraftBody(UniqueVersion());
        body.Artifacts[0].Sha256 = "zzzz";
        var response = await client.PostAsJsonAsync("/api/v1/releases", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Block_Published_Succeeds()
    {
        var client = Publisher();
        var created = await CreateDraftAsync(client, UniqueVersion());
        (await client.PutAsJsonAsync($"/api/v1/releases/{created.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();
        var blocked = await client.PutAsJsonAsync(
            $"/api/v1/releases/{created.ReleaseId}/status",
            new { status = "Blocked", reason = "SHA incorrect" });
        blocked.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await blocked.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json);
        body!.Status.Should().Be("Blocked");
    }

    [Fact]
    public async Task Published_To_Draft_Is_Rejected()
    {
        var client = Publisher();
        var created = await CreateDraftAsync(client, UniqueVersion());
        (await client.PutAsJsonAsync($"/api/v1/releases/{created.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();
        var revert = await client.PutAsJsonAsync(
            $"/api/v1/releases/{created.ReleaseId}/status",
            new { status = "Draft" });
        revert.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_Published_Is_Rejected()
    {
        var client = Publisher();
        var created = await CreateDraftAsync(client, UniqueVersion());
        (await client.PutAsJsonAsync($"/api/v1/releases/{created.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IUpdateReleaseCatalog>();
        var act = async () => await catalog.DeleteDraftAsync(created.ReleaseId, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<CatalogException>(act);
        ex.StatusCode.Should().Be(409);

        var httpDelete = await client.DeleteAsync($"/api/v1/releases/{created.ReleaseId}");
        httpDelete.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Dev_Is_Invisible_From_Prod_Check()
    {
        var client = Publisher();
        var created = await CreateDraftAsync(client, UniqueVersion(), channel: "DEV");
        (await client.PutAsJsonAsync($"/api/v1/releases/{created.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();
        var check = await Anonymous().GetAsync("/api/v1/releases/check?channel=PROD");
        check.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Prod_Is_Invisible_From_Dev_Check()
    {
        var client = Publisher();
        var created = await CreateDraftAsync(client, UniqueVersion(), channel: "PROD");
        (await client.PutAsJsonAsync($"/api/v1/releases/{created.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();
        var check = await Anonymous().GetAsync("/api/v1/releases/check?channel=DEV");
        check.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Global_Release_Visible_Without_And_With_SchoolId()
    {
        var client = Publisher();
        var schoolA = await SeedSchoolAsync("ECOLE A");
        var created = await CreateDraftAsync(client, UniqueVersion());
        (await client.PutAsJsonAsync($"/api/v1/releases/{created.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();

        (await Anonymous().GetAsync("/api/v1/releases/check?channel=PROD")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Anonymous().GetAsync($"/api/v1/releases/check?channel=PROD&schoolId={schoolA:D}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Targeted_Release_Visible_Only_To_School_A()
    {
        var client = Publisher();
        var schoolA = await SeedSchoolAsync("ECOLE A");
        var schoolB = await SeedSchoolAsync("ECOLE B");
        var created = await CreateDraftAsync(client, UniqueVersion(), schoolId: schoolA);
        (await client.PutAsJsonAsync($"/api/v1/releases/{created.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();

        (await Anonymous().GetAsync($"/api/v1/releases/check?channel=PROD&schoolId={schoolA:D}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Anonymous().GetAsync($"/api/v1/releases/check?channel=PROD&schoolId={schoolB:D}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Anonymous().GetAsync("/api/v1/releases/check?channel=PROD"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Check_Selects_Highest_SemVer()
    {
        var client = Publisher();
        var v1 = UniqueVersion();
        var v2 = UniqueVersion();
        // Ensure v2 > v1 by using explicit pair.
        v1 = $"2.0.{Interlocked.Increment(ref _versionSeq)}";
        v2 = $"2.1.{Interlocked.Increment(ref _versionSeq)}";
        var older = await CreateDraftAsync(client, v1);
        var newer = await CreateDraftAsync(client, v2);
        (await client.PutAsJsonAsync($"/api/v1/releases/{older.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/v1/releases/{newer.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();

        var check = await Anonymous().GetAsync("/api/v1/releases/check?channel=PROD");
        check.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await check.Content.ReadFromJsonAsync<UpdateReleaseCheckResponse>(_json);
        body!.Version.Should().Be(v2);
        body.Status.Should().Be("Published");
        body.Artifact.Sha256.Should().Be(ValidSha);
    }

    [Fact]
    public async Task Post_Without_Key_Returns401()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/v1/releases", DraftBody(UniqueVersion()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Wrong_Key_Returns401()
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Add(ReleasePublishKeyAuthorizationFilter.HeaderName, "wrong-key");
        var response = await client.PostAsJsonAsync("/api/v1/releases", DraftBody(UniqueVersion()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Without_Key_Returns401()
    {
        var created = await CreateDraftAsync(Publisher(), UniqueVersion());
        var response = await Anonymous().PutAsJsonAsync(
            $"/api/v1/releases/{created.ReleaseId}/status",
            new { status = "Published" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Wrong_Key_Returns401()
    {
        var created = await CreateDraftAsync(Publisher(), UniqueVersion());
        var client = Anonymous();
        client.DefaultRequestHeaders.Add(ReleasePublishKeyAuthorizationFilter.HeaderName, "wrong-key");
        var response = await client.PutAsJsonAsync(
            $"/api/v1/releases/{created.ReleaseId}/status",
            new { status = "Published" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Check_Without_Key_Returns200Or204()
    {
        var response = await Anonymous().GetAsync("/api/v1/releases/check?channel=PROD");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Get_Draft_Without_Key_Returns404()
    {
        var created = await CreateDraftAsync(Publisher(), UniqueVersion());
        var response = await Anonymous().GetAsync($"/api/v1/releases/{created.ReleaseId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Missing_Publish_Key_Configuration_Returns503()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Bootstrap:ReleasePublishApiKey", string.Empty);
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<BootstrapOptions>(options => options.ReleasePublishApiKey = string.Empty);
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            ReleasePublishKeyAuthorizationFilter.HeaderName,
            "any-value");
        var response = await client.PostAsJsonAsync("/api/v1/releases", DraftBody(UniqueVersion()));
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 63
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public async Task Create_Rejects_Invalid_Sha(string sha)
    {
        var client = Publisher();
        var body = DraftBody(UniqueVersion());
        body.Artifacts[0].Sha256 = sha;
        var response = await client.PostAsJsonAsync("/api/v1/releases", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Accepts_Valid_Sha64()
    {
        var client = Publisher();
        var response = await client.PostAsJsonAsync("/api/v1/releases", DraftBody(UniqueVersion()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_Https_Url_Accepted()
    {
        var client = Publisher();
        var body = DraftBody(UniqueVersion());
        body.Artifacts[0].Url = HttpsUrl;
        (await client.PostAsJsonAsync("/api/v1/releases", body)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_Http_Public_Url_Rejected()
    {
        var client = Publisher();
        var body = DraftBody(UniqueVersion(), channel: "PROD");
        body.Artifacts[0].Url = "http://169.58.93.203/pkg.exe";
        (await client.PostAsJsonAsync("/api/v1/releases", body)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Relative_Url_Rejected()
    {
        var client = Publisher();
        var body = DraftBody(UniqueVersion());
        body.Artifacts[0].Url = "/release/1.2.0/pkg.exe";
        (await client.PostAsJsonAsync("/api/v1/releases", body)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dev_Allows_Http_Loopback()
    {
        var client = Publisher();
        var body = DraftBody(UniqueVersion(), channel: "DEV");
        body.Artifacts[0].Url = "http://127.0.0.1/pkg.exe";
        (await client.PostAsJsonAsync("/api/v1/releases", body)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static CreateUpdateReleaseRequest PairBody(
        string version,
        string channel = "PROD",
        bool includeDesktop = false)
    {
        var url = channel == "DEV" ? "http://127.0.0.1/pkg.exe" : HttpsUrl;
        var artifacts = new List<CreateUpdateReleaseArtifactRequest>();
        if (includeDesktop)
        {
            artifacts.Add(new CreateUpdateReleaseArtifactRequest
            {
                Type = "Desktop",
                Version = version,
                Url = url,
                Size = 12,
                Sha256 = ValidSha,
            });
        }

        artifacts.Add(new CreateUpdateReleaseArtifactRequest
        {
            Type = "Api",
            Version = version,
            Url = url,
            Size = 12,
            Sha256 = ValidSha,
        });
        artifacts.Add(new CreateUpdateReleaseArtifactRequest
        {
            Type = "Migration",
            Version = version,
            Url = url,
            Size = 12,
            Sha256 = ValidSha,
        });

        return new CreateUpdateReleaseRequest
        {
            Version = version,
            Channel = channel,
            ProtocolVersion = 2,
            FromSchemaVersion = 1,
            SchemaVersion = 3,
            MinimumDesktopVersion = "1.0.0",
            MinimumApiVersion = version,
            Artifacts = artifacts,
        };
    }

    private HttpClient Publisher()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            ReleasePublishKeyAuthorizationFilter.HeaderName,
            BootstrapWebApplicationFactory.TestReleasePublishApiKey);
        return client;
    }

    private HttpClient Anonymous() => _factory.CreateClient();

    private static string UniqueVersion() =>
        $"1.8.{Interlocked.Increment(ref _versionSeq)}";

    private static CreateUpdateReleaseRequest DraftBody(
        string version,
        string channel = "PROD",
        string artifactType = "Desktop",
        Guid? schoolId = null)
    {
        var request = new CreateUpdateReleaseRequest
        {
            Version = version,
            Channel = channel,
            ProtocolVersion = 1,
            SchemaVersion = 3,
            MinimumDesktopVersion = "1.0.0",
            MinimumApiVersion = "1.0.0",
            Mandatory = false,
            ReleaseNotes = ["test"],
            Artifacts =
            [
                new CreateUpdateReleaseArtifactRequest
                {
                    Type = artifactType,
                    Version = version,
                    Url = channel == "DEV" ? "http://127.0.0.1/pkg.exe" : HttpsUrl,
                    Size = 12,
                    Sha256 = ValidSha,
                }
            ],
        };
        if (schoolId is not null)
        {
            request.Targets = [new CreateUpdateReleaseTargetRequest { SchoolId = schoolId }];
        }

        return request;
    }

    private async Task<UpdateReleaseResponse> CreateDraftAsync(
        HttpClient client,
        string version,
        string channel = "PROD",
        Guid? schoolId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/releases", DraftBody(version, channel, schoolId: schoolId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json))!;
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
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private sealed class ErrorBody
    {
        public string? Error { get; set; }
    }
}
