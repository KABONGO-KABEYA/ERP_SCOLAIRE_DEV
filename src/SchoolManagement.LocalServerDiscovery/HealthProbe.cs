using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.LocalServerDiscovery;

public sealed class HealthProbe : IHealthProbe
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HealthProbe> _logger;

    public HealthProbe(IHttpClientFactory httpClientFactory, ILogger<HealthProbe> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HealthInfo?> ProbeAsync(
        string baseUrl,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var normalized = NormalizeBaseUrl(baseUrl);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            var client = _httpClientFactory.CreateClient("LocalServerDiscovery");
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(normalized), DiscoveryConstants.HealthPath.TrimStart('/')));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            var dto = await JsonSerializer.DeserializeAsync<HealthDto>(stream, JsonOptions, cts.Token)
                .ConfigureAwait(false);

            if (dto is null || string.IsNullOrWhiteSpace(dto.Status))
            {
                // Accepte aussi l'ancien format encapsulé / plain 200.
                return new HealthInfo(
                    "ok",
                    "local",
                    "École",
                    "1.0.0",
                    DateTimeOffset.UtcNow);
            }

            var status = dto.Status.Trim();
            if (!status.Equals("ok", StringComparison.OrdinalIgnoreCase)
                && !status.Equals("healthy", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new HealthInfo(
                "ok",
                string.IsNullOrWhiteSpace(dto.Server) ? "local" : dto.Server!,
                string.IsNullOrWhiteSpace(dto.School) ? "École" : dto.School!,
                string.IsNullOrWhiteSpace(dto.Version) ? "1.0.0" : dto.Version!,
                dto.Time ?? DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("[Discovery] Health timeout {Url}", normalized);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Discovery] Health échec {Url}", normalized);
            return null;
        }
    }

    public static string NormalizeBaseUrl(string url)
    {
        var cleaned = url.Trim();
        while (cleaned.EndsWith('/'))
        {
            cleaned = cleaned[..^1];
        }

        return cleaned + "/";
    }

    public static string ToHostPortBaseUrl(IPAddress ip, int port) =>
        $"http://{ip}:{port}/";

    private sealed class HealthDto
    {
        public string? Status { get; set; }
        public string? Server { get; set; }
        public string? School { get; set; }
        public string? Version { get; set; }
        public DateTimeOffset? Time { get; set; }
    }
}
