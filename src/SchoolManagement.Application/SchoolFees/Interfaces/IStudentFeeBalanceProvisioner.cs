using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.SchoolFees.Interfaces;

/// <summary>
/// Génère / aligne les <c>StudentFeeBalances</c> à partir des <c>ClassFeeAmounts</c>
/// (une ligne de solde par tranche configurée). <c>AmountDue</c> est figé à la création.
/// </summary>
public interface IStudentFeeBalanceProvisioner
{
    /// <summary>
    /// Crée les soldes manquants pour l'élève selon année / classe pédagogique / catégorie tarifaire.
    /// Ne modifie jamais <c>AmountDue</c> d'un solde déjà créé. Soft-supprime les soldes non payés
    /// de la même année qui ne correspondent plus à la catégorie/classe cible.
    /// </summary>
    /// <returns>Somme des <c>AmountDue</c> actifs pour l'année après provisionnement.</returns>
    Task<decimal> ProvisionForStudentAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid feePricingCategoryId,
        Currency currency,
        CancellationToken cancellationToken = default);
}
