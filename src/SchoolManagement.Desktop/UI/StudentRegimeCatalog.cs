namespace SchoolManagement.Desktop.UI;

public static class StudentRegimeCatalog
{
    public static readonly IReadOnlyList<string> DisplayOrder =
    [
        "Maternelle",
        "Primaire",
        "Secondaire"
    ];

    public static string ResolveRegime(string? sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return "Secondaire";
        }

        var name = sectionName.Trim();
        if (name.Contains("Maternelle", StringComparison.OrdinalIgnoreCase))
        {
            return "Maternelle";
        }

        if (name.Contains("Primaire", StringComparison.OrdinalIgnoreCase))
        {
            return "Primaire";
        }

        return "Secondaire";
    }

    public static int SortKey(string regime)
    {
        for (var i = 0; i < DisplayOrder.Count; i++)
        {
            if (string.Equals(DisplayOrder[i], regime, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return DisplayOrder.Count;
    }
}
