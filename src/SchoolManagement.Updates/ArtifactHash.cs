using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SchoolManagement.Updates;

public static class ArtifactHash
{
    private static readonly Regex Hex64 = new("^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Normalize(string? sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256))
        {
            throw new MigrationException("SHA256 requis.");
        }

        var normalized = sha256.Trim().Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        if (!Hex64.IsMatch(normalized))
        {
            throw new MigrationException("SHA256 invalide (64 hex attendus).");
        }

        return normalized;
    }

    public static bool EqualsHex(string? expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        return string.Equals(
            expected.Trim().Replace("-", "", StringComparison.Ordinal),
            actual.Trim().Replace("-", "", StringComparison.Ordinal),
            StringComparison.OrdinalIgnoreCase);
    }
}
