using System.Net;
using System.Net.Http;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UnitTests.Updates;

public sealed class UpdateManagerTests
{
    [Fact]
    public void Evaluate_up_to_date()
    {
        var manager = CreateManager(new CountingHandler());
        var outcome = manager.Evaluate("1.2.0", Manifest("1.2.0", "1.0.0", mandatory: false, sha: "ab"));
        Assert.Equal(UpdateAvailability.UpToDate, outcome.Availability);
    }

    [Fact]
    public void Evaluate_optional_when_newer_and_not_mandatory()
    {
        var manager = CreateManager(new CountingHandler());
        var outcome = manager.Evaluate("1.2.0", Manifest("1.3.0", "1.0.0", mandatory: false, sha: "ab"));
        Assert.Equal(UpdateAvailability.Optional, outcome.Availability);
    }

    [Fact]
    public void Evaluate_mandatory_when_flag_or_below_minimum()
    {
        var manager = CreateManager(new CountingHandler());
        var flagged = manager.Evaluate("1.2.0", Manifest("1.3.0", "1.0.0", mandatory: true, sha: "ab"));
        Assert.Equal(UpdateAvailability.Mandatory, flagged.Availability);

        var belowMin = manager.Evaluate("1.0.0", Manifest("1.2.0", "1.1.0", mandatory: false, sha: "ab"));
        Assert.Equal(UpdateAvailability.Mandatory, belowMin.Availability);
    }

    [Fact]
    public async Task DownloadAndVerify_refuses_missing_sha_without_http()
    {
        var handler = new CountingHandler();
        var manager = CreateManager(handler);
        var manifest = Manifest("1.3.0", "1.0.0", mandatory: false, sha: null);
        manifest = new UpdateManifest
        {
            LatestVersion = manifest.LatestVersion,
            MinimumVersion = manifest.MinimumVersion,
            Mandatory = manifest.Mandatory,
            DownloadUrl = "https://localhost/update.exe",
            Sha256 = null
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.DownloadAndVerifyAsync(manifest, progress: null, CancellationToken.None));

        Assert.Contains("SHA256", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.SendCount);
    }

    private static UpdateManifest Manifest(string latest, string minimum, bool mandatory, string? sha) =>
        new()
        {
            LatestVersion = latest,
            MinimumVersion = minimum,
            Mandatory = mandatory,
            Sha256 = sha,
            DownloadUrl = "https://localhost/update.exe"
        };

    private static UpdateManager CreateManager(HttpMessageHandler handler)
    {
        var dir = Path.Combine(Path.GetTempPath(), "erp-upd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        return new UpdateManager(
            new UpdateApiService(http, _ => true),
            new DownloadManager(http, _ => true),
            new UpdateSettingsStore(dir),
            new UpdateHistoryStore(dir),
            Path.Combine(dir, "packages"),
            UpdateClientPlatform.Desktop);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int SendCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref SendCount);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("should-not-download")
            });
        }
    }
}
