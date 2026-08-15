using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UnitTests.Updates;

public sealed class AppVersionInfoTests
{
    [Fact]
    public void Assembly_wins_over_divergent_version_json()
    {
        var dir = CreateJsonDir("1.1.0");
        try
        {
            var resolved = AppVersionInfo.Resolve("1.2.0", fileVersion: "1.0.0.0", versionJsonDirectory: dir);
            Assert.Equal("1.2.0", resolved);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Metadata_is_stripped_prerelease_is_kept()
    {
        Assert.Equal("1.2.0", AppVersionInfo.Resolve("1.2.0+gitsha"));
        Assert.Equal("1.2.0-beta", AppVersionInfo.Resolve("1.2.0-beta+gitsha"));
    }

    [Fact]
    public void Version_json_is_used_only_when_assembly_is_missing()
    {
        var dir = CreateJsonDir("1.4.0");
        try
        {
            var resolved = AppVersionInfo.Resolve(null, fileVersion: null, versionJsonDirectory: dir);
            Assert.Equal("1.4.0", resolved);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Zero_assembly_falls_back_to_version_json()
    {
        var dir = CreateJsonDir("1.5.0");
        try
        {
            var resolved = AppVersionInfo.Resolve("0.0.0", fileVersion: "0.0.0.0", versionJsonDirectory: dir);
            Assert.Equal("1.5.0", resolved);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ApplyToSettings_overwrites_stale_1_0_0()
    {
        var settings = new UpdateSettings { CurrentVersion = "1.0.0" };
        AppVersionInfo.ApplyToSettings(settings, "1.2.0");
        Assert.Equal("1.2.0", settings.CurrentVersion);
    }

    [Fact]
    public void Missing_sources_yield_0_0_0()
    {
        Assert.Equal("0.0.0", AppVersionInfo.Resolve(null, null, versionJsonDirectory: null));
    }

    private static string CreateJsonDir(string version)
    {
        var dir = Path.Combine(Path.GetTempPath(), "erp-appver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "version.json"), $$"""{"version":"{{version}}"}""");
        return dir;
    }
}
