using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SchoolManagement.Application.ParentActivation.BootstrapRelay;
using Xunit;

namespace SchoolManagement.Bootstrap.UnitTests;

public sealed class RegistryRelayAuthTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public RegistryRelayAuthTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upsert_WithoutRelayKey_Returns401_Missing()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/registry/schools/upsert", MinimalUpsertBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(_json);
        body!.Error.Should().Be("Clé relay Bootstrap manquante.");
    }

    [Fact]
    public async Task Upsert_WithWrongRelayKey_Returns401_Invalid()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            BootstrapRelayAuthConstants.LegacySharedKeyHeaderName,
            "wrong-key");

        var response = await client.PostAsJsonAsync("/registry/schools/upsert", MinimalUpsertBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(_json);
        body!.Error.Should().Be("Clé relay Bootstrap invalide.");
    }

    [Fact]
    public async Task Upsert_WithCorrectRelayKey_Returns200()
    {
        using var client = CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/registry/schools/upsert", MinimalUpsertBody());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<UpsertOkBody>(_json);
        payload!.SchoolId.Should().NotBe(Guid.Empty);
        payload.SchoolName.Should().Be("ECOLE AUTH");
        payload.IsActive.Should().BeTrue();
        payload.ActiveCredentialId.Should().NotBeNull();
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            BootstrapRelayAuthConstants.LegacySharedKeyHeaderName,
            BootstrapWebApplicationFactory.TestRelayApiKey);
        return client;
    }

    private static object MinimalUpsertBody() => new
    {
        schoolId = Guid.NewGuid(),
        schoolName = "ECOLE AUTH",
        activationBaseUrl = "http://school.example:5096",
        cloudBaseUrl = "http://cloud.example:1804",
        credential = new
        {
            credentialId = Guid.NewGuid(),
            credentialVersion = 1,
            secretHash = "hash-auth-1",
            tokenType = "school_establishment",
        },
    };

    private sealed class ErrorBody
    {
        public string? Error { get; set; }
    }

    private sealed class UpsertOkBody
    {
        public Guid SchoolId { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public Guid? ActiveCredentialId { get; set; }
    }
}
