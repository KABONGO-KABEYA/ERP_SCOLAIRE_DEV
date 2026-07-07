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
