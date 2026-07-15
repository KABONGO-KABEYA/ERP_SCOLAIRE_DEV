namespace SchoolManagement.Application.Schools;

using SchoolManagement.Domain.Entities.Settings;

public static class ClassLocalCodeBuilder
{
    public const int MaxCodeLength = 80;

    public static string Build(PedagogicalClass pedagogicalClass, string localName, int existingCount)
    {
        var suffix = BuildSuffix(localName, existingCount);
        var code = existingCount > 0
            ? $"{pedagogicalClass.TemplateCode}-{suffix}{existingCount + 1}"
            : $"{pedagogicalClass.TemplateCode}-{suffix}";

        return Fit(code);
    }

    public static string BuildFromSourceCode(string sourceCode, string localName, int existingCount)
    {
        var suffix = BuildSuffix(localName, existingCount);
        var prefix = sourceCode.Contains('-', StringComparison.Ordinal)
            ? sourceCode[..sourceCode.LastIndexOf('-')]
            : sourceCode;

        var code = existingCount > 0
            ? $"{prefix}-{suffix}{existingCount + 1}"
            : $"{prefix}-{suffix}";

        return Fit(code);
    }

    public static string WithSuffix(string baseCode, int sequence) =>
        Fit(sequence <= 1 ? baseCode : $"{baseCode}-{sequence}");

    private static string BuildSuffix(string localName, int existingCount)
    {
        var suffix = new string(localName
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .Take(6)
            .ToArray());

        return string.IsNullOrEmpty(suffix) ? $"L{existingCount + 1}" : suffix;
    }

    private static string Fit(string code)
    {
        if (code.Length <= MaxCodeLength)
        {
            return code;
        }

        var lastDash = code.LastIndexOf('-');
        if (lastDash <= 0)
        {
            return code[..MaxCodeLength];
        }

        var suffixPart = code[lastDash..];
        var prefixBudget = MaxCodeLength - suffixPart.Length;
        if (prefixBudget <= 0)
        {
            return suffixPart.Length <= MaxCodeLength
                ? suffixPart.TrimStart('-')
                : suffixPart[^MaxCodeLength..];
        }

        return string.Concat(code.AsSpan(0, prefixBudget), suffixPart);
    }
}
