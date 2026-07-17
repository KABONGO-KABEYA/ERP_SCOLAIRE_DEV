namespace SchoolManagement.Application.Dashboard.DTOs;

public enum DashboardPeriod
{
    Today = 0,
    Week = 1,
    Month = 2,
    Year = 3
}

public enum RevenueGranularity
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2
}

public sealed record PromoterDashboardOverviewDto(
    string SchoolName,
    string Currency,
    string Period,
    DateTime GeneratedAtUtc,
    PromoterFinancialSummaryDto Summary,
    IReadOnlyList<RevenuePointDto> RevenueSeries,
    IReadOnlyList<NamedAmountShareDto> FeeTypeShares,
    IReadOnlyList<FundAllocationShareDto> FundAllocations,
    IReadOnlyList<DashboardActivityDto> RecentActivities,
    IReadOnlyList<DashboardAlertDto> Alerts,
    IReadOnlyList<ClassRevenueRankDto> TopClasses,
    IReadOnlyList<NamedAmountShareDto> TopFeeTypes,
    PromoterQuickStatsDto QuickStats);

public sealed record PromoterFinancialSummaryDto(
    string PeriodRevenueLabel,
    decimal PeriodRevenue,
    decimal PeriodRevenueChangePercent,
    string SecondaryRevenueLabel,
    decimal SecondaryRevenue,
    decimal SecondaryRevenueChangePercent,
    int NewEnrollments,
    int ActiveStudents,
    decimal RealizationRate,
    decimal ExpectedRevenue,
    decimal CollectedRevenue);

public sealed record RevenuePointDto(
    string Label,
    DateTime PeriodStartUtc,
    decimal Amount);

public sealed record NamedAmountShareDto(
    string Name,
    decimal Amount,
    decimal Percentage,
    string ColorHex);

public sealed record FundAllocationShareDto(
    Guid DestinationId,
    string Name,
    decimal Amount,
    decimal Percentage);

public sealed record DashboardActivityDto(
    DateTime OccurredAtUtc,
    string Kind,
    string Title,
    string Subtitle,
    decimal? Amount,
    string? Currency);

public sealed record DashboardAlertDto(
    string Severity,
    string Code,
    string Message);

public sealed record ClassRevenueRankDto(
    int Rank,
    string ClassName,
    decimal Amount);

public sealed record PromoterQuickStatsDto(
    int PresentStudents,
    int AbsentStudents,
    int PaymentsToday,
    int ReceiptsPrinted,
    decimal RemainingToCollect,
    decimal TotalAllocated);
