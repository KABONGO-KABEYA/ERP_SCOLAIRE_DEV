namespace SchoolManagement.Application.RevenueAllocation.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record RevenueDestinationDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record SaveRevenueDestinationRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record RevenueAllocationKeyDetailDto(
    Guid Id,
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    AllocationCalculationType CalculationType,
    decimal Value,
    int SortOrder);

public sealed record RevenueAllocationKeyDto(
    Guid Id,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid FeeTypeId,
    string FeeTypeCode,
    string FeeTypeName,
    string Name,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    bool HasAllocationHistory,
    bool CanDelete,
    IReadOnlyList<RevenueAllocationKeyDetailDto> Details,
    decimal PercentageTotal);

public sealed record SaveRevenueAllocationKeyDetailRequest(
    Guid DestinationId,
    decimal Value,
    int SortOrder);

public sealed record CreateRevenueAllocationKeyRequest(
    Guid AcademicYearId,
    Guid FeeTypeId,
    string Name,
    string? Notes,
    DateOnly StartDate,
    IReadOnlyList<SaveRevenueAllocationKeyDetailRequest> Details);

public sealed record UpdateRevenueAllocationKeyRequest(
    string Name,
    string? Notes,
    DateOnly StartDate,
    IReadOnlyList<SaveRevenueAllocationKeyDetailRequest> Details);

public sealed record CloseRevenueAllocationKeyRequest(DateOnly? EndDate);

public sealed record RevenueAllocationEntryDto(
    Guid Id,
    Guid PaymentId,
    string ReceiptNumber,
    Guid StudentId,
    string StudentName,
    decimal PaymentAmount,
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    decimal AllocatedAmount,
    decimal? AppliedPercentage,
    AllocationCalculationType CalculationType,
    Guid AllocationKeyId,
    string AllocationKeyName,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid? FeeTypeId,
    string? FeeTypeName,
    DateTime AllocatedAt,
    string? AllocatedBy);

public sealed record RevenueAllocationSearchRequest(
    Guid? AcademicYearId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    Guid? StudentId,
    Guid? PaymentId,
    Guid? DestinationId,
    Guid? FeeTypeId,
    int Page = 1,
    int PageSize = 50);

public sealed record DestinationTotalDto(Guid DestinationId, string Code, string Name, decimal Total);

public sealed record FeeTypeTotalDto(Guid FeeTypeId, string Code, string Name, decimal Total);

public sealed record RevenueAllocationTotalsDto(
    decimal GrandTotal,
    IReadOnlyList<DestinationTotalDto> ByDestination,
    IReadOnlyList<FeeTypeTotalDto> ByFeeType);

public sealed record RevenueAllocationSearchResultDto(
    IReadOnlyList<RevenueAllocationEntryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    RevenueAllocationTotalsDto Totals);

/// <summary>Ligne calculée (réutilisable Comptabilité / Budget / Caisse).</summary>
public sealed record CalculatedAllocationLine(
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    AllocationCalculationType CalculationType,
    decimal Amount,
    decimal? AppliedPercentage);
