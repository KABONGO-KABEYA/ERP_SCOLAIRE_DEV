using System.Net;
using System.Net.Http;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UnitTests.Updates;

public sealed class UpdateApiServiceTests
{
    [Fact]
    public async Task Check_204_returns_null()
    {
        var api = CreateApi(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var manifest = await api.CheckAsync("/api/v1/update/check", UpdateClientPlatform.Desktop, "1.0.0", CancellationToken.None);
        Assert.Null(manifest);
    }

    [Fact]
    public async Task Check_200_unwraps_data_payload()
    {
        const string json = """
            {
              "success": true,
              "data": {
                "latestVersion": "1.3.0",
                "minimumVersion": "1.0.0",
                "mandatory": false,
                "downloadUrl": "https://localhost/update.exe",
                "desktopUrl": "https://localhost/update.exe",
                "sha256": "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899",
                "size": 12,
                "releaseNotes": ["fix"]
              }
            }
            """;

        var api = CreateApi(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        var manifest = await api.CheckAsync("/api/v1/update/check", UpdateClientPlatform.Desktop, "1.0.0", CancellationToken.None);
        Assert.NotNull(manifest);
        Assert.Equal("1.3.0", manifest!.LatestVersion);
        Assert.Equal("1.0.0", manifest.MinimumVersion);
        Assert.Equal("https://localhost/update.exe", manifest.DownloadUrl);
        Assert.Equal("aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899", manifest.Sha256);
    }

    private static UpdateApiService CreateApi(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var http = new HttpClient(new StubHandler(responder))
        {
            BaseAddress = new Uri("https://localhost/")
        };
        return new UpdateApiService(http, _ => true);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
