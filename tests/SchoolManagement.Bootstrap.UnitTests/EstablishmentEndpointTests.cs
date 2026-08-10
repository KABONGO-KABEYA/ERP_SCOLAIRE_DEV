using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.ParentActivation;
using SchoolManagement.Bootstrap.API.Establishment;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;
using Xunit;

namespace SchoolManagement.Bootstrap.UnitTests;

public sealed class EstablishmentEndpointTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private const string SecretHash = "phase3-establishment-hmac-secret-hash";
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public EstablishmentEndpointTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StartThenComplete_ReturnsSchoolBinding_WithEstablishmentExtensions()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var deviceId = "device-success-1";
        await SeedSchoolAsync(schoolId, credentialId, version: 1, status: EstablishmentCredentialStatuses.Active);

        var token = EstablishmentJwtValidator.CreateSignedToken(schoolId, credentialId, 1, SecretHash);
        using var client = _factory.CreateClient();

        var start = await client.PostAsJsonAsync("/establishment/start", new
        {
            token,
            deviceId,
            clientHints = new { platform = "test" },
        });

        start.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await start.Content.ReadFromJsonAsync<StartOk>(_json);
        session!.Status.Should().Be("pending");
        session.SchoolId.Should().Be(schoolId);
        session.DeviceId.Should().Be(deviceId);
        session.EstablishmentSessionId.Should().NotBe(Guid.Empty);

        var complete = await client.PostAsJsonAsync("/establishment/complete", new
        {
            establishmentSessionId = session.EstablishmentSessionId,
            deviceId,
        });

        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var binding = await complete.Content.ReadFromJsonAsync<BindingOk>(_json);
        binding!.SchoolId.Should().Be(schoolId);
        binding.SchoolName.Should().Be("ECOLE ESTABLISH");
        binding.CloudBaseUrl.Should().Be("http://cloud.example:1804");
        binding.ActivationTokenId.Should().Be(credentialId);
        binding.ActivationSessionId.Should().Be(session.EstablishmentSessionId);
        binding.DeviceId.Should().Be(deviceId);
        binding.ProtocolVersion.Should().Be(2);
        binding.Extensions.Should().NotBeNull();
        binding.Extensions!["bindingKind"]!.ToString().Should().Be("school_establishment");
        Convert.ToInt32(binding.Extensions["establishmentCredentialVersion"]!.ToString())
            .Should().Be(1);
    }

    [Fact]
    public async Task Start_WithParentActivationTokenType_Returns400()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        await SeedSchoolAsync(schoolId, credentialId, 1, EstablishmentCredentialStatuses.Active);

        var token = EstablishmentJwtValidator.CreateSignedToken(
            schoolId,
            credentialId,
            1,
            SecretHash,
            tokenType: "parent_activation");

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/establishment/start", new { token, deviceId = "d1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(response)).Should().Be("Token non valide pour l'établissement (type incorrect).");
    }

    [Fact]
    public async Task Start_WithBadSignature_Returns401()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        await SeedSchoolAsync(schoolId, credentialId, 1, EstablishmentCredentialStatuses.Active);

        var token = EstablishmentJwtValidator.CreateSignedToken(
            schoolId,
            credentialId,
            1,
            "wrong-hmac-secret-hash");

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/establishment/start", new { token, deviceId = "d1" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadErrorAsync(response)).Should().Be("Token établissement invalide.");
    }

    [Fact]
    public async Task Start_UnknownSchool_Returns404()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        // Pas de seed registre — école inconnue.
        var token = EstablishmentJwtValidator.CreateSignedToken(schoolId, credentialId, 1, SecretHash);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/establishment/start", new { token, deviceId = "d1" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadErrorAsync(response)).Should().Be("École introuvable dans le registre Bootstrap.");
    }

    [Fact]
    public async Task Start_RevokedCredential_Returns403()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        await SeedSchoolAsync(schoolId, credentialId, 1, EstablishmentCredentialStatuses.Revoked);

        var token = EstablishmentJwtValidator.CreateSignedToken(schoolId, credentialId, 1, SecretHash);
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/establishment/start", new { token, deviceId = "d1" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadErrorAsync(response))
            .Should().Be("QR établissement révoqué. Demandez un nouveau QR à l'école.");
    }

    [Fact]
    public async Task Start_WrongCredentialVersion_Returns400()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        await SeedSchoolAsync(schoolId, credentialId, version: 2, status: EstablishmentCredentialStatuses.Active);

        var token = EstablishmentJwtValidator.CreateSignedToken(schoolId, credentialId, credentialVersion: 1, SecretHash);
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/establishment/start", new { token, deviceId = "d1" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(response)).Should().Be("Version de credential invalide.");
    }

    [Fact]
    public async Task Complete_WrongDeviceId_Returns400()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        await SeedSchoolAsync(schoolId, credentialId, 1, EstablishmentCredentialStatuses.Active);
        var token = EstablishmentJwtValidator.CreateSignedToken(schoolId, credentialId, 1, SecretHash);

        using var client = _factory.CreateClient();
        var start = await client.PostAsJsonAsync("/establishment/start", new { token, deviceId = "device-A" });
        start.EnsureSuccessStatusCode();
        var session = await start.Content.ReadFromJsonAsync<StartOk>(_json);

        var complete = await client.PostAsJsonAsync("/establishment/complete", new
        {
            establishmentSessionId = session!.EstablishmentSessionId,
            deviceId = "device-B",
        });

        complete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(complete)).Should().Be("DeviceId incompatible.");
    }

    [Fact]
    public async Task Complete_ExpiredSession_Returns400()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        await SeedSchoolAsync(schoolId, credentialId, 1, EstablishmentCredentialStatuses.Active);

        Guid sessionId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IBootstrapSchoolRegistryRepository>();
            var session = await repo.CreateSessionAsync(
                schoolId,
                credentialId,
                "device-expired",
                DateTime.UtcNow.AddMinutes(-5));
            sessionId = session.Id;
        }

        using var client = _factory.CreateClient();
        var complete = await client.PostAsJsonAsync("/establishment/complete", new
        {
            establishmentSessionId = sessionId,
            deviceId = "device-expired",
        });

        complete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(complete)).Should().Be("Session d'établissement expirée.");
    }

    [Fact]
    public async Task ActivationStart_RejectsSchoolEstablishmentToken()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var token = EstablishmentJwtValidator.CreateSignedToken(schoolId, credentialId, 1, SecretHash);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/activation/start", new
        {
            token,
            deviceId = "d1",
            bootstrapSessionId = (Guid?)null,
            clientHints = (object?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadErrorAsync(response))
            .Should().Be(ParentActivationTokenTypeGuard.RejectedEstablishmentMessage);
    }

    private async Task SeedSchoolAsync(
        Guid schoolId,
        Guid credentialId,
        int version,
        string status)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IBootstrapSchoolRegistryRepository>();
        await repo.UpsertSchoolAsync(new BootstrapSchoolRegistryUpsertRequest
        {
            SchoolId = schoolId,
            SchoolName = "ECOLE ESTABLISH",
            ActivationBaseUrl = "http://school.example:5096",
            CloudBaseUrl = "http://cloud.example:1804",
            ServerInstanceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Credential = new BootstrapCredentialUpsert
            {
                CredentialId = credentialId,
                CredentialVersion = version,
                SecretHash = SecretHash,
            },
        });

        if (string.Equals(status, EstablishmentCredentialStatuses.Revoked, StringComparison.OrdinalIgnoreCase))
        {
            var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
            var row = await db.EstablishmentCredentials.SingleAsync(c => c.Id == credentialId);
            row.Status = EstablishmentCredentialStatuses.Revoked;
            row.RevokedAtUtc = DateTime.UtcNow;
            row.RevokedReason = "test-revoke";
            await db.SaveChangesAsync();
        }
    }

    private async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(_json);
        return body?.Error ?? string.Empty;
    }

    private sealed class ErrorBody
    {
        public string? Error { get; set; }
    }

    private sealed class StartOk
    {
        public Guid EstablishmentSessionId { get; set; }
        public Guid SchoolId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    private sealed class BindingOk
    {
        public Guid SchoolId { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public string CloudBaseUrl { get; set; } = string.Empty;
        public Guid ActivationTokenId { get; set; }
        public Guid ActivationSessionId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public int ProtocolVersion { get; set; }
        public Dictionary<string, object?>? Extensions { get; set; }
    }
}
