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
            Description = "Enseignement préscolaire",
            IconKind = "BabyCarriage",
            AccentColor = "#22C55E",
            Matches = c => c.TemplateCode.StartsWith("MAT-", StringComparison.OrdinalIgnoreCase)
        },
        new()
        {
            Key = "primaire",
            Title = "Primaire",
            Description = "Enseignement de base",
            IconKind = "BookOpenPageVariant",
            AccentColor = "#1E5EFF",
            Matches = c => c.TemplateCode.StartsWith("PRI-", StringComparison.OrdinalIgnoreCase)
        },
        new()
        {
            Key = "secondaire-general",
            Title = "Secondaire Général",
            Description = "Cycle des Humanités",
            IconKind = "SchoolOutline",
            AccentColor = "#F59E0B",
            Matches = c =>
                c.TemplateCode.StartsWith("CTEB-", StringComparison.OrdinalIgnoreCase)
                || c.TemplateCode.StartsWith("FS-", StringComparison.OrdinalIgnoreCase)
                || (c.TemplateCode.StartsWith("HUM-", StringComparison.OrdinalIgnoreCase)
                    && c.HumanitiesSection is "Scientifique" or "Littéraire" or "Pédagogique" or "Sociale")
        },
        new()
        {
            Key = "humanites-techniques",
            Title = "Humanités Techniques",
            Description = "Filières techniques",
            IconKind = "Wrench",
            AccentColor = "#8B5CF6",
            Matches = c =>
                c.TemplateCode.StartsWith("HPRO-", StringComparison.OrdinalIgnoreCase)
                || (c.TemplateCode.StartsWith("HUM-", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(c.HumanitiesSection, "Technique", StringComparison.OrdinalIgnoreCase))
        },
        new()
        {
            Key = "commerciale-gestion",
            Title = "Commerciale & Gestion",
            Description = "Filières commerciales",
            IconKind = "BriefcaseOutline",
            AccentColor = "#14B8A6",
            Matches = c =>
                c.TemplateCode.StartsWith("HUM-", StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.HumanitiesSection, "Commerciale", StringComparison.OrdinalIgnoreCase)
        },
        new()
        {
            Key = "technique-rurale",
            Title = "Technique Rurale",
            Description = "Agriculture et ruralité",
            IconKind = "Barley",
            AccentColor = "#84CC16",
            Matches = c => ContainsAny(c.StudyOption, "Agriculture", "Topographie")
        },
        new()
        {
            Key = "informatique",
            Title = "Informatique",
            Description = "Filières numériques",
            IconKind = "Laptop",
            AccentColor = "#1D4ED8",
            Matches = c => ContainsAny(c.StudyOption, "Informatique")
        },
        new()
        {
            Key = "arts",
            Title = "Arts",
            Description = "Filières artistiques",
            IconKind = "PaletteOutline",
            AccentColor = "#EC4899",
            Matches = c => ContainsAny(c.StudyOption, "Arts")
        }
    ];

    public static StructureUiSection? FindSection(string key) =>
        Sections.FirstOrDefault(section => section.Key == key);

    public static StructureUiSection? ResolveSection(PedagogicalClassItemViewModel item) =>
        Sections.FirstOrDefault(section => section.Matches(item));

    public static string? ResolveSectionKey(PedagogicalClassItemViewModel item) =>
        ResolveSection(item)?.Key;

    public static bool SectionHasOptions(IEnumerable<PedagogicalClassItemViewModel> classes)
    {
        var list = classes.ToList();
        if (list.Count == 0)
        {
            return false;
        }

        var options = list
            .Select(c => c.StudyOption)
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return options.Count > 1 || (options.Count == 1 && list.Any(c => string.IsNullOrWhiteSpace(c.StudyOption)));
    }

    public static IEnumerable<IGrouping<string, PedagogicalClassItemViewModel>> GroupByOption(
        IEnumerable<PedagogicalClassItemViewModel> classes)
    {
        return classes
            .GroupBy(c => string.IsNullOrWhiteSpace(c.StudyOption) ? "Général" : c.StudyOption!)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string? value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}

public enum StructureDisplayFilter
{
    All,
    Enabled,
    Disabled,
    WithoutLocals
}
