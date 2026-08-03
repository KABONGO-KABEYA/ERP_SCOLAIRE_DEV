using SchoolManagement.Domain.Entities.Deliberation;

namespace SchoolManagement.Application.Mentions;

/// <summary>
/// Résout le libellé de mention à partir du pourcentage et des définitions paramétrées.
/// Source de vérité unique — jamais de règles hardcodées dans l'UI.
/// </summary>
public static class MentionLabelResolver
{
    public static string? Resolve(
        decimal percentage,
        IReadOnlyList<ResultMentionDefinition> definitions)
    {
        if (definitions.Count == 0)
        {
            return null;
        }

        return definitions
            .OrderByDescending(m => m.MinPercentageInclusive)
            .FirstOrDefault(m =>
                percentage >= m.MinPercentageInclusive
                && percentage <= m.MaxPercentageInclusive)
            ?.Label;
    }

    /// <summary>
    /// Préfère la valeur déjà persistée ; sinon calcule depuis les plages actives.
    /// </summary>
    public static string? ResolveOrFallback(
        string? persistedAppreciation,
        decimal percentage,
        IReadOnlyList<ResultMentionDefinition> definitions)
    {
        if (!string.IsNullOrWhiteSpace(persistedAppreciation))
        {
            return persistedAppreciation.Trim();
        }

        return Resolve(percentage, definitions);
    }
}
