using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.ServerIdentity;
using Xunit;

namespace SchoolManagement.UnitTests.Foundations;

[Trait("Category", "Foundations")]
public sealed class LocalDiscoveryHealthFoundationTests
{
    [Fact]
    public void Get_Before_Setup_Exposes_Identity_With_Null_SchoolId()
    {
        var provider = Substitute.For<IServerIdentityProvider>();
        provider.Current.Returns(CreateSnapshot(schoolId: null));

        var result = new LocalDiscoveryHealthController(provider).Get() as OkObjectResult;
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result!.Value);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("status").GetString().Should().Be("ok");
        doc.RootElement.GetProperty("identity").GetProperty("schoolId").ValueKind.Should().Be(JsonValueKind.Null);
        doc.RootElement.GetProperty("identity").GetProperty("serverInstanceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Get_After_Setup_Exposes_SchoolId_And_KeyVersion()
    {
        var schoolId = Guid.NewGuid();
        var provider = Substitute.For<IServerIdentityProvider>();
        provider.Current.Returns(CreateSnapshot(schoolId: schoolId, keyVersion: 1));

        var result = new LocalDiscoveryHealthController(provider).Get() as OkObjectResult;
        result.Should().NotBeNull();
        var json = JsonSerializer.Serialize(result!.Value);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("identity").GetProperty("schoolId").GetString()
            .Should().Be(schoolId.ToString("D"));
        doc.RootElement.GetProperty("identity").GetProperty("keyVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public void Get_Keeps_Legacy_Client_Fields()
    {
        var provider = Substitute.For<IServerIdentityProvider>();
        provider.Current.Returns(CreateSnapshot(schoolId: null));

        var result = new LocalDiscoveryHealthController(provider).Get() as OkObjectResult;
        var json = JsonSerializer.Serialize(result!.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("server").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("school").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("version").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("time").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Get_Exposes_V2_Fields()
    {
        var provider = Substitute.For<IServerIdentityProvider>();
        provider.Current.Returns(CreateSnapshot(schoolId: null));

        var result = new LocalDiscoveryHealthController(provider).Get() as OkObjectResult;
        var json = JsonSerializer.Serialize(result!.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("protocolVersion").GetInt32().Should().Be(ConnectionProtocolConstants.ProtocolVersion);
        root.GetProperty("apiVersion").GetString().Should().Be(ConnectionProtocolConstants.ApiVersion);
        root.TryGetProperty("serverSignature", out var sig).Should().BeTrue();
        sig.ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("identity").GetProperty("publicKeyFingerprint").GetString()
            .Should().StartWith("sha256:");
    }

    private static ServerIdentitySnapshot CreateSnapshot(Guid? schoolId, int keyVersion = 1) =>
        new(
            Guid.NewGuid(),
            schoolId,
            "École Test",
            null,
            "sha256:test-fingerprint",
            keyVersion,
            "1.0.0",
            ConnectionProtocolConstants.ApiVersion,
            ConnectionProtocolConstants.ProtocolVersion,
            "local");
}
