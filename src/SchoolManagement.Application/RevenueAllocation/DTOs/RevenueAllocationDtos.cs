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
    RevenueAllocationSourceKind SourceKind,
    Guid? FeeTypeId,
    string? FeeTypeCode,
    string? FeeTypeName,
    Guid? WithholdingTypeId,
    string? WithholdingTypeCode,
    string? WithholdingTypeName,
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
    Guid? FeeTypeId,
    Guid? WithholdingTypeId,
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
    Guid? AllocationKeyId,
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
    Guid? SectionId = null,
    Guid? ClassRoomId = null,
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

/// <summary>Ligne agrégée : compte bénéficiaire pour un type de frais.</summary>
public sealed record FeeTypeAllocationDestinationSummaryDto(
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    decimal Percentage,
    decimal AllocatedAmount);

/// <summary>Groupe de répartition par type de frais (montants globaux).</summary>
public sealed record FeeTypeAllocationSummaryGroupDto(
    Guid FeeTypeId,
    string FeeTypeCode,
    string FeeTypeName,
    decimal FeeTypeTotal,
    IReadOnlyList<FeeTypeAllocationDestinationSummaryDto> Destinations);

/// <summary>Solde d'un compte bénéficiaire (J-1, encaissements, dépenses, solde période).</summary>
public sealed record AllocationCashFlowRowDto(
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    decimal PeriodJ1,
    decimal Encaissement,
    decimal DepenseP,
    decimal PeriodeP);

/// <summary>Groupe journalier de répartition comptable.</summary>
public sealed record AllocationCashFlowDailyGroupDto(
    DateOnly Date,
    IReadOnlyList<AllocationCashFlowRowDto> Rows);

/// <summary>Résultat global + journalier des répartitions comptables.</summary>
public sealed record AllocationCashFlowResultDto(
    IReadOnlyList<AllocationCashFlowRowDto> GlobalRows,
    IReadOnlyList<AllocationCashFlowDailyGroupDto> DailyGroups,
    AllocationCashFlowRowDto Totals);

/// <summary>Ligne calculée (réutilisable Comptabilité / Budget / Caisse).</summary>
public sealed record CalculatedAllocationLine(
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    AllocationCalculationType CalculationType,
    decimal Amount,
    decimal? AppliedPercentage);

/// <summary>Ligne élève ayant occasionné une retenue.</summary>
public sealed record WithholdingReportStudentLineDto(
    Guid StudentId,
    string StudentName,
    Guid PaymentId,
    DateOnly PaymentDate,
    decimal Amount);

/// <summary>Groupe de retenues par type (rupture).</summary>
public sealed record WithholdingReportTypeGroupDto(
    Guid WithholdingTypeId,
    string WithholdingTypeCode,
    string WithholdingTypeName,
    decimal TypeTotal,
    IReadOnlyList<WithholdingReportStudentLineDto> Students);

/// <summary>Résultat du rapport retenues (groupé par type, détail élève).</summary>
public sealed record WithholdingReportResultDto(
    IReadOnlyList<WithholdingReportTypeGroupDto> Groups,
    decimal GrandTotal,
    int PaymentCount);
