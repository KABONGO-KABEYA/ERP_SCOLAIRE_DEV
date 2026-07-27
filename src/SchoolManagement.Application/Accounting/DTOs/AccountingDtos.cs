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

public sealed record ExpensePaymentDto(
    Guid Id,
    string Reference,
    string Label,
    string BeneficiaryName,
    string AuthorizedByName,
    decimal Amount,
    string Currency,
    DateOnly ExpenseDate,
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    Guid? ExpenseRequestId,
    Guid AcademicYearId,
    string AcademicYearLabel);

public sealed record CreateExpenseRequestRequest(
    Guid AcademicYearId,
    Guid DestinationId,
    string Title,
    string? Description,
    decimal RequestedAmount,
    Currency Currency,
    DateOnly RequestDate);

public sealed record CreateExpensePaymentRequest(
    Guid AcademicYearId,
    Guid DestinationId,
    string Label,
    string BeneficiaryName,
    string AuthorizedByName,
    decimal Amount,
    Currency Currency,
    DateOnly ExpenseDate,
    Guid? ExpenseRequestId = null);

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

/// <summary>Solde d'un compte bénéficiaire pour l'imputation des dépenses.</summary>
public sealed record ExpenseDestinationBalanceDto(
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    decimal AllocatedAmount,
    decimal SpentAmount,
    decimal AvailableAmount,
    string Currency = "CDF");
