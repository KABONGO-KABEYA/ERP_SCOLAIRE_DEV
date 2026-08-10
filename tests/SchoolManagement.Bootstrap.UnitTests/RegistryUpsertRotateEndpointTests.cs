using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.ParentActivation.BootstrapRelay;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;
using Xunit;

namespace SchoolManagement.Bootstrap.UnitTests;

public sealed class RegistryUpsertRotateEndpointTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public RegistryUpsertRotateEndpointTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upsert_TwiceSameSchoolId_DoesNotCreateDuplicateRegistryRow()
    {
        var schoolId = Guid.NewGuid();
        var firstCredentialId = Guid.NewGuid();
        var secondCredentialId = Guid.NewGuid();

        using var client = CreateAuthenticatedClient();

        var first = await client.PostAsJsonAsync(
            "/registry/schools/upsert",
            UpsertBody(schoolId, "ECOLE A", firstCredentialId, version: 1, hash: "h1"));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync(
            "/registry/schools/upsert",
            UpsertBody(schoolId, "ECOLE A RENAMED", secondCredentialId, version: 2, hash: "h2"));
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPayload = await second.Content.ReadFromJsonAsync<UpsertOkBody>(_json);
        secondPayload!.SchoolName.Should().Be("ECOLE A RENAMED");
        secondPayload.ActiveCredentialId.Should().Be(secondCredentialId);
        secondPayload.ActiveCredentialVersion.Should().Be(2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();

        (await db.SchoolRegistry.CountAsync(s => s.SchoolId == schoolId)).Should().Be(1);
        (await db.SchoolRegistry.CountAsync()).Should().BeGreaterThanOrEqualTo(1);

        var credentials = await db.EstablishmentCredentials
            .Where(c => c.SchoolId == schoolId)
            .ToListAsync();
        credentials.Should().HaveCount(2);
        credentials.Count(c => c.Status == EstablishmentCredentialStatuses.Active).Should().Be(1);
        credentials.Single(c => c.Id == firstCredentialId).Status.Should().Be(EstablishmentCredentialStatuses.Revoked);
        credentials.Single(c => c.Id == secondCredentialId).Status.Should().Be(EstablishmentCredentialStatuses.Active);
    }

    [Fact]
    public async Task Rotate_RevokesPrevious_ThenActivatesNew()
    {
        var schoolId = Guid.NewGuid();
        var firstCredentialId = Guid.NewGuid();
        var secondCredentialId = Guid.NewGuid();

        using var client = CreateAuthenticatedClient();

        var upsert = await client.PostAsJsonAsync(
            "/registry/schools/upsert",
            UpsertBody(schoolId, "ECOLE ROTATE", firstCredentialId, version: 1, hash: "hash-v1"));
        upsert.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotate = await client.PostAsJsonAsync(
            $"/registry/schools/{schoolId:D}/credentials/rotate",
            new
            {
                reason = "Admin rotation",
                credential = new
                {
                    credentialId = secondCredentialId,
                    credentialVersion = 2,
                    secretHash = "hash-v2",
                    tokenType = "school_establishment",
                },
            });

        rotate.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await rotate.Content.ReadFromJsonAsync<RotateOkBody>(_json);
        payload!.SchoolId.Should().Be(schoolId);
        payload.RevokedCredentialId.Should().Be(firstCredentialId);
        payload.RevokedReason.Should().Be("Admin rotation");
        payload.ActiveCredentialId.Should().Be(secondCredentialId);
        payload.ActiveCredentialVersion.Should().Be(2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
        var old = await db.EstablishmentCredentials.AsNoTracking()
            .SingleAsync(c => c.Id == firstCredentialId);
        var neu = await db.EstablishmentCredentials.AsNoTracking()
            .SingleAsync(c => c.Id == secondCredentialId);

        old.Status.Should().Be(EstablishmentCredentialStatuses.Revoked);
        old.RevokedAtUtc.Should().NotBeNull();
        old.RevokedReason.Should().Be("Admin rotation");
        neu.Status.Should().Be(EstablishmentCredentialStatuses.Active);
        neu.RevokedAtUtc.Should().BeNull();

        (await db.EstablishmentCredentials.CountAsync(
            c => c.SchoolId == schoolId && c.Status == EstablishmentCredentialStatuses.Active))
            .Should().Be(1);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            BootstrapRelayAuthConstants.LegacySharedKeyHeaderName,
            BootstrapWebApplicationFactory.TestRelayApiKey);
        return client;
    }

    private static object UpsertBody(
        Guid schoolId,
        string name,
        Guid credentialId,
        int version,
        string hash) => new
    {
        schoolId,
        schoolName = name,
        activationBaseUrl = "http://school.example:5096",
        cloudBaseUrl = "http://cloud.example:1804",
        credential = new
        {
            credentialId,
            credentialVersion = version,
            secretHash = hash,
            tokenType = "school_establishment",
        },
    };

    private sealed class UpsertOkBody
    {
        public Guid SchoolId { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public Guid? ActiveCredentialId { get; set; }
        public int? ActiveCredentialVersion { get; set; }
    }

    private sealed class RotateOkBody
    {
        public Guid SchoolId { get; set; }
        public Guid RevokedCredentialId { get; set; }
        public string RevokedReason { get; set; } = string.Empty;
        public Guid ActiveCredentialId { get; set; }
        public int ActiveCredentialVersion { get; set; }
    }
}
