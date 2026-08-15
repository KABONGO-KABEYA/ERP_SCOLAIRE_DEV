using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UnitTests.Updates;

public sealed class UpdateUrlGuardTests
{
    private static readonly string[] Allowed =
    [
        "localhost",
        "127.0.0.1",
        "::1",
        "169.58.93.203",
        "192.168.1.10",
        "10.0.0.5",
        "172.16.0.8"
    ];

    [Fact]
    public void Https_whitelisted_host_is_allowed()
    {
        Assert.True(UpdateUrlGuard.IsAllowed(new Uri("https://169.58.93.203/pkg.exe"), Allowed));
        Assert.True(UpdateUrlGuard.IsAllowed(new Uri("https://localhost/pkg.exe"), Allowed));
    }

    [Fact]
    public void Host_not_in_whitelist_is_refused()
    {
        Assert.False(UpdateUrlGuard.IsAllowed(new Uri("https://evil.com/pkg.exe"), Allowed));
        Assert.False(UpdateUrlGuard.IsAllowed(new Uri("http://evil.com/pkg.exe"), Allowed));
    }

    [Theory]
    [InlineData("http://localhost:5096/api/v1/update/check")]
    [InlineData("http://127.0.0.1/pkg.exe")]
    [InlineData("http://[::1]/pkg.exe")]
    public void Http_loopback_is_allowed(string url)
    {
        Assert.True(UpdateUrlGuard.IsAllowed(new Uri(url), Allowed));
    }

    [Fact]
    public void Http_private_lan_is_allowed_temporarily()
    {
        Assert.True(UpdateUrlGuard.IsAllowed(new Uri("http://192.168.1.10/pkg.exe"), Allowed));
        Assert.True(UpdateUrlGuard.IsAllowed(new Uri("http://10.0.0.5/pkg.exe"), Allowed));
        Assert.True(UpdateUrlGuard.IsAllowed(new Uri("http://172.16.0.8/pkg.exe"), Allowed));
    }

    [Fact]
    public void Http_public_ip_is_refused()
    {
        Assert.False(UpdateUrlGuard.IsAllowed(new Uri("http://169.58.93.203/pkg.exe"), Allowed));
        Assert.False(UpdateUrlGuard.IsAllowed(new Uri("http://8.8.8.8/pkg.exe"), ["8.8.8.8"]));
    }
}
