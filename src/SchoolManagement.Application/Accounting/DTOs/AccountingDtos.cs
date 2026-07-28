namespace SchoolManagement.Application.Accounting.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record ExpenseRequestDto(
    Guid Id,
    string Reference,
    string Title,
    string? Description,
    decimal RequestedAmount,
    string Currency,
    DateOnly RequestDate,
    ExpenseRequestStatus Status,
    string StatusLabel,
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    Guid AcademicYearId,
    string AcademicYearLabel,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt);

public sealed record ExpensePaymentAllocationDto(
    Guid Id,
    Guid CurrencyId,
    string CurrencyCode,
    decimal Amount,
    Guid? ExchangeRateId,
    decimal AppliedExchangeRate,
    decimal EquivalentInPrimaryCurrency,
    int SortOrder,
    string RateDirectionLabel);

public sealed record ExpensePaymentDto(
    Guid Id,
    string Reference,
    string Label,
    string BeneficiaryName,
    string AuthorizedByName,
    decimal Amount,
    string Currency,
    Guid? PrimaryCurrencyId,
    DateOnly ExpenseDate,
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    Guid? ExpenseRequestId,
    Guid AcademicYearId,
    string AcademicYearLabel,
    bool HasMultiCurrencyAllocation = false,
    IReadOnlyList<ExpensePaymentAllocationDto>? Allocations = null,
    string? ExternalReference = null,
    string? Category = null,
    string? CategoryLabel = null,
    string? Observations = null,
    string? AttachmentFileName = null,
    bool HasAttachment = false);

public sealed record CreateExpenseRequestRequest(
    Guid AcademicYearId,
    Guid DestinationId,
    string Title,
    string? Description,
    decimal RequestedAmount,
    Currency Currency,
    DateOnly RequestDate);

/// <summary>Ligne de répartition multi-devises à l'enregistrement.</summary>
public sealed record CreateExpensePaymentAllocationLine(
    Guid CurrencyId,
    decimal Amount,
    decimal? OverrideRate = null);

public sealed record CreateExpensePaymentRequest(
    Guid AcademicYearId,
    Guid DestinationId,
    string Label,
    string BeneficiaryName,
    string AuthorizedByName,
    decimal Amount,
    Currency Currency,
    DateOnly ExpenseDate,
    Guid? ExpenseRequestId = null,
    Guid? PrimaryCurrencyId = null,
    IReadOnlyList<CreateExpensePaymentAllocationLine>? CurrencyAllocations = null,
    string? ExternalReference = null,
    string? Category = null,
    string? Observations = null,
    string? AttachmentFileName = null,
    string? AttachmentStoragePath = null);

public sealed record ExpenseSearchRequest(
    Guid? AcademicYearId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? DestinationId = null,
    ExpenseRequestStatus? Status = null,
    int Page = 1,
    int PageSize = 100);

public sealed record ExpenseRequestSearchResultDto(
    IReadOnlyList<ExpenseRequestDto> Items,
    int TotalCount);

public sealed record ExpensePaymentSearchResultDto(
    IReadOnlyList<ExpensePaymentDto> Items,
    int TotalCount,
    decimal TotalAmount = 0m);

/// <summary>Solde d'un compte bénéficiaire pour une devise (imputation des dépenses).</summary>
public sealed record ExpenseDestinationBalanceDto(
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    Guid? CurrencyId,
    string Currency,
    decimal AllocatedAmount,
    decimal SpentAmount,
    decimal AvailableAmount);
