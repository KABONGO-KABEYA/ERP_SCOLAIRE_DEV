namespace SchoolManagement.Application.RevenueAllocation.Interfaces;

using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Domain.Entities.Finance;

public interface IRevenueAllocationEngine
{
    /// <summary>
    /// Calcule la répartition d'un montant selon les détails d'une clé.
    /// Méthode pure, réutilisable hors contexte paiement.
    /// </summary>
    IReadOnlyList<CalculatedAllocationLine> Calculate(
        decimal paymentAmount,
        IReadOnlyList<RevenueAllocationKeyDetail> details);

    /// <summary>Valide une clé avant activation. Retourne les messages d'erreur.</summary>
    IReadOnlyList<string> ValidateKeyForActivation(IReadOnlyList<RevenueAllocationKeyDetail> details);
}
