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
    DateTime? PaymentDate = null,
    /// <summary>Devise de paiement (référentiel). Si null, = devise du frais / Currency enum.</summary>
    Guid? PaymentCurrencyId = null,
    /// <summary>Devise du frais (référentiel). Si null, dérivée de Currency / lignes.</summary>
    Guid? FeeCurrencyId = null,
    /// <summary>Taux forcé (nécessite permission payment-fx.update côté API).</summary>
    decimal? OverrideExchangeRate = null);

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
    IReadOnlyList<PaymentLineDto>? Lines = null,
    DateTime? CreatedAt = null,
    Guid? FeeCurrencyId = null,
    Guid? PaymentCurrencyId = null,
    Guid? ExchangeRateId = null,
    decimal? FeeCurrencyAmount = null,
    decimal? PaymentCurrencyAmount = null,
    decimal? AppliedExchangeRate = null,
    string? FeeCurrencyCode = null,
    string? PaymentCurrencyCode = null);

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
    IReadOnlyList<PaymentLineDto> Lines,
    DateTime? CreatedAt = null,
    Guid? FeeCurrencyId = null,
    Guid? PaymentCurrencyId = null,
    Guid? ExchangeRateId = null,
    decimal? FeeCurrencyAmount = null,
    decimal? PaymentCurrencyAmount = null,
    decimal? AppliedExchangeRate = null,
    string? FeeCurrencyCode = null,
    string? PaymentCurrencyCode = null);

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

/// <summary>
/// Verrou rétrograde global : dernier encaissement Complet du type de frais
/// (tous élèves confondus) pour l'année scolaire.
/// </summary>
public sealed record PaymentMutationGateDto(
    Guid? LatestPaymentId,
    DateTime? LatestPaymentDate,
    Guid? LatestStudentId,
    string? LatestStudentName,
    string? LatestReceiptNumber);

public sealed record CancelPaymentRequest(string Reason);

public sealed record UpdatePaymentNotesRequest(string? Notes);

/// <summary>Modification du montant / n° physique du dernier versement (admin, ordre rétrograde).</summary>
public sealed record UpdatePaymentLineAmountRequest(
    Guid LineId,
    decimal Amount,
    string? PhysicalReceiptNumber = null);

public sealed record UpdatePaymentAmountRequest(
    decimal NewAmount,
    string? Notes = null,
    string? PhysicalReceiptNumber = null,
    IReadOnlyList<UpdatePaymentLineAmountRequest>? Lines = null);
