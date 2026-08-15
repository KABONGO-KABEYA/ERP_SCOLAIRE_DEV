using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Bootstrap.API.Contracts;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;
using SchoolManagement.Bootstrap.API.Security;
using Xunit;

namespace SchoolManagement.Bootstrap.UnitTests;

public sealed class UpdateAgentAuthTests : IDisposable
{
    private static int _versionSeq;
    private const string ValidSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HttpsUrl = "https://example.com/release/DesktopSetup.exe";

    private readonly BootstrapWebApplicationFactory _factory = new();
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private readonly JwtSecurityTokenHandler _jwtHandler = new() { MapInboundClaims = false };

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Create_Credential_Returns201_WithSecretOnce()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var response = await Provisioner().PostAsJsonAsync(
            "/api/v1/agent/credentials",
            new { schoolId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UpdateAgentCredentialSecretResponse>(_json);
        body!.ClientId.Should().NotBeEmpty();
        body.SchoolId.Should().Be(schoolId);
        body.Status.Should().Be(UpdateAgentCredentialStatuses.Active);
        body.ClientSecret.Should().NotBeNullOrWhiteSpace();
        body.CredentialVersion.Should().Be(1);
    }

    [Fact]
    public async Task Secret_Is_Hashed_And_Not_Recoverable()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
        var row = await db.UpdateAgentCredentials.SingleAsync(c => c.Id == created.ClientId);
        row.SecretHash.Should().NotBe(created.ClientSecret);
        row.SecretHash.Should().HaveLength(64);
        row.SecretHash.Should().MatchRegex("^[0-9a-f]{64}$");
        UpdateAgentSecret.Matches(created.ClientSecret, row.SecretHash).Should().BeTrue();

        var list = await Provisioner().GetAsync($"/api/v1/agent/credentials?schoolId={schoolId:D}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await list.Content.ReadAsStringAsync();
        json.Should().NotContain(created.ClientSecret);
        json.Should().NotContain(row.SecretHash);
        json.Should().NotContain("secretHash");
        json.Should().NotContain("clientSecret");
    }

    [Fact]
    public async Task Token_Valid_ReturnsBearer_WithUniqueJti()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);
        var first = await IssueTokenAsync(created.ClientId, created.ClientSecret);
        first.TokenType.Should().Be("Bearer");
        first.SchoolId.Should().Be(schoolId);
        first.ExpiresIn.Should().BeGreaterThan(0);

        var second = await IssueTokenAsync(created.ClientId, created.ClientSecret);
        var jwt1 = _jwtHandler.ReadJwtToken(first.AccessToken);
        var jwt2 = _jwtHandler.ReadJwtToken(second.AccessToken);
        jwt1.Subject.Should().Be(created.ClientId.ToString("D"));
        jwt1.Audiences.Should().Contain(UpdateAgentTokenConstants.Audience);
        jwt1.Issuer.Should().Be(UpdateAgentTokenConstants.Issuer);
        jwt1.Claims.Should().Contain(c =>
            c.Type == UpdateAgentTokenConstants.TokenTypeClaim
            && c.Value == UpdateAgentTokenConstants.TokenTypeValue);
        jwt1.Claims.Should().Contain(c =>
            c.Type == UpdateAgentTokenConstants.SchoolIdClaim && c.Value == schoolId.ToString("D"));

