using FluentAssertions;
using Xunit;

namespace SchoolManagement.Setup.UnitTests;

public sealed class ApiStartupWaitTests
{
    [Fact]
    public void ResolveTimeout_defaults_to_20_minutes()
    {
        ApiStartupWait.ResolveTimeout(null).Should().Be(TimeSpan.FromSeconds(1200));
        ApiStartupWait.ResolveTimeout("").Should().Be(TimeSpan.FromSeconds(1200));
        ApiStartupWait.ResolveTimeout("abc").Should().Be(TimeSpan.FromSeconds(1200));
    }

    [Fact]
    public void ResolveTimeout_parses_and_clamps_env_value()
    {
        ApiStartupWait.ResolveTimeout("120").Should().Be(TimeSpan.FromSeconds(120));
        ApiStartupWait.ResolveTimeout("10").Should().Be(TimeSpan.FromSeconds(ApiStartupWait.MinTimeoutSeconds));
        ApiStartupWait.ResolveTimeout("99999").Should().Be(TimeSpan.FromSeconds(ApiStartupWait.MaxTimeoutSeconds));
    }

    [Fact]
    public async Task WaitAsync_succeeds_after_slow_initialization_then_confirmed_health()
    {
        var attempts = 0;
        var logs = new List<string>();
        var result = await ApiStartupWait.WaitAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(attempts >= 4);
            },
            () => true,
            logs.Add,
            timeout: TimeSpan.FromSeconds(2),
            pollInterval: TimeSpan.FromMilliseconds(20),
            progressInterval: TimeSpan.FromMilliseconds(40),
            confirmCount: 2);

        result.Healthy.Should().BeTrue();
        result.Reason.Should().Be("OK");
        attempts.Should().BeGreaterThanOrEqualTo(5);
        logs.Should().Contain(l => l.Contains("initialise encore", StringComparison.OrdinalIgnoreCase)
            || l.Contains("Attente health", StringComparison.OrdinalIgnoreCase));
        logs.Should().Contain(l => l.Contains("API prête", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WaitAsync_fails_immediately_when_process_exits()
    {
        var result = await ApiStartupWait.WaitAsync(
            _ => Task.FromResult(false),
            () => false,
            _ => { },
            timeout: TimeSpan.FromSeconds(5),
            pollInterval: TimeSpan.FromMilliseconds(10),
            confirmCount: 2);

        result.Healthy.Should().BeFalse();
        result.Reason.Should().Contain("arrêtée");
        result.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WaitAsync_times_out_while_process_still_alive()
    {
        var result = await ApiStartupWait.WaitAsync(
            _ => Task.FromResult(false),
            () => true,
            _ => { },
            timeout: TimeSpan.FromMilliseconds(120),
            pollInterval: TimeSpan.FromMilliseconds(20),
            progressInterval: TimeSpan.FromMilliseconds(30),
            confirmCount: 2);

        result.Healthy.Should().BeFalse();
        result.Reason.Should().Contain("Timeout");
        result.Reason.Should().Contain("encore en vie");
    }

    [Fact]
    public async Task WaitAsync_does_not_succeed_on_a_single_unconfirmed_health()
    {
        var attempts = 0;
        var result = await ApiStartupWait.WaitAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(attempts == 1);
            },
            () => true,
            _ => { },
            timeout: TimeSpan.FromMilliseconds(150),
            pollInterval: TimeSpan.FromMilliseconds(20),
            confirmCount: 2);

        result.Healthy.Should().BeFalse();
        attempts.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Database_Migrate_is_not_part_of_startup_wait()
    {
        var source = File.ReadAllText(
            Path.Combine(FindRepoRoot(), "src", "SchoolManagement.Setup", "ApiStartupWait.cs"));
        source.Should().NotContain("Database.Migrate");
        source.Should().NotContain("ApplyPendingEfMigrationsAsync");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SchoolManagement.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Racine du dépôt introuvable.");
    }
}
