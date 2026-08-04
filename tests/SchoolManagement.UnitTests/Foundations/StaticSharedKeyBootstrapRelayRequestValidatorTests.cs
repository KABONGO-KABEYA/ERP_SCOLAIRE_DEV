using FluentAssertions;
using SchoolManagement.API.Services.BootstrapRelay;
using SchoolManagement.Application.ParentActivation.BootstrapRelay;
using Microsoft.Extensions.Options;
using Xunit;

namespace SchoolManagement.UnitTests.Foundations;

[Trait("Category", "Foundations")]
public sealed class StaticSharedKeyBootstrapRelayRequestValidatorTests
{
    [Fact]
    public async Task ValidateAsync_accepts_matching_shared_key()
    {
        var validator = CreateValidator("secret-relay");
        var headers = new Dictionary<string, string?>
        {
            [BootstrapRelayAuthConstants.LegacySharedKeyHeaderName] = "secret-relay"
        };

        var result = await validator.ValidateAsync(headers);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_rejects_wrong_key()
    {
        var validator = CreateValidator("secret-relay");
        var headers = new Dictionary<string, string?>
        {
            [BootstrapRelayAuthConstants.LegacySharedKeyHeaderName] = "other"
        };

        var result = await validator.ValidateAsync(headers);
        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().Be(401);
    }

    [Fact]
    public async Task ValidateAsync_fails_when_not_configured()
    {
        var validator = CreateValidator("");
        var headers = new Dictionary<string, string?>
        {
            [BootstrapRelayAuthConstants.LegacySharedKeyHeaderName] = "x"
        };

        var result = await validator.ValidateAsync(headers);
        result.IsSuccess.Should().BeFalse();
        result.HttpStatusCode.Should().Be(503);
    }

    private static StaticSharedKeyBootstrapRelayRequestValidator CreateValidator(string key)
    {
        return new StaticSharedKeyBootstrapRelayRequestValidator(
            Options.Create(new BootstrapRelaySchoolOptions { BootstrapRelayKey = key }));
    }
}
