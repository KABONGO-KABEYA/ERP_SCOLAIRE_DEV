using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolManagement.Updates;

public sealed class UpdateApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _httpClient;
    private readonly Func<Uri, bool> _isUrlAllowed;

    public UpdateApiService(HttpClient httpClient, Func<Uri, bool> isUrlAllowed)
    {
        _httpClient = httpClient;
        _isUrlAllowed = isUrlAllowed;
    }

    public async Task<UpdateManifest?> CheckAsync(
        string endpoint,
        UpdateClientPlatform platform,
        string currentVersion,
        CancellationToken cancellationToken)
    {
        var path = endpoint.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var separator = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var url = $"{path}{separator}platform={platform.ToString().ToLowerInvariant()}&currentVersion={Uri.EscapeDataString(currentVersion)}";

        if (_httpClient.BaseAddress is not null)
        {
            var absolute = new Uri(_httpClient.BaseAddress, url);
            if (!_isUrlAllowed(absolute))
            {
                throw new InvalidOperationException("Hôte de mise à jour non autorisé.");
            }
        }

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent || !response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        // Supporte ApiResponse<T> { data: ... } ou payload direct.
        var payload = root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            ? data
            : root;

        var manifest = payload.Deserialize<UpdateManifest>(JsonOptions);
        if (manifest is null)
        {
            return null;
        }

        var download = platform == UpdateClientPlatform.Mobile
            ? manifest.MobileUrl ?? manifest.DownloadUrl
            : manifest.DesktopUrl ?? manifest.DownloadUrl;

        return new UpdateManifest
        {
            LatestVersion = manifest.LatestVersion,
            MinimumVersion = manifest.MinimumVersion,
            Mandatory = manifest.Mandatory,
            ReleaseDate = manifest.ReleaseDate,
            ReleaseNotes = manifest.ReleaseNotes,
            DesktopUrl = manifest.DesktopUrl,
            MobileUrl = manifest.MobileUrl,
            DownloadUrl = download,
            Sha256 = manifest.Sha256,
            Size = manifest.Size,
            SchemaVersion = manifest.SchemaVersion
        };
    }
}
