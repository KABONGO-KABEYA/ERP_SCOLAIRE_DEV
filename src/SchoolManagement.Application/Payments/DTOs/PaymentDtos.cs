namespace SchoolManagement.Application.Payments.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record PaymentLineRequest(
    Guid FeeTypeId,
    decimal Amount,
    Currency Currency,
    string? Description,
    Guid? FeeInstallmentId = null,
    string? PhysicalReceiptNumber = null);

public sealed record CreatePaymentRequest(
    Guid StudentId,
    Guid AcademicYearId,
    Guid? BankId,
    Currency Currency,
    string? Notes,
    IReadOnlyList<PaymentLineRequest> Lines,
    DateTime? PaymentDate = null);

public sealed record PaymentLineDto(
    Guid Id,
    Guid FeeTypeId,
    string? FeeTypeName,
    decimal Amount,
    Currency Currency,
    string? Description,
    Guid? FeeInstallmentId = null,
    string? InstallmentName = null,
    string? PhysicalReceiptNumber = null);

public sealed record PaymentDto(
    Guid Id,
    string ReceiptNumber,
    Guid StudentId,
    string StudentName,
    DateTime PaymentDate,
    decimal TotalAmount,
    Currency Currency,
    PaymentStatus Status,
    string? Notes = null,
    IReadOnlyList<PaymentLineDto>? Lines = null);

/// <summary>Détail d'un paiement avec lignes (consultation / reçu / annulation).</summary>
public sealed record PaymentDetailDto(
    Guid Id,
    string ReceiptNumber,
    Guid StudentId,
    string StudentName,
    Guid AcademicYearId,
    DateTime PaymentDate,
    decimal TotalAmount,
    Currency Currency,
    PaymentStatus Status,
    string? Notes,
    IReadOnlyList<PaymentLineDto> Lines);

public sealed record PaymentSearchRequest(
    Guid? StudentId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Page = 1,
    int PageSize = 20);

public sealed record PaymentListDto
{
    public required IReadOnlyList<PaymentDto> Items { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}

public sealed record StudentFinancialSummaryDto(
    Guid StudentId,
    string StudentName,
    decimal TotalDue,
    decimal TotalPaid,
    decimal Balance,
    Currency Currency);

public sealed record CancelPaymentRequest(string Reason);

public sealed record UpdatePaymentNotesRequest(string? Notes);

/// <summary>Modification du montant du dernier versement (admin, ordre rétrograde).</summary>
public sealed record UpdatePaymentAmountRequest(decimal NewAmount, string? Notes = null);
