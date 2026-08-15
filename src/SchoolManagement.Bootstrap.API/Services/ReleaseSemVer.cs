using System.Globalization;
using System.Text.RegularExpressions;

namespace SchoolManagement.Bootstrap.API.Services;

/// <summary>
/// SemVer 2.0 local au catalogue Bootstrap (pas de dépendance vers SchoolManagement.Updates).
/// <c>+metadata</c> ignorée ; <c>-prerelease</c> conservée.
/// </summary>
public static class ReleaseSemVer
{
    private static readonly Regex HexSha = new("^[0-9a-fA-F]{64}$", RegexOptions.Compiled);

    public readonly record struct ProductVersion(
        int Major,
        int Minor,
        int Patch,
        int Build,
        string? Prerelease)
    {
        public bool HasPrerelease => !string.IsNullOrEmpty(Prerelease);

        public string ToNormalizedString()
        {
            var core = Build > 0
                ? $"{Major}.{Minor}.{Patch}.{Build}"
                : $"{Major}.{Minor}.{Patch}";
            return HasPrerelease ? $"{core}-{Prerelease}" : core;
        }
    }

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = "0.0.0";
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var cleaned = value.Trim();
        var plus = cleaned.IndexOf('+');
        if (plus >= 0)
        {
            cleaned = cleaned[..plus];
        }

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return false;
        }

        string? prerelease = null;
        var dash = cleaned.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = cleaned[(dash + 1)..].Trim();
            if (prerelease.Length == 0)
            {
                prerelease = null;
            }

            cleaned = cleaned[..dash];
        }

        var parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        var numbers = new int[4];
        for (var i = 0; i < Math.Min(parts.Length, 4); i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i])
                || numbers[i] < 0)
            {
                return false;
            }
        }

        normalized = new ProductVersion(numbers[0], numbers[1], numbers[2], numbers[3], prerelease)
            .ToNormalizedString();
        return true;
    }

    public static ProductVersion Parse(string value)
    {
        if (!TryNormalize(value, out var normalized))
        {
            return new ProductVersion(0, 0, 0, 0, null);
        }

        string? pre = null;
        var dash = normalized.IndexOf('-');
        var core = normalized;
        if (dash >= 0)
        {
            pre = normalized[(dash + 1)..];
            core = normalized[..dash];
        }

        var parts = core.Split('.');
        var n = new int[4];
        for (var i = 0; i < Math.Min(parts.Length, 4); i++)
        {
            int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out n[i]);
        }

        return new ProductVersion(n[0], n[1], n[2], n[3], pre);
    }

    public static int Compare(string? left, string? right) =>
        Compare(Parse(left ?? "0.0.0"), Parse(right ?? "0.0.0"));

    public static int Compare(ProductVersion left, ProductVersion right)
    {
        var core = left.Major.CompareTo(right.Major);
        if (core != 0)
        {
            return core;
        }

        core = left.Minor.CompareTo(right.Minor);
        if (core != 0)
        {
            return core;
        }

        core = left.Patch.CompareTo(right.Patch);
        if (core != 0)
        {
            return core;
        }

        core = left.Build.CompareTo(right.Build);
        if (core != 0)
        {
            return core;
        }

        return ComparePrerelease(left.Prerelease, right.Prerelease);
    }

    public static bool TryNormalizeSha256(string? raw, out string hex, out string? error)
    {
        hex = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "SHA256 manquant.";
            return false;
        }

        var cleaned = raw.Trim().Replace("-", "", StringComparison.Ordinal);
        if (cleaned.Length != 64)
        {
            error = "SHA256 doit contenir exactement 64 caractères hexadécimaux.";
            return false;
        }

        if (!HexSha.IsMatch(cleaned))
        {
            error = "SHA256 contient des caractères invalides.";
            return false;
        }

        hex = cleaned.ToLowerInvariant();
        error = null;
        return true;
    }

    private static int ComparePrerelease(string? left, string? right)
    {
        var leftEmpty = string.IsNullOrEmpty(left);
        var rightEmpty = string.IsNullOrEmpty(right);
        if (leftEmpty && rightEmpty)
        {
            return 0;
        }

        if (leftEmpty)
        {
            return 1;
        }

        if (rightEmpty)
        {
            return -1;
        }

        var leftParts = left!.Split('.');
        var rightParts = right!.Split('.');
        var count = Math.Max(leftParts.Length, rightParts.Length);
        for (var i = 0; i < count; i++)
        {
            if (i >= leftParts.Length)
            {
                return -1;
            }

            if (i >= rightParts.Length)
            {
                return 1;
            }

            var cmp = CompareIdentifier(leftParts[i], rightParts[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
        if (leftNumeric && rightNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftNumeric)
        {
            return -1;
        }

        if (rightNumeric)
        {
            return 1;
        }

        return string.CompareOrdinal(left, right);
    }
}