        var jti1 = Guid.Parse(jwt1.Id);
        var jti2 = Guid.Parse(jwt2.Id);
        jti1.Should().NotBe(created.ClientId);
        jti2.Should().NotBe(created.ClientId);
        jti1.Should().NotBe(jti2);
    }

    [Fact]
    public async Task Token_WrongSecret_Returns401()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);
        var response = await Anonymous().PostAsJsonAsync(
            "/api/v1/agent/token",
            new { clientId = created.ClientId, clientSecret = "not-the-secret" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_RevokedCredential_Returns401()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);
        (await Provisioner().PostAsJsonAsync(
            $"/api/v1/agent/credentials/{created.ClientId:D}/revoke",
            new { reason = "compromis" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Anonymous().PostAsJsonAsync(
            "/api/v1/agent/token",
            new { clientId = created.ClientId, clientSecret = created.ClientSecret });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Inactive_School_Returns403()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);
        var token = await IssueTokenAsync(created.ClientId, created.ClientSecret);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
            var school = await db.SchoolRegistry.SingleAsync(s => s.SchoolId == schoolId);
            school.IsActive = false;
            await db.SaveChangesAsync();
        }

        var check = await AgentClient(token.AccessToken).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        check.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var tokenAgain = await Anonymous().PostAsJsonAsync(
            "/api/v1/agent/token",
            new { clientId = created.ClientId, clientSecret = created.ClientSecret });
        tokenAgain.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Expired_Jwt_Returns401()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);
        var expired = UpdateAgentJwt.Create(
            BootstrapWebApplicationFactory.TestAgentJwtSigningKey,
            created.ClientId,
            schoolId,
            serverInstanceId: null,
            expiresUtc: DateTime.UtcNow.AddMinutes(-5));

        var response = await AgentClient(expired).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_BodySchoolId_Mismatch_Returns401()
    {
        var schoolA = await SeedSchoolAsync("ECOLE A");
        var schoolB = await SeedSchoolAsync("ECOLE B");
        var created = await CreateCredentialAsync(schoolA);
        var response = await Anonymous().PostAsJsonAsync(
            "/api/v1/agent/token",
            new
            {
                clientId = created.ClientId,
                clientSecret = created.ClientSecret,
                schoolId = schoolB,
            });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Jwt_SchoolA_Cannot_Read_Targeted_Release_Of_B()
    {
        var schoolA = await SeedSchoolAsync("ECOLE A");
        var schoolB = await SeedSchoolAsync("ECOLE B");
        var credA = await CreateCredentialAsync(schoolA);
        var tokenA = await IssueTokenAsync(credA.ClientId, credA.ClientSecret);
        await PublishReleaseAsync(schoolId: schoolB);

        var spoofed = await AgentClient(tokenA.AccessToken)
            .GetAsync($"/api/v1/agent/releases/check?channel=PROD&schoolId={schoolB:D}");
        spoofed.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Global_Release_Visible_To_SchoolA()
    {
        var schoolA = await SeedSchoolAsync("ECOLE A");
        var credA = await CreateCredentialAsync(schoolA);
        var tokenA = await IssueTokenAsync(credA.ClientId, credA.ClientSecret);
        var published = await PublishReleaseAsync(schoolId: null);

        var check = await AgentClient(tokenA.AccessToken).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        check.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await check.Content.ReadFromJsonAsync<UpdateAgentReleaseCheckResponse>(_json);
        body!.ReleaseId.Should().Be(published.ReleaseId);
        body.Api.Type.Should().Be("Api");
        body.Migration.Type.Should().Be("Migration");
        body.Api.Version.Should().Be(published.Version);
        body.Migration.Version.Should().Be(published.Version);
    }

    [Fact]
    public async Task Targeted_Release_A_Visible_To_A()
    {
        var schoolA = await SeedSchoolAsync("ECOLE A");
        var credA = await CreateCredentialAsync(schoolA);
        var tokenA = await IssueTokenAsync(credA.ClientId, credA.ClientSecret);
        var published = await PublishReleaseAsync(schoolId: schoolA);

        var check = await AgentClient(tokenA.AccessToken).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        check.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await check.Content.ReadFromJsonAsync<UpdateAgentReleaseCheckResponse>(_json);
        body!.ReleaseId.Should().Be(published.ReleaseId);
    }

    [Fact]
    public async Task Targeted_Release_B_Invisible_To_A()
    {
        var schoolA = await SeedSchoolAsync("ECOLE A");
        var schoolB = await SeedSchoolAsync("ECOLE B");
        var credA = await CreateCredentialAsync(schoolA);
        var tokenA = await IssueTokenAsync(credA.ClientId, credA.ClientSecret);
        await PublishReleaseAsync(schoolId: schoolB);

        var check = await AgentClient(tokenA.AccessToken).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        check.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Agent_Check_Desktop_Only_Returns204()
    {
        var schoolA = await SeedSchoolAsync("ECOLE A");
        var credA = await CreateCredentialAsync(schoolA);
        var tokenA = await IssueTokenAsync(credA.ClientId, credA.ClientSecret);

        var version = $"1.9.{Interlocked.Increment(ref _versionSeq)}";
        var request = new CreateUpdateReleaseRequest
        {
            Version = version,
            Channel = "PROD",
            ProtocolVersion = 1,
            FromSchemaVersion = 1,
            SchemaVersion = 3,
            MinimumDesktopVersion = "1.0.0",
            MinimumApiVersion = "1.0.0",
            Artifacts =
            [
                new CreateUpdateReleaseArtifactRequest
                {
                    Type = "Desktop",
                    Version = version,
                    Url = HttpsUrl,
                    Size = 12,
                    Sha256 = ValidSha,
                }
            ],
        };
        var client = Publisher();
        var created = await client.PostAsJsonAsync("/api/v1/releases", request);
        created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json))!;
        (await client.PutAsJsonAsync($"/api/v1/releases/{draft.ReleaseId}/status", new { status = "Published" }))
            .EnsureSuccessStatusCode();

        var check = await AgentClient(tokenA.AccessToken).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        check.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Check_Without_Bearer_Returns401()
    {
        var response = await Anonymous().GetAsync("/api/v1/agent/releases/check?channel=PROD");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Check_Wrong_Audience_Returns401()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);
        var jwt = UpdateAgentJwt.Create(
            BootstrapWebApplicationFactory.TestAgentJwtSigningKey,
            created.ClientId,
            schoolId,
            serverInstanceId: null,
            expiresUtc: DateTime.UtcNow.AddMinutes(30),
            audience: "erp-scolaire-mobile-establish");

        var response = await AgentClient(jwt).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Check_Wrong_TokenType_Returns401()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);
        var jwt = UpdateAgentJwt.Create(
            BootstrapWebApplicationFactory.TestAgentJwtSigningKey,
            created.ClientId,
            schoolId,
            serverInstanceId: null,
            expiresUtc: DateTime.UtcNow.AddMinutes(30),
            tokenType: "school_establishment");

        var response = await AgentClient(jwt).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Missing_Provision_Key_Configuration_Returns503()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Bootstrap:AgentProvisionApiKey", string.Empty);
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<BootstrapOptions>(options => options.AgentProvisionApiKey = string.Empty);
            });
        });
        var schoolId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
            db.SchoolRegistry.Add(new BootstrapSchoolRegistryEntry
            {
                SchoolId = schoolId,
                SchoolName = "ECOLE 503",
                ActivationBaseUrl = "http://127.0.0.1:5096",
                CloudBaseUrl = "http://127.0.0.1:1804",
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(AgentProvisionKeyAuthorizationFilter.HeaderName, "any-value");
        var response = await client.PostAsJsonAsync("/api/v1/agent/credentials", new { schoolId });
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Wrong_Provision_Key_Returns401()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var client = Anonymous();
        client.DefaultRequestHeaders.Add(AgentProvisionKeyAuthorizationFilter.HeaderName, "wrong-key");
        var response = await client.PostAsJsonAsync("/api/v1/agent/credentials", new { schoolId });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rotate_Issues_New_Secret_And_Invalidates_Old()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);
        var rotate = await Provisioner().PostAsJsonAsync(
            $"/api/v1/agent/credentials/{created.ClientId:D}/rotate",
            new { reason = "rotation test" });
        rotate.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await rotate.Content.ReadFromJsonAsync<UpdateAgentCredentialSecretResponse>(_json);
        rotated!.ClientId.Should().NotBe(created.ClientId);
        rotated.ClientSecret.Should().NotBe(created.ClientSecret);
        rotated.CredentialVersion.Should().Be(2);
        rotated.SchoolId.Should().Be(schoolId);

        var oldToken = await Anonymous().PostAsJsonAsync(
            "/api/v1/agent/token",
            new { clientId = created.ClientId, clientSecret = created.ClientSecret });
        oldToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newToken = await Anonymous().PostAsJsonAsync(
            "/api/v1/agent/token",
            new { clientId = rotated.ClientId, clientSecret = rotated.ClientSecret });
        newToken.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoke_A_Does_Not_Affect_B()
    {
        var schoolA = await SeedSchoolAsync("ECOLE A");
        var schoolB = await SeedSchoolAsync("ECOLE B");
        var credA = await CreateCredentialAsync(schoolA);
        var credB = await CreateCredentialAsync(schoolB);

        (await Provisioner().PostAsJsonAsync(
            $"/api/v1/agent/credentials/{credA.ClientId:D}/revoke",
            new { reason = "compromis A" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenA = await Anonymous().PostAsJsonAsync(
            "/api/v1/agent/token",
            new { clientId = credA.ClientId, clientSecret = credA.ClientSecret });
        tokenA.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var tokenB = await Anonymous().PostAsJsonAsync(
            "/api/v1/agent/token",
            new { clientId = credB.ClientId, clientSecret = credB.ClientSecret });
        tokenB.StatusCode.Should().Be(HttpStatusCode.OK);

        await PublishReleaseAsync(schoolId: null);
        var issuedB = await tokenB.Content.ReadFromJsonAsync<UpdateAgentTokenResponse>(_json);
        var checkB = await AgentClient(issuedB!.AccessToken).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        checkB.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Check_Revoked_Jwt_Returns401()
    {
        var schoolId = await SeedSchoolAsync("ECOLE A");
        var created = await CreateCredentialAsync(schoolId);
        var token = await IssueTokenAsync(created.ClientId, created.ClientSecret);
        (await Provisioner().PostAsJsonAsync(
            $"/api/v1/agent/credentials/{created.ClientId:D}/revoke",
            new { reason = "revoke after token" })).EnsureSuccessStatusCode();

        var check = await AgentClient(token.AccessToken).GetAsync("/api/v1/agent/releases/check?channel=PROD");
        check.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient Provisioner()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            AgentProvisionKeyAuthorizationFilter.HeaderName,
            BootstrapWebApplicationFactory.TestAgentProvisionApiKey);
        return client;
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

    private HttpClient AgentClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<UpdateAgentCredentialSecretResponse> CreateCredentialAsync(Guid schoolId)
    {
        var response = await Provisioner().PostAsJsonAsync("/api/v1/agent/credentials", new { schoolId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UpdateAgentCredentialSecretResponse>(_json))!;
    }

    private async Task<UpdateAgentTokenResponse> IssueTokenAsync(Guid clientId, string clientSecret)
    {
        var response = await Anonymous().PostAsJsonAsync(
            "/api/v1/agent/token",
            new { clientId, clientSecret });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UpdateAgentTokenResponse>(_json))!;
    }

    private async Task<UpdateReleaseResponse> PublishReleaseAsync(Guid? schoolId)
    {
        var version = $"1.9.{Interlocked.Increment(ref _versionSeq)}";
        var request = new CreateUpdateReleaseRequest
        {
            Version = version,
            Channel = "PROD",
            ProtocolVersion = 2,
            FromSchemaVersion = 1,
            SchemaVersion = 3,
            MinimumDesktopVersion = "1.0.0",
            MinimumApiVersion = "1.0.0",
            Artifacts =
            [
                new CreateUpdateReleaseArtifactRequest
                {
                    Type = "Desktop",
                    Version = version,
                    Url = HttpsUrl,
                    Size = 12,
                    Sha256 = ValidSha,
                },
                new CreateUpdateReleaseArtifactRequest
                {
                    Type = "Api",
                    Version = version,
                    Url = HttpsUrl,
                    Size = 12,
                    Sha256 = ValidSha,
                },
                new CreateUpdateReleaseArtifactRequest
                {
                    Type = "Migration",
                    Version = version,
                    Url = HttpsUrl,
                    Size = 12,
                    Sha256 = ValidSha,
                },
            ],
        };
        if (schoolId is not null)
        {
            request.Targets = [new CreateUpdateReleaseTargetRequest { SchoolId = schoolId }];
        }

        var client = Publisher();
        var created = await client.PostAsJsonAsync("/api/v1/releases", request);
        created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json))!;
        var published = await client.PutAsJsonAsync(
            $"/api/v1/releases/{draft.ReleaseId}/status",
            new { status = "Published" });
        published.EnsureSuccessStatusCode();
        return (await published.Content.ReadFromJsonAsync<UpdateReleaseResponse>(_json))!;
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
