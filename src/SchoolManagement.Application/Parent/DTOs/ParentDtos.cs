namespace SchoolManagement.Application.Parent.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record ParentChildDto(
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    string? ClassName,
    string? PhotoUrl = null,
    string? SchoolName = null);

public sealed record ParentPaymentDto(
    Guid Id,
    string ReceiptNumber,
    DateTime PaymentDate,
    decimal TotalAmount,
    Currency Currency,
    PaymentStatus Status,
    string? FeeTypeLabel = null,
    Guid? FeeTypeId = null,
    Guid? AcademicYearId = null);

public sealed record ParentPaymentSummaryDto(
    decimal TotalDue,
    decimal TotalPaid,
    decimal Balance,
    string CurrencyLabel,
    int Currency);

public sealed record ParentFeeInstallmentSituationDto(
    int Number,
    string InstallmentName,
    decimal AmountExpected,
    decimal AmountPaid,
    decimal Remaining);

public sealed record ParentFeeTypeSituationDto(
    Guid FeeTypeId,
    string FeeTypeName,
    Currency Currency,
    string CurrencyLabel,
    decimal AmountExpected,
    decimal AmountPaid,
    decimal Balance,
    bool IsInOrder,
    IReadOnlyList<ParentFeeInstallmentSituationDto> Installments);

public sealed record ParentFeeSituationsResultDto(
    Guid AcademicYearId,
    string AcademicYearLabel,
    string CurrencyLabel,
    decimal TotalExpected,
    decimal TotalPaid,
    decimal TotalBalance,
    IReadOnlyList<ParentFeeTypeSituationDto> FeeTypes);

public sealed record ParentBulletinSummaryDto(
    Guid AcademicPeriodId,
    string PeriodName,
    decimal Average,
    decimal Percentage,
    int Rank,
    int ClassSize,
    bool IsPublished,
    string? Mention = null,
    string? Decision = null,
    string? Appreciation = null);

public sealed record ParentGradeItemDto(
    string Label,
    decimal Score,
    decimal MaxScore,
    DateTime? Date,
    string? EvaluationType = null);

public sealed record ParentGradeSubjectDto(
    string Name,
    decimal Average,
    decimal MaxScore,
    IReadOnlyList<ParentGradeItemDto> Interrogations,
    IReadOnlyList<ParentGradeItemDto> Exams,
    IReadOnlyList<ParentGradeItemDto> Works);

public sealed record ParentGradesOverviewDto(
    decimal GeneralAverage,
    int Rank,
    int ClassSize,
    IReadOnlyList<double> Evolution,
    IReadOnlyList<ParentGradeSubjectDto> Subjects);

public sealed record ParentAttendanceDayDto(
    DateOnly Date,
    string Status,
    string? Note = null);

public sealed record ParentCommunicationAttachmentDto(
    string Name,
    string Type,
    string? Url = null);

public sealed record ParentCommunicationDto(
    Guid Id,
    string Title,
    string Type,
    DateTime Date,
    string? Body = null,
    bool IsRead = false,
    IReadOnlyList<ParentCommunicationAttachmentDto>? Attachments = null);

