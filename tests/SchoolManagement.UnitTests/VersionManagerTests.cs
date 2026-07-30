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

    [Fact]
    public void IsNewer_detects_patch_bump()
    {
        Assert.True(VersionManager.IsNewer("1.0.10", "1.0.9"));
        Assert.False(VersionManager.IsNewer("1.0.9", "1.0.10"));
    }
}
