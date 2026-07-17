namespace SchoolManagement.Application.Withholdings.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record WithholdingTypeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record SaveWithholdingTypeRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record WithholdingConfigurationDto(
    Guid Id,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid WithholdingTypeId,
    string WithholdingTypeCode,
    string WithholdingTypeName,
    Guid FeeTypeId,
    string FeeTypeCode,
    string FeeTypeName,
    Guid? FeeInstallmentId,
    string? FeeInstallmentName,
    Guid? PricingCategoryId,
    string? PricingCategoryName,
    WithholdingCalculationMode CalculationMode,
    decimal Value,
    bool IsActive);

public sealed record SaveWithholdingConfigurationRequest(
    Guid AcademicYearId,
    Guid WithholdingTypeId,
    Guid FeeTypeId,
    Guid? FeeInstallmentId,
    Guid? PricingCategoryId,
    WithholdingCalculationMode CalculationMode,
    decimal Value,
    bool IsActive);

public sealed record WithholdingConfigurationSearchRequest(
    Guid? AcademicYearId,
    Guid? WithholdingTypeId,
    Guid? FeeTypeId,
    Guid? FeeInstallmentId,
    Guid? PricingCategoryId,
    WithholdingCalculationMode? CalculationMode,
    bool? ActiveOnly,
    string? Search,
    int Page = 1,
    int PageSize = 50);

public sealed record WithholdingConfigurationSearchResultDto(
    IReadOnlyList<WithholdingConfigurationDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Contexte pour résoudre les retenues applicables à une ligne d'encaissement.</summary>
public sealed record WithholdingResolveContext(
    Guid AcademicYearId,
    Guid FeeTypeId,
    Guid? FeeInstallmentId,
    Guid? PricingCategoryId);

/// <summary>Ligne de retenue calculée (intégration future Finance → Répartition).</summary>
public sealed record CalculatedWithholdingLine(
    Guid ConfigurationId,
    Guid WithholdingTypeId,
    string WithholdingTypeCode,
    string WithholdingTypeName,
    WithholdingCalculationMode CalculationMode,
    decimal ConfiguredValue,
    decimal WithheldAmount);

/// <summary>Résultat de calcul des retenues sur un montant brut.</summary>
public sealed record WithholdingCalculationResult(
    decimal GrossAmount,
    decimal TotalWithheld,
    decimal NetAmount,
    IReadOnlyList<CalculatedWithholdingLine> Lines);

/// <summary>Requête API pour calculer les retenues (intégration future Finance).</summary>
public sealed record WithholdingCalculateRequest(
    decimal GrossAmount,
    WithholdingResolveContext Context);
