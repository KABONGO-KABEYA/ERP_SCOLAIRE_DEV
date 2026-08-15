using System.Text.Json;
using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent;

public sealed class HealthProbeResult
{
    public bool Ok { get; init; }

    public string? Error { get; init; }
}

public interface IApiHealthProbe
{
    Task<HealthProbeResult> CheckOnceAsync(
        string url,
        string targetRelease,
        int targetProtocol,
        int targetSchema,
        Guid expectedInstanceId,
        CancellationToken cancellationToken);
}

public sealed class ApiHealthProbe : IApiHealthProbe
{
    private readonly HttpClient _http;

    public ApiHealthProbe(HttpClient http) => _http = http;

    public async Task<HealthProbeResult> CheckOnceAsync(
        string url,
        string targetRelease,
        int targetProtocol,
        int targetSchema,
        Guid expectedInstanceId,
        CancellationToken cancellationToken)
    {
        try
        {
        using var response = await _http.GetAsync(url, cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            return new HealthProbeResult { Error = $"HTTP {(int)response.StatusCode}" };
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!string.Equals(root.GetProperty("status").GetString(), "ok", StringComparison.OrdinalIgnoreCase))
        {
            return new HealthProbeResult { Error = "status ≠ ok" };
        }

        if (root.GetProperty("protocolVersion").GetInt32() != targetProtocol)
        {
            return new HealthProbeResult { Error = "mauvais ProtocolVersion" };
        }

        var version = VersionManager.Parse(root.GetProperty("version").GetString()).ToNormalizedString();
        var expected = VersionManager.Parse(targetRelease).ToNormalizedString();
        if (!string.Equals(version, expected, StringComparison.OrdinalIgnoreCase))
        {
            return new HealthProbeResult { Error = "mauvaise version" };
        }

        if (root.GetProperty("schemaVersion").GetInt32() != targetSchema)
        {
            return new HealthProbeResult { Error = "mauvais SchemaVersion" };
        }

        var instance = root.GetProperty("identity").GetProperty("serverInstanceId").GetString();
        if (!Guid.TryParse(instance, out var id) || id != expectedInstanceId)
        {
            return new HealthProbeResult { Error = "mauvais ServerInstanceId" };
        }

        return new HealthProbeResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new HealthProbeResult { Error = ex.Message };
        }
    }
}
