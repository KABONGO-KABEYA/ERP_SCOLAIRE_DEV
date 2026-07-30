namespace SchoolManagement.Application.Schools;

using SchoolManagement.Domain.Enums;

/// <summary>
/// Sections administratives canoniques d'une école RDC (4 sections).
/// </summary>
public static class PedagogicalSectionCatalog
{
    public static readonly IReadOnlyList<(string Code, string Name, EducationCycle Cycle)> RequiredSections =
    [
        ("MAT", "Maternelle", EducationCycle.Primaire),
        ("PRI", "Primaire", EducationCycle.Primaire),
        ("CTEB", "Secondaire générale", EducationCycle.Secondaire),
        ("HUM", "Humanité", EducationCycle.Secondaire),
    ];

    public static readonly HashSet<string> CanonicalCodes =
        RequiredSections.Select(s => s.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> LegacySectionCodeMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PRIM"] = "PRI",
            ["SEC-SCI"] = "HUM",
            ["SEC-LIT"] = "HUM",
            ["HPRO"] = "HUM",
            ["FS"] = "HUM",
        };

    public static string ResolveLegacySectionCode(string code)
    {
        if (CanonicalCodes.Contains(code))
        {
            return code;
        }

        return LegacySectionCodeMap.TryGetValue(code, out var mapped) ? mapped : "HUM";
    }
}
