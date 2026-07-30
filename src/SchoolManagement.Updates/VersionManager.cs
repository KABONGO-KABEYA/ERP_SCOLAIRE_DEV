namespace SchoolManagement.Updates;

/// <summary>Comparaison sémantique de versions (1.0.9 &lt; 1.0.10 &lt; 1.1.0).</summary>
public static class VersionManager
{
    public static Version Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Version(0, 0, 0, 0);
        }

        var cleaned = value.Trim();
        var plus = cleaned.IndexOf('+');
        if (plus >= 0)
        {
            cleaned = cleaned[..plus];
        }

        var dash = cleaned.IndexOf('-');
        if (dash >= 0)
        {
            cleaned = cleaned[..dash];
        }

        var parts = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var numbers = new int[4];
        for (var i = 0; i < Math.Min(parts.Length, 4); i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]))
            {
                numbers[i] = 0;
            }
        }

        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    public static int Compare(string? left, string? right) =>
        Parse(left).CompareTo(Parse(right));

    public static bool IsNewer(string? candidate, string? current) =>
        Compare(candidate, current) > 0;

    public static bool IsOlderThan(string? current, string? minimum) =>
        Compare(current, minimum) < 0;

    public static string Normalize(string? value)
    {
        var v = Parse(value);
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
