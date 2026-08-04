using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SchoolManagement.Application.ServerIdentity;
using Xunit;

namespace SchoolManagement.IntegrationTests;

[Collection("ApiIntegration")]
[Trait("Category", "Foundations")]
public class HealthDiscoveryEndpointTests
{
    private readonly HttpClient _client;

    public HealthDiscoveryEndpointTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Discovery_Health_Returns_Protocol_And_Identity()
    {
        var response = await _client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("protocolVersion").GetInt32().Should().Be(ConnectionProtocolConstants.ProtocolVersion);
        root.GetProperty("apiVersion").GetString().Should().Be(ConnectionProtocolConstants.ApiVersion);
        root.TryGetProperty("serverSignature", out var sig).Should().BeTrue();
        sig.ValueKind.Should().Be(JsonValueKind.Null);

        var identity = root.GetProperty("identity");
        identity.GetProperty("serverInstanceId").GetString().Should().NotBeNullOrEmpty();
        Guid.TryParse(identity.GetProperty("serverInstanceId").GetString(), out _).Should().BeTrue();
        identity.GetProperty("publicKeyFingerprint").GetString().Should().StartWith("sha256:");
        identity.GetProperty("keyVersion").GetInt32().Should().BeGreaterThan(0);
    }
}
