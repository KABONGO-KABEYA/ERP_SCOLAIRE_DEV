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
    /// Préparé pour le module Finance : récupère les retenues actives applicables
    /// (année + type de frais + tranche optionnelle + catégorie optionnelle).
    /// </summary>
    Task<IReadOnlyList<WithholdingConfigurationDto>> ResolveApplicableAsync(
        Guid schoolId,
        WithholdingResolveContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Préparé pour le module Finance : calcule TotalRetenues et MontantNet à partir d'un montant brut.
    /// </summary>
    Task<WithholdingCalculationResult> CalculateForPaymentLineAsync(
        Guid schoolId,
        decimal grossAmount,
        WithholdingResolveContext context,
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
