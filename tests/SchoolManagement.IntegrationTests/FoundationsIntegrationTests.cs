using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SchoolManagement.Application.ServerIdentity;
using Xunit;

namespace SchoolManagement.IntegrationTests;

/// <summary>Non-régression intégrée des fondations (health discovery + garde clé AES).</summary>
[Collection("ApiIntegration")]
[Trait("Category", "Foundations")]
[Trait("Category", "LiveSql")]
public sealed class FoundationsIntegrationTests
{
    private readonly HttpClient _client;

    public FoundationsIntegrationTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Discovery_Health_Legacy_Fields_Remain_For_Old_Clients()
    {
        var response = await _client.GetAsync("/api/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("server").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("school").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("version").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("time").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Discovery_Health_Includes_V2_Identity_Fields()
    {
        var response = await _client.GetAsync("/api/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("protocolVersion").GetInt32().Should().Be(ConnectionProtocolConstants.ProtocolVersion);
        root.GetProperty("apiVersion").GetString().Should().Be(ConnectionProtocolConstants.ApiVersion);
        root.TryGetProperty("serverSignature", out var sig).Should().BeTrue();
        sig.ValueKind.Should().Be(JsonValueKind.Null);

        var identity = root.GetProperty("identity");
        identity.GetProperty("publicKeyFingerprint").GetString().Should().StartWith("sha256:");
        identity.GetProperty("keyVersion").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task V1_Health_Endpoint_Remains_Available()
    {
        var response = await _client.GetAsync("/api/v1/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Trait("Category", "Foundations")]
public sealed class ProductionStartupGuardFoundationTests
{
    [Fact]
    public void Host_Build_Fails_When_Production_Cloud_Without_Encryption_Key()
    {
        if (OperatingSystem.IsWindows())
        {
            // Garde Production Windows local : DPAPI — scénario couvert par ProductionEncryptionKeyGuardFoundationTests.
            return;
        }

        var previousEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var previousKey = Environment.GetEnvironmentVariable("ERP_CONFIG_ENCRYPTION_KEY");
        var previousRole = Environment.GetEnvironmentVariable("Deployment__Role");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            Environment.SetEnvironmentVariable("ERP_CONFIG_ENCRYPTION_KEY", null);
            Environment.SetEnvironmentVariable("Deployment__Role", "Cloud");

            var act = () => ProductionEncryptionKeyGuard.EnsureConfigured(
                new ProductionTestHostEnvironment(),
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Deployment:Role"] = "Cloud" })
                    .Build());

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnv);
            Environment.SetEnvironmentVariable("ERP_CONFIG_ENCRYPTION_KEY", previousKey);
            Environment.SetEnvironmentVariable("Deployment__Role", previousRole);
        }
    }

    private sealed class ProductionTestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
