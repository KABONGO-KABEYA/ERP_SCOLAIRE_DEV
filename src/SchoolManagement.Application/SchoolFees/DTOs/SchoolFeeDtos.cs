namespace SchoolManagement.Application.SchoolFees.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record FeeTypeDto(
    Guid Id,
    string Code,
    string Name,
    Currency Currency,
    bool IsMandatory,
    bool IsActive);

public sealed record CreateFeeTypeRequest(
    string Name,
    Currency Currency,
    bool IsMandatory,
    bool IsActive);

public sealed record UpdateFeeTypeRequest(
    string Name,
    Currency Currency,
    bool IsMandatory,
    bool IsActive);

public sealed record FeeInstallmentDto(
    Guid Id,
    string Name,
    int SortOrder,
    bool IsActive);

public sealed record SaveFeeInstallmentRequest(
    string Name,
    int SortOrder,
    bool IsActive);

public sealed record FeePricingCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record CreateFeePricingCategoryRequest(
    string Name,
    string? Description,
    bool IsActive);

public sealed record UpdateFeePricingCategoryRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record FeeTypeInstallmentDto(
    Guid Id,
    Guid FeeInstallmentId,
    string InstallmentName,
    int SortOrder);

public sealed record SaveFeeTypeInstallmentItemRequest(
    Guid FeeInstallmentId,
    int SortOrder);

public sealed record SaveFeeTypeInstallmentsRequest(
    IReadOnlyList<SaveFeeTypeInstallmentItemRequest> Items);

public sealed record ClassFeeScheduleSignatureDto(
    Guid PedagogicalClassId,
    string Signature,
    bool IsConfigured);

public sealed record ClassFeeScheduleLineDto(
    Guid? Id,
    Guid FeeInstallmentId,
    string InstallmentName,
    int SortOrder,
    decimal Amount,
    DateOnly? DueDate);

public sealed record ClassFeeScheduleDto(
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid PedagogicalClassId,
    string PedagogicalClassName,
    Guid FeePricingCategoryId,
    string FeePricingCategoryCode,
    string FeePricingCategoryName,
    Guid FeeTypeId,
    string FeeTypeCode,
    string FeeTypeName,
    Currency Currency,
    decimal AnnualTotal,
    IReadOnlyList<ClassFeeScheduleLineDto> Lines);

public sealed record SaveClassFeeScheduleLineRequest(
    Guid FeeInstallmentId,
    int SortOrder,
    decimal Amount,
    DateOnly? DueDate);

public sealed record SaveClassFeeScheduleRequest(
    Guid AcademicYearId,
    Guid PedagogicalClassId,
    Guid FeePricingCategoryId,
    Guid FeeTypeId,
    IReadOnlyList<SaveClassFeeScheduleLineRequest> Lines);

public sealed record SaveClassFeeScheduleBulkRequest(
    Guid AcademicYearId,
    IReadOnlyList<Guid> PedagogicalClassIds,
    Guid FeePricingCategoryId,
    Guid FeeTypeId,
    IReadOnlyList<SaveClassFeeScheduleLineRequest> Lines);

public sealed record SaveClassFeeScheduleBulkResult(
    int SavedClassCount,
    IReadOnlyList<string> ClassNames);

public sealed record CopyClassFeeScheduleRequest(
    Guid TargetAcademicYearId,
    Guid PedagogicalClassId,
    Guid FeePricingCategoryId,
    Guid FeeTypeId,
    Guid? SourceAcademicYearId = null);

public sealed record CopyClassFeeScheduleBulkRequest(
    Guid TargetAcademicYearId,
    IReadOnlyList<Guid> PedagogicalClassIds,
    Guid FeePricingCategoryId,
    Guid FeeTypeId,
    Guid? SourceAcademicYearId = null);

public sealed record CopyClassFeeScheduleBulkResult(
    int CopiedCount,
    int ClassCount,
    string SourceYearLabel,
    string TargetYearLabel);

public sealed record CopyClassFeeScheduleResult(
    int CopiedCount,
    string SourceYearLabel,
    string TargetYearLabel);

public sealed record SchoolFeeCatalogDto(
    IReadOnlyList<FeeTypeDto> FeeTypes,
    IReadOnlyList<FeeInstallmentDto> Installments,
    IReadOnlyList<FeePricingCategoryDto> PricingCategories);
