using System.Net;

namespace SchoolManagement.LocalServerDiscovery;

/// <summary>
/// Réécrit les requêtes HttpClient vers l'URL découverte dynamiquement.
/// </summary>
public sealed class DiscoveryBaseAddressHandler : DelegatingHandler
{
    private readonly ILocalServerDiscovery _discovery;
    private readonly string _fallbackBaseUrl;

    public DiscoveryBaseAddressHandler(ILocalServerDiscovery discovery, string? fallbackBaseUrl = null)
    {
        _discovery = discovery;
        _fallbackBaseUrl = HealthProbe.NormalizeBaseUrl(
            string.IsNullOrWhiteSpace(fallbackBaseUrl)
                ? DiscoveryConstants.PlaceholderBaseUrl
                : fallbackBaseUrl);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var baseUrl = ResolveBaseUrl();
        if (request.RequestUri is null)
        {
            return base.SendAsync(request, cancellationToken);
        }

        if (!request.RequestUri.IsAbsoluteUri)
        {
            request.RequestUri = new Uri(new Uri(baseUrl), request.RequestUri);
        }
        else if (IsPlaceholderHost(request.RequestUri) || IsLoopbackPlaceholder(request.RequestUri))
        {
            var path = request.RequestUri.PathAndQuery;
            if (string.IsNullOrEmpty(path) || path == "/")
            {
                path = string.Empty;
            }
            else if (path.StartsWith('/'))
            {
                path = path[1..];
            }

            request.RequestUri = new Uri(new Uri(baseUrl), path);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_discovery.Current.BaseUrl))
        {
            return HealthProbe.NormalizeBaseUrl(_discovery.Current.BaseUrl);
        }

        return _fallbackBaseUrl;
    }

    private static bool IsPlaceholderHost(Uri uri) =>
        uri.Host.Equals("discovery.local", StringComparison.OrdinalIgnoreCase);

    private static bool IsLoopbackPlaceholder(Uri uri) =>
        uri.IsLoopback
        && (uri.Port == DiscoveryConstants.ApiPort
            || uri.Port == 5041
            || uri.Port == 80
            || uri.Port == -1);
}
