namespace SchoolManagement.Updates;

public static class UpdateUrlGuard
{
    public static bool IsAllowed(Uri uri, IEnumerable<string> allowedHosts, bool allowHttpForLocalHosts = true)
    {
        if (!uri.IsAbsoluteUri)
        {
            return false;
        }

        var host = uri.Host.Trim().ToLowerInvariant();
        var allowed = allowedHosts
            .Select(h => h.Trim().ToLowerInvariant())
            .Where(h => h.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!allowed.Contains(host))
        {
            return false;
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowHttpForLocalHosts)
        {
            return false;
        }

        // Coolify / LAN : HTTP autorisé uniquement pour hôtes explicitement whitelistés.
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }
}
