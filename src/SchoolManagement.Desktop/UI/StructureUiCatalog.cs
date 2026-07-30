using SchoolManagement.Domain.Enums;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.UI;

public sealed class StructureUiSection
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string IconKind { get; init; }

    public required string AccentColor { get; init; }

    public required Func<PedagogicalClassItemViewModel, bool> Matches { get; init; }
}

public static class StructureUiCatalog
{
    public static IReadOnlyList<StructureUiSection> Sections { get; } =
    [
        new()
        {
            Key = "maternelle",
            Title = "Maternelle",
            Description = "Enseignement préscolaire (1re à 3e maternelle)",
            IconKind = "BabyCarriage",
            AccentColor = "#22C55E",
            Matches = c => c.Program == SchoolProgram.Maternelle
        },
        new()
        {
            Key = "primaire",
            Title = "Primaire",
            Description = "Enseignement primaire (1re à 6e primaire)",
            IconKind = "BookOpenPageVariant",
            AccentColor = "#1E5EFF",
            Matches = c => c.Program == SchoolProgram.Primaire
        },
        new()
        {
            Key = "secondaire-generale",
            Title = "Secondaire générale",
            Description = "7e et 8e année (CTEB)",
            IconKind = "SchoolOutline",
            AccentColor = "#F59E0B",
            Matches = c => c.Program == SchoolProgram.CTEB
        },
        new()
        {
            Key = "humanite",
            Title = "Humanité",
            Description = "Humanités, filières techniques, commerciales et spécialisées",
            IconKind = "AccountSchoolOutline",
            AccentColor = "#8B5CF6",
            Matches = c => c.Program is SchoolProgram.Humanites
                or SchoolProgram.HumanitesProfessionnelles
                or SchoolProgram.FilieresSpecialisees
        }
    ];

    public static StructureUiSection? FindSection(string key) =>
        Sections.FirstOrDefault(section => section.Key == key);

    public static StructureUiSection? ResolveSection(PedagogicalClassItemViewModel item) =>
        Sections.FirstOrDefault(section => section.Matches(item));

    public static string? ResolveSectionKey(PedagogicalClassItemViewModel item) =>
        ResolveSection(item)?.Key;

    public static string GetOptionGroupKey(PedagogicalClassItemViewModel item, string sectionKey)
    {
        if (sectionKey == "humanite")
        {
            if (!string.IsNullOrWhiteSpace(item.HumanitiesSection)
                && !string.IsNullOrWhiteSpace(item.StudyOption))
            {
                return $"{item.HumanitiesSection} — {item.StudyOption}";
            }

            if (!string.IsNullOrWhiteSpace(item.HumanitiesSection))
            {
                return item.HumanitiesSection!;
            }

            if (!string.IsNullOrWhiteSpace(item.StudyOption))
            {
                return item.StudyOption!;
            }

            return item.ProgramLabel;
        }

        return string.IsNullOrWhiteSpace(item.StudyOption) ? "Général" : item.StudyOption!;
    }

    public static bool SectionHasOptions(IEnumerable<PedagogicalClassItemViewModel> classes, string sectionKey)
    {
        var list = classes.ToList();
        if (list.Count == 0)
        {
            return false;
        }

        if (sectionKey == "humanite")
        {
            return list.Select(c => GetOptionGroupKey(c, sectionKey))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1;
        }

        var options = list
            .Select(c => GetOptionGroupKey(c, sectionKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return options.Count > 1 || (options.Count == 1 && list.Any(c => string.IsNullOrWhiteSpace(c.StudyOption)));
    }

    public static IEnumerable<IGrouping<string, PedagogicalClassItemViewModel>> GroupByOption(
        IEnumerable<PedagogicalClassItemViewModel> classes,
        string sectionKey)
    {
        return classes
            .GroupBy(c => GetOptionGroupKey(c, sectionKey))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
    }
}

public enum StructureDisplayFilter
{
    All,
    Enabled,
    Disabled,
    WithoutLocals
}
