using System.Net;
using System.Net.Sockets;

namespace SchoolManagement.Updates;

/// <summary>
/// Whitelist d'hôtes + règles de schéma.
/// <para>
/// HTTPS : autorisé si l'hôte est whitelisté (cible production, certificat validé par le handler TLS).
/// HTTP loopback (<c>localhost</c>, <c>127.0.0.1</c>, <c>::1</c>) : autorisé (DEV).
/// HTTP vers une IP privée LAN (RFC1918) : autorisé temporairement pour ne pas casser le DEV/LAN existant.
/// HTTP vers une IP publique : refusé, même si l'hôte est whitelisté.
/// </para>
/// Le HTTP LAN est une compatibilité transitoire. La cible production du mécanisme de mise à jour
/// est HTTPS avec certificat valide.
/// </summary>
public static class UpdateUrlGuard
{
    public static bool IsAllowed(Uri uri, IEnumerable<string> allowedHosts, bool allowHttpForLocalHosts = true)
    {
        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        var host = NormalizeHost(uri.Host);
        if (string.IsNullOrEmpty(host))
        {
            host = NormalizeHost(uri.DnsSafeHost);
        }

        var allowed = allowedHosts
            .Select(NormalizeHost)
            .Where(h => h.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var loopback = uri.IsLoopback || IsLoopbackHost(host);
        if (!allowed.Contains(host) && !(loopback && HasLoopbackAlias(allowed)))
        {
            return false;
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!allowHttpForLocalHosts)
        {
            return false;
        }

        return loopback || IsPrivateLanHost(host);
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

    private static bool HasLoopbackAlias(HashSet<string> allowed) =>
        allowed.Contains("localhost") || allowed.Contains("127.0.0.1") || allowed.Contains("::1");

    private static string NormalizeHost(string? host)
    {
        host = (host ?? string.Empty).Trim().ToLowerInvariant();
        if (host.StartsWith('[') && host.EndsWith(']'))
        {
            host = host[1..^1];
        }

        return host;
    }

    public static bool IsPrivateLanHost(string host)
    {
        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
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
}
