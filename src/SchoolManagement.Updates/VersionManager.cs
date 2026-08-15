using System.Globalization;

namespace SchoolManagement.Updates;

/// <summary>
/// Comparaison SemVer 2.0 (1.0.9 &lt; 1.0.10, 1.2.0-beta &lt; 1.2.0).
/// La métadonnée <c>+…</c> est ignorée ; le suffixe de pré-release <c>-beta</c>, <c>-rc</c>, etc. est conservé.
/// </summary>
public static class VersionManager
{
    public readonly record struct ProductVersion(
        int Major,
        int Minor,
        int Patch,
        int Build,
        string? Prerelease)
    {
        public bool HasPrerelease => !string.IsNullOrEmpty(Prerelease);

        public bool IsZero =>
            Major == 0 && Minor == 0 && Patch == 0 && Build == 0 && !HasPrerelease;

        public string ToNormalizedString()
        {
            var core = Build > 0
                ? $"{Major}.{Minor}.{Patch}.{Build}"
                : $"{Major}.{Minor}.{Patch}";
            return HasPrerelease ? $"{core}-{Prerelease}" : core;
        }
    }

    public static ProductVersion Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ProductVersion(0, 0, 0, 0, null);
        }

        var cleaned = value.Trim();
        var plus = cleaned.IndexOf('+');
        if (plus >= 0)
        {
            cleaned = cleaned[..plus];
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
        var numbers = new int[4];
        for (var i = 0; i < Math.Min(parts.Length, 4); i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]))
            {
                numbers[i] = 0;
            }
        }

        return new ProductVersion(numbers[0], numbers[1], numbers[2], numbers[3], prerelease);
    }

    public static int Compare(string? left, string? right) =>
        Compare(Parse(left), Parse(right));

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

    public static bool IsNewer(string? candidate, string? current) =>
        Compare(candidate, current) > 0;

    public static bool IsOlderThan(string? current, string? minimum) =>
        Compare(current, minimum) < 0;

    public static string Normalize(string? value) => Parse(value).ToNormalizedString();

    private static int ComparePrerelease(string? left, string? right)
    {
        var leftEmpty = string.IsNullOrEmpty(left);
        var rightEmpty = string.IsNullOrEmpty(right);
        if (leftEmpty && rightEmpty)
        {
            return 0;
        }

        // Une version de release a une précédence supérieure à une pré-release de mêmes nombres.
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
