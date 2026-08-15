using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UnitTests.Updates;

public sealed class DownloadManagerHashTests
{
    [Fact]
    public void HashesMatch_accepts_equal_hex()
    {
        const string hash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";
        Assert.True(DownloadManager.HashesMatch(hash, hash));
        Assert.True(DownloadManager.HashesMatch(hash.ToUpperInvariant(), hash));
    }

    [Fact]
    public void HashesMatch_rejects_mismatch()
    {
        const string expected = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";
        const string actual = "0000000000000000000000000000000000000000000000000000000000000000";
        Assert.False(DownloadManager.HashesMatch(expected, actual));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HashesMatch_rejects_empty_expected(string? expected)
    {
        Assert.False(DownloadManager.HashesMatch(expected, "aabbcc"));
    }
}
