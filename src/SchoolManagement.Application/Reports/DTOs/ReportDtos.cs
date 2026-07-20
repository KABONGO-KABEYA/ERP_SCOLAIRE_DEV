namespace SchoolManagement.Application.Reports.DTOs;

public sealed record DashboardStatsDto(
    int TotalStudents,
    int ActiveEnrollments,
    int TotalClassRooms,
    int TotalTeachers,
    decimal TotalPaymentsAmount,
    int PaymentCount);

public sealed record EnrollmentByClassDto(
    Guid ClassRoomId,
    string ClassCode,
    string ClassName,
    string SectionName,
    int TotalStudents,
    int MaleCount,
    int FemaleCount);

public sealed record ClassAverageReportDto(
    Guid ClassRoomId,
    string ClassName,
    string PeriodName,
    int StudentCount,
    decimal ClassAverage,
    decimal MaxAverage,
    decimal MinAverage,
    int PassCount,
    int FailCount);

public sealed record FinancialSummaryDto(
    decimal TotalCollected,
    int PaymentCount,
    int DebtorCount,
    int UpToDateCount,
    int PartialCount);

/// <summary>Période prédéfinie pour les rapports de recettes réalisées.</summary>
public enum RealizedReceiptsPeriodKind
{
    Day = 0,
    Week = 1,
    Month = 2,
    Custom = 3
}

public sealed record RealizedReceiptsRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? AcademicYearId = null,
    Guid? FeeTypeId = null,
    Guid? ClassRoomId = null,
    Guid? SectionId = null,
    int Page = 1,
    int PageSize = 500);

public sealed record RealizedReceiptLineDto(
    Guid PaymentId,
    string ReceiptNumber,
    Guid StudentId,
    string StudentName,
    string? ClassName,
    DateTime PaymentDate,
    decimal TotalAmount,
    string Currency,
    string? FeeTypesSummary,
    string? Notes);

public sealed record RealizedReceiptsDailyBucketDto(
    DateOnly Date,
    decimal TotalAmount,
    int PaymentCount);

public sealed record RealizedReceiptsByCurrencyDto(
    string Currency,
    decimal TotalAmount,
    int PaymentCount);

public sealed record RealizedReceiptsByClassDto(
    Guid? ClassRoomId,
    string ClassCode,
    string ClassName,
    string SectionName,
    decimal TotalAmount,
    int PaymentCount);

public sealed record RealizedReceiptsByFeeTypeDto(
    Guid FeeTypeId,
    string FeeTypeName,
    string Currency,
    decimal TotalAmount,
    int PaymentCount);

public sealed record RealizedReceiptsDailyByClassDto(
    DateOnly Date,
    Guid? ClassRoomId,
    string ClassName,
    decimal TotalAmount,
    int PaymentCount);

public sealed record RealizedReceiptsDailyByFeeTypeDto(
    DateOnly Date,
    Guid FeeTypeId,
    string FeeTypeName,
    string Currency,
    decimal TotalAmount,
    int PaymentCount);

public sealed record RealizedReceiptsBySectionDto(
    Guid? SectionId,
    string SectionCode,
    string SectionName,
    decimal TotalAmount,
    int PaymentCount);

public sealed record RealizedReceiptsDailyBySectionDto(
    DateOnly Date,
    Guid? SectionId,
    string SectionName,
    decimal TotalAmount,
    int PaymentCount);

/// <summary>Colonne dynamique de tranche pour le tableau croisé des recettes.</summary>
public sealed record RealizedReceiptsInstallmentColumnDto(
    Guid FeeInstallmentId,
    string InstallmentName,
    int SortOrder);

/// <summary>Ligne pivot : élève × classe × montants par tranche.</summary>
public sealed record RealizedReceiptsPivotRowDto(
    Guid StudentId,
    string StudentName,
    string ClassName,
    IReadOnlyList<decimal> InstallmentAmounts,
    decimal RowTotal);

/// <summary>Ligne pivot journalière : date × élève × classe × détails par tranche (montant + reçu).</summary>
public sealed record RealizedReceiptsDailyPivotRowDto(
    DateOnly Date,
    Guid StudentId,
    string StudentName,
    string ClassName,
    IReadOnlyList<string> InstallmentDetails,
    decimal RowTotal);

public sealed record RealizedReceiptsResultDto(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<RealizedReceiptLineDto> Items,
    IReadOnlyList<RealizedReceiptsInstallmentColumnDto> InstallmentColumns,
    IReadOnlyList<RealizedReceiptsPivotRowDto> PivotRows,
    IReadOnlyList<RealizedReceiptsDailyPivotRowDto> DailyPivotRows,
    IReadOnlyList<RealizedReceiptsDailyBucketDto> DailyBuckets,
    IReadOnlyList<RealizedReceiptsByCurrencyDto> ByCurrency,
    IReadOnlyList<RealizedReceiptsByClassDto> ByClass,
    IReadOnlyList<RealizedReceiptsByFeeTypeDto> ByFeeType,
    IReadOnlyList<RealizedReceiptsBySectionDto> BySection,
    IReadOnlyList<RealizedReceiptsDailyByClassDto> DailyByClass,
    IReadOnlyList<RealizedReceiptsDailyByFeeTypeDto> DailyByFeeType,
    IReadOnlyList<RealizedReceiptsDailyBySectionDto> DailyBySection,
    decimal GrandTotal,
    int PaymentCount,
    int TotalCount);
