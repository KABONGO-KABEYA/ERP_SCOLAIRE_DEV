using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UnitTests;

public sealed class VersionManagerTests
{
    [Theory]
    [InlineData("1.0.9", "1.0.10", -1)]
    [InlineData("1.0.10", "1.1.0", -1)]
    [InlineData("1.1.0", "2.0.0", -1)]
    [InlineData("2.0.0", "1.9.9", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    public void Compare_semantic_versions(string left, string right, int expectedSign)
    {
        var cmp = VersionManager.Compare(left, right);
        Assert.Equal(Math.Sign(expectedSign), Math.Sign(cmp));
    }

    [Theory]
    [InlineData("1.2.0+gitsha", "1.2.0")]
    [InlineData("1.2.0-beta", "1.2.0-beta")]
    [InlineData("1.2.0-beta.1", "1.2.0-beta.1")]
    [InlineData("1.2.0-rc.1", "1.2.0-rc.1")]
    [InlineData("1.2.0-beta.1+gitsha", "1.2.0-beta.1")]
    public void Normalize_strips_metadata_keeps_prerelease(string input, string expected)
    {
        Assert.Equal(expected, VersionManager.Normalize(input));
    }

    [Theory]
    [InlineData("1.2.0+gitsha", "1.2.0", 0)]
    [InlineData("1.2.0-beta", "1.2.0", -1)]
    [InlineData("1.2.0", "1.2.0-beta", 1)]
    [InlineData("1.2.0-beta.1", "1.2.0-rc.1", -1)]
    [InlineData("1.2.0-beta+build", "1.2.0-beta", 0)]
    [InlineData("1.2.0-beta.2", "1.2.0-beta.11", -1)]
    public void Compare_prerelease_and_metadata(string left, string right, int expectedSign)
    {
        var cmp = VersionManager.Compare(left, right);
        Assert.Equal(Math.Sign(expectedSign), Math.Sign(cmp));
    }

    [Fact]
    public void IsNewer_detects_patch_bump()
    {
        Assert.True(VersionManager.IsNewer("1.0.10", "1.0.9"));
        Assert.False(VersionManager.IsNewer("1.0.9", "1.0.10"));
    }

    [Fact]
    public void IsNewer_release_is_newer_than_prerelease()
    {
        Assert.True(VersionManager.IsNewer("1.2.0", "1.2.0-beta"));
        Assert.False(VersionManager.IsNewer("1.2.0-beta", "1.2.0"));
    }

    [Fact]
    public void IsOlderThan_prerelease_is_below_release_minimum()
    {
        Assert.True(VersionManager.IsOlderThan("1.2.0-beta", "1.2.0"));
        Assert.False(VersionManager.IsOlderThan("1.2.0", "1.2.0-beta"));
    }
}
