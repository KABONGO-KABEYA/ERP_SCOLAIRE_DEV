using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent;

public sealed class AgentTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public int ExpiresIn { get; set; }

    public Guid SchoolId { get; set; }

    public Guid ClientId { get; set; }
}

public sealed class AgentArtifactDto
{
    public Guid ArtifactId { get; set; }

    /// <summary>Présent seulement si le catalogue l'expose ; sinon le ReleaseId parent s'applique.</summary>
    public Guid? ReleaseId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public long? Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;
}

public sealed class AgentReleaseCheckDto
{
    public Guid ReleaseId { get; set; }

    public string Version { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int ProtocolVersion { get; set; }

    public int FromSchemaVersion { get; set; }

    public int SchemaVersion { get; set; }

    public AgentArtifactDto? Api { get; set; }

    public AgentArtifactDto? Migration { get; set; }

    /// <summary>Ancien format check (un seul artifact Desktop).</summary>
    public AgentArtifactDto? Artifact { get; set; }
}

public sealed class AgentCheckResult
{
    public HttpStatusCode StatusCode { get; init; }

    public AgentReleaseCheckDto? Body { get; init; }
}

public interface IBootstrapAgentClient
{
    Task<AgentTokenResponse> GetTokenAsync(Guid clientId, string clientSecret, CancellationToken cancellationToken);

    Task<AgentCheckResult> CheckReleaseAsync(string accessToken, string channel, CancellationToken cancellationToken);
}

public sealed class BootstrapAgentClient : IBootstrapAgentClient
{
    private readonly HttpClient _http;
    private readonly ILogger<BootstrapAgentClient> _log;

    public BootstrapAgentClient(HttpClient http, ILogger<BootstrapAgentClient> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<AgentTokenResponse> GetTokenAsync(
        Guid clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        _log.LogInformation("Demande JWT agent clientId={ClientId}", clientId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/agent/token")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { clientId, clientSecret }, JsonOpts.Http),
                Encoding.UTF8,
                "application/json"),
        };
        using var response = await _http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureNoSecretInPayload(raw, clientSecret);
        if (!response.IsSuccessStatusCode)
        {
            throw new AgentException($"Token Bootstrap HTTP {(int)response.StatusCode}.");
        }

        var token = JsonSerializer.Deserialize<AgentTokenResponse>(raw, JsonOpts.Http)
                    ?? throw new AgentException("Réponse token vide.");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new AgentException("JWT absent.");
        }

        return token;
    }

    public async Task<AgentCheckResult> CheckReleaseAsync(
        string accessToken,
        string channel,
        CancellationToken cancellationToken)
    {
        var path = $"api/v1/agent/releases/check?channel={Uri.EscapeDataString(channel)}&artifactType=Api";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new AgentCheckResult { StatusCode = response.StatusCode };
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AgentException($"Check release HTTP {(int)response.StatusCode}.");
        }

        var body = JsonSerializer.Deserialize<AgentReleaseCheckDto>(raw, JsonOpts.Http);
        return new AgentCheckResult { StatusCode = response.StatusCode, Body = body };
    }

    private static void EnsureNoSecretInPayload(string raw, string secret)
    {
        if (!string.IsNullOrEmpty(secret) && raw.Contains(secret, StringComparison.Ordinal))
        {
            throw new AgentException("Le secret ne doit pas être renvoyé par Bootstrap.");
        }
    }
}

public static class BootstrapUrlPolicy
{
    public static void EnsureAllowed(string bootstrapBaseUrl, IReadOnlyList<string> allowedHosts)
    {
        if (!Uri.TryCreate(bootstrapBaseUrl, UriKind.Absolute, out var uri))
        {
            throw new AgentException("BootstrapBaseUrl invalide.");
        }

        var hosts = allowedHosts.Count > 0
            ? allowedHosts
            : new[] { uri.Host };
        if (!UpdateUrlGuard.IsAllowed(uri, hosts, allowHttpForLocalHosts: true))
        {
            throw new AgentException("URL Bootstrap non autorisée (HTTPS requis hors loopback).");
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !UpdateUrlGuard.IsLoopbackHost(uri.Host) && !uri.IsLoopback)
        {
            throw new AgentException("HTTPS obligatoire vers Bootstrap (hors loopback de test).");
        }
    }
}
