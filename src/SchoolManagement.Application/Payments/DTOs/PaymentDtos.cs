namespace SchoolManagement.Application.Payments.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record PaymentLineRequest(Guid FeeTypeId, decimal Amount, Currency Currency, string? Description);

public sealed record CreatePaymentRequest(
    Guid StudentId,
    Guid AcademicYearId,
    Guid CashRegisterId,
    Guid? BankId,
    Currency Currency,
    string PaymentMethod,
    string? Notes,
    IReadOnlyList<PaymentLineRequest> Lines);

public sealed record PaymentDto(
    Guid Id,
    string ReceiptNumber,
    Guid StudentId,
    string StudentName,
    DateTime PaymentDate,
    decimal TotalAmount,
    Currency Currency,
    PaymentStatus Status,
    string PaymentMethod);

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
