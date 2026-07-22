namespace SchoolManagement.Application.Withholdings.Interfaces;

using SchoolManagement.Application.Withholdings.DTOs;
using SchoolManagement.Domain.Entities.Finance;

public interface IWithholdingEngine
{
    /// <summary>
    /// Calcule les montants retenus et le net à transmettre à la répartition des recettes.
    /// </summary>
    WithholdingCalculationResult Calculate(decimal grossAmount, IReadOnlyList<WithholdingConfiguration> configurations);
}

public interface IWithholdingService
{
    Task EnsureDefaultTypesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WithholdingTypeDto>> GetTypesAsync(Guid schoolId, bool activeOnly = false, CancellationToken cancellationToken = default);

    Task<WithholdingTypeDto> CreateTypeAsync(Guid schoolId, SaveWithholdingTypeRequest request, CancellationToken cancellationToken = default);

    Task<WithholdingTypeDto> UpdateTypeAsync(Guid schoolId, Guid typeId, SaveWithholdingTypeRequest request, CancellationToken cancellationToken = default);

    Task DeactivateTypeAsync(Guid schoolId, Guid typeId, CancellationToken cancellationToken = default);

    Task<WithholdingConfigurationSearchResultDto> SearchConfigurationsAsync(
        Guid schoolId,
        WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<WithholdingConfigurationDto?> GetConfigurationByIdAsync(Guid schoolId, Guid configurationId, CancellationToken cancellationToken = default);

    Task<WithholdingConfigurationDto> CreateConfigurationAsync(
        Guid schoolId,
        SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task<WithholdingConfigurationDto> UpdateConfigurationAsync(
        Guid schoolId,
        Guid configurationId,
        SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateConfigurationAsync(Guid schoolId, Guid configurationId, CancellationToken cancellationToken = default);

    Task DeleteConfigurationAsync(Guid schoolId, Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retenues actives applicables à un versement
    /// (année + type de frais + tranche optionnelle + catégorie optionnelle).
    /// </summary>
    Task<IReadOnlyList<WithholdingConfigurationDto>> ResolveApplicableAsync(
        Guid schoolId,
        WithholdingResolveContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcule TotalRetenues et MontantNet pour un montant brut d'encaissement.
    /// Montant fixe : une seule fois par rubrique. Pourcentage : à chaque versement tant que la rubrique n'est pas soldée.
    /// </summary>
    Task<WithholdingCalculationResult> CalculateForPaymentLineAsync(
        Guid schoolId,
        decimal grossAmount,
        WithholdingResolveContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Enregistre les retenues calculées lors d'un encaissement.</summary>
    Task RecordApplicationsAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid paymentId,
        Guid paymentLineId,
        WithholdingCalculationResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Configurations de retenue fixe déjà liées à ce paiement, indexées par ligne de paiement
    /// (pour les conserver lors d'une modification de montant, uniquement sur la bonne ligne).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> GetFixedApplicationConfigurationIdsByLineAsync(
        Guid schoolId,
        Guid paymentId,
        CancellationToken cancellationToken = default);

    /// <summary>Supprime les retenues enregistrées pour un paiement (annulation / modification).</summary>
    Task RemoveApplicationsForPaymentAsync(
        Guid schoolId,
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportConfigurationsExcelAsync(
        Guid schoolId,
        WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportConfigurationsPdfAsync(
        Guid schoolId,
        WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default);
}
