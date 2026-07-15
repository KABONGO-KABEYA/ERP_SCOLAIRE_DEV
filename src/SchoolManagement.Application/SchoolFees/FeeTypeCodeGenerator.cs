namespace SchoolManagement.Application.SchoolFees;

internal static class FeeTypeCodeGenerator
{
    public static string Generate(string name, IEnumerable<string> existingCodes)
    {
        var codes = existingCodes
            .Select(c => c.Trim().ToUpperInvariant())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var baseCode = BuildBaseCode(name);
        if (!codes.Contains(baseCode))
        {
            return baseCode;
        }

        for (var i = 2; i <= 99; i++)
        {
            var suffix = i.ToString();
            var prefixLength = Math.Max(1, Math.Min(20 - suffix.Length, baseCode.Length));
            var candidate = $"{baseCode[..prefixLength]}{suffix}";
            if (!codes.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"F{Guid.NewGuid():N}"[..8].ToUpperInvariant();
    }

    private static string BuildBaseCode(string name)
    {
        var words = name.Split([' ', '\'', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            var acronym = new string(words
                .Where(w => w.Length > 0)
                .Select(w => char.ToUpperInvariant(w[0]))
                .ToArray());

            if (acronym.Length >= 3)
            {
                return acronym.Length > 20 ? acronym[..20] : acronym;
            }
        }

        var letters = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return letters.Length switch
        {
            0 => "FRAIS",
            < 4 => letters.PadRight(4, 'X'),
            _ => letters[..Math.Min(6, letters.Length)]
        };
    }
}
