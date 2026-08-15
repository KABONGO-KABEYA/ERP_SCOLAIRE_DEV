using FluentAssertions;
using SchoolManagement.Bootstrap.API.Security;
using SchoolManagement.Bootstrap.API.Services;
using Xunit;

namespace SchoolManagement.Bootstrap.UnitTests;

public sealed class ReleaseArtifactValidationTests
{
    [Fact]
    public void Sha256_64_hex_is_valid()
    {
        ReleaseSemVer.TryNormalizeSha256(
                "AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899",
                out var hex,
                out var error)
            .Should().BeTrue();
        hex.Should().Be("aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899");
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sha256_empty_is_invalid(string? sha)
    {
        ReleaseSemVer.TryNormalizeSha256(sha, out _, out var error).Should().BeFalse();
        error.Should().Contain("SHA256");
    }

    [Fact]
    public void Sha256_too_short_is_invalid()
    {
        ReleaseSemVer.TryNormalizeSha256("aa", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Sha256_non_hex_is_invalid()
    {
        var sixtyFour = new string('g', 64);
        ReleaseSemVer.TryNormalizeSha256(sixtyFour, out _, out var error).Should().BeFalse();
        error.Should().Contain("invalides");
    }

    [Fact]
    public void Https_url_is_allowed()
    {
        ReleaseArtifactUrlGuard.TryValidate("https://cdn.example/pkg.exe", "PROD", out var error)
            .Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void Http_public_is_rejected()
    {
        ReleaseArtifactUrlGuard.TryValidate("http://169.58.93.203/pkg.exe", "PROD", out var error)
            .Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void Relative_url_is_rejected()
    {
        ReleaseArtifactUrlGuard.TryValidate("/release/pkg.exe", "PROD", out _).Should().BeFalse();
        ReleaseArtifactUrlGuard.TryValidate("release/pkg.exe", "DEV", out _).Should().BeFalse();
    }

    [Fact]
    public void Http_loopback_allowed_only_in_dev()
    {
        ReleaseArtifactUrlGuard.TryValidate("http://127.0.0.1/pkg.exe", "DEV", out _).Should().BeTrue();
        ReleaseArtifactUrlGuard.TryValidate("http://127.0.0.1/pkg.exe", "PROD", out _).Should().BeFalse();
    }

    [Fact]
    public void Unknown_scheme_is_rejected()
    {
        ReleaseArtifactUrlGuard.TryValidate("ftp://example.com/pkg.exe", "PROD", out _).Should().BeFalse();
    }
}
