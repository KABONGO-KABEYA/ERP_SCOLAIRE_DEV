namespace SchoolManagement.Application.Payments.DTOs;

using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Domain.Enums;

/// <summary>Relevé de frais scolaire — document officiel remis après encaissement.</summary>
public sealed record FeeTypeStatementDto(
    Guid PaymentId,
    string ReceiptNumber,
    string StatementNumber,
    DateTime PaymentDate,
    DateTime EditedAt,
    Guid StudentId,
    string StudentName,
    string StudentLastName,
    string? StudentMiddleName,
    string StudentFirstName,
    string? StudentRegistrationNumber,
    string ClassName,
    string? ParentName,
    string? ParentPhone,
    string? CashierName,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid FeeTypeId,
    string FeeTypeName,
    Currency Currency,
    string? FeeCurrencyCode,
    string? PaymentCurrencyCode,
    decimal? FeeCurrencyAmount,
    decimal? PaymentCurrencyAmount,
    decimal? AppliedExchangeRate,
    string SchoolName,
    string? SchoolMotto,
    string? SchoolAddress,
    string? SchoolPhone,
    string? SchoolEmail,
    DocumentPrintBrandingDto Branding,
    IReadOnlyList<FeeTypeStatementPaymentHistoryLineDto> PaymentHistory,
    IReadOnlyList<FeeTypeStatementInstallmentLineDto> InstallmentSituations,
    decimal TotalExpected,
    decimal TotalPaid,
    decimal TotalRemaining);

public sealed record FeeTypeStatementPaymentHistoryLineDto(
    int Number,
    string InstallmentName,
    DateTime PaymentDate,
    decimal AmountPaid,
    string ReceiptNumber);

public sealed record FeeTypeStatementInstallmentLineDto(
    int Number,
    string InstallmentName,
    decimal AmountExpected,
    decimal AmountPaid,
    decimal Remaining);
