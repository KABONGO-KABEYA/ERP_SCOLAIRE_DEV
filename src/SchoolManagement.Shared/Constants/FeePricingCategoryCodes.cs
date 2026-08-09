namespace SchoolManagement.Shared.Constants;

/// <summary>
/// Codes standards pour les catégories tarifaires.
/// </summary>
public static class FeePricingCategoryCodes
{
    /// <summary>Catégorie par défaut créée et assignée à chaque nouvelle inscription.</summary>
    public const string General = "GENERAL";

    /// <summary>Libellés équivalents à la catégorie par défaut (éviter les doublons à l'installation).</summary>
    public static bool IsGeneralDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        return trimmed.Equals("Générale", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Général", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("General", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Generale", StringComparison.OrdinalIgnoreCase);
    }
}
