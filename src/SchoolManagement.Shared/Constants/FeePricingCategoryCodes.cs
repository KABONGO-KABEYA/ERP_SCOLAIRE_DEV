namespace SchoolManagement.Shared.Constants;

/// <summary>
/// Codes recommandés pour les catégories tarifaires.
/// Aucune catégorie n'est créée automatiquement en base — ces constantes préparent l'intégration future
/// (inscription, facturation) sans codage en dur des libellés.
/// </summary>
public static class FeePricingCategoryCodes
{
    /// <summary>Code suggéré pour la catégorie par défaut à créer manuellement par l'établissement.</summary>
    public const string General = "GENERAL";
}
