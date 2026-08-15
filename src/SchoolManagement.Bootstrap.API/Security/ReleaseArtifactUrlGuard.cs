using System.Net;
using System.Net.Sockets;

namespace SchoolManagement.Bootstrap.API.Security;

/// <summary>
/// Validation d'URL d'artifact du catalogue Bootstrap (indépendante de SchoolManagement.Updates).
/// HTTPS toujours autorisé. HTTP uniquement loopback / LAN privé, et seulement pour le channel DEV.
/// HTTP vers une IP publique : refusé. URL relative / schéma inconnu : refusé.
/// </summary>
public static class ReleaseArtifactUrlGuard
{
    public static bool TryValidate(string? rawUrl, string channel, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            error = "L'URL de l'artifact est obligatoire.";
            return false;
        }

        var trimmed = rawUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || !uri.IsAbsoluteUri)
        {
            error = "L'URL de l'artifact doit être absolue.";
            return false;
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            error = "Schéma d'URL non autorisé. Utilisez HTTPS (HTTP LAN uniquement en DEV).";
            return false;
        }

        var normalizedChannel = channel.Trim().ToUpperInvariant();
        if (!string.Equals(normalizedChannel, "DEV", StringComparison.Ordinal))
        {
            error = "HTTP public interdit. Le channel PROD exige une URL HTTPS.";
            return false;
        }

        var host = NormalizeHost(uri.Host);
        if (string.IsNullOrEmpty(host))
        {
            host = NormalizeHost(uri.DnsSafeHost);
        }

        if (uri.IsLoopback || IsLoopbackHost(host) || IsPrivateLanHost(host))
        {
            return true;
        }

        error = "HTTP vers une IP publique est interdit.";
        return false;
    }

    public static bool IsLoopbackHost(string host)
    {
        host = NormalizeHost(host);
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    public static bool IsPrivateLanHost(string host)
    {
        if (!IPAddress.TryParse(host, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (bytes[0] == 10)
        {
            return true;
        }

        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        return bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
    }

    private static string NormalizeHost(string? host)
    {
        host = (host ?? string.Empty).Trim().ToLowerInvariant();
        if (host.StartsWith('[') && host.EndsWith(']'))
        {
            host = host[1..^1];
        }

        return host;
    }
}
