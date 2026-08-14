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

public enum DashboardDetailScope
{
    Today = 0,
    Month = 1,
    Year = 2,
    Custom = 3
}

/// Centre de pilotage promoteur — payload lecture seule.
public sealed record PromoterDashboardOverviewDto(
    string SchoolName,
    string? SchoolLogoUrl,
    string Currency,
    string Period,
    DateTime GeneratedAtUtc,
    Guid? SelectedFeeTypeId,
    string SelectedFeeTypeName,
    IReadOnlyList<DashboardFeeTypeOptionDto> AvailableFeeTypes,
    PromoterKpiBoardDto Kpis,
    IReadOnlyList<RevenuePointDto> DailyRevenueLast30Days,
    IReadOnlyList<RevenuePointDto> MonthlyRevenueSchoolYear,
    PromoterExpensesBoardDto Expenses,
    IReadOnlyList<FundAllocationShareDto> FundAllocations,
    IReadOnlyList<PromoterWithholdingShareDto> Withholdings,
    PromoterSituationDto Situation,
    PromoterReceivablesDto Receivables,
    IReadOnlyList<DashboardAlertDto> Alerts,
    PromoterFinancialSummaryDto Summary,
    IReadOnlyList<RevenuePointDto> RevenueSeries,
    IReadOnlyList<NamedAmountShareDto> FeeTypeShares,
    IReadOnlyList<DashboardActivityDto> RecentActivities,
    IReadOnlyList<ClassRevenueRankDto> TopClasses,
    IReadOnlyList<NamedAmountShareDto> TopFeeTypes,
    PromoterQuickStatsDto QuickStats);

public sealed record DashboardFeeTypeOptionDto(
    Guid Id,
    string Name,
    string Currency);

public sealed record PromoterKpiBoardDto(
    PromoterMoneyKpiDto TodayRevenue,
    PromoterMoneyKpiDto MonthRevenue,
    PromoterMoneyKpiDto YearRevenue,
    PromoterStudentsKpiDto Students);

public sealed record PromoterMoneyKpiDto(
    string Label,
    decimal Amount,
    decimal ChangePercent,
    string ComparisonLabel);

public sealed record PromoterStudentsKpiDto(
    int Total,
    int Boys,
    int Girls,
    int NewThisPeriod);

public sealed record PromoterExpensesBoardDto(
    decimal Today,
    decimal Month,
    decimal Year,
    IReadOnlyList<NamedAmountShareDto> ByCategory);

public sealed record PromoterSituationDto(
    decimal TotalRevenue,
    decimal TotalExpenses,
    decimal AvailableBalance);

public sealed record PromoterReceivablesDto(
    decimal RemainingToCollect,
    int DebtorStudents,
    int FullyPaidStudents,
    decimal RecoveryPercent);

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

/// <summary>
/// Compte de répartition lié au frais suivi : solde J-1, encaissement du jour (J), dépense du jour, solde.
/// </summary>
public sealed record FundAllocationShareDto(
    Guid DestinationId,
    string Code,
    string Name,
    decimal PeriodJ1,
    decimal EncaissementJ,
    decimal DepenseJ,
    decimal Solde,
    decimal Percentage,
    string ColorHex);

/// <summary>Retenues appliquées sur le frais suivi (aujourd'hui / mois / année scolaire).</summary>
public sealed record PromoterWithholdingShareDto(
    Guid WithholdingTypeId,
    string Name,
    decimal AmountToday,
    decimal AmountMonth,
    decimal AmountYear);

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
    string Title,
    string Message,
    string? ActionHint);

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

public sealed record DashboardPaymentLineDto(
    Guid Id,
    DateTime PaymentDateUtc,
    string StudentName,
    string Reference,
    decimal Amount,
    string Currency,
    string Method);

public sealed record DashboardExpenseLineDto(
    Guid Id,
    DateOnly ExpenseDate,
    string Label,
    string Category,
    Guid DestinationId,
    string AccountTypeName,
    decimal Amount,
    string Currency,
    string Reference);

public sealed record DashboardDebtorLineDto(
    Guid StudentId,
    string StudentName,
    string ClassName,
    decimal AmountDue,
    decimal AmountPaid,
    decimal Remaining);

/// <summary>Créances du frais suivi : totaux + répartition par tranche et par compte.</summary>
public sealed record FeeReceivablesBreakdownDto(
    Guid FeeTypeId,
    string FeeTypeName,
    Guid AcademicYearId,
    string AcademicYearLabel,
    string Currency,
    decimal TotalExpected,
    decimal TotalPaid,
    decimal TotalRemaining,
    IReadOnlyList<FeeInstallmentReceivableDto> ByInstallment,
    IReadOnlyList<FeeDestinationReceivableDto> ByDestination,
    IReadOnlyList<DashboardDebtorLineDto> Debtors);

public sealed record FeeInstallmentReceivableDto(
    Guid FeeInstallmentId,
    string InstallmentName,
    int SortOrder,
    decimal AmountExpected,
    decimal AmountPaid,
    decimal Remaining);

public sealed record FeeDestinationReceivableDto(
    Guid DestinationId,
    string DestinationCode,
    string DestinationName,
    decimal Percentage,
    decimal AmountExpected,
    decimal AmountCollected,
    decimal Remaining);

public sealed record DashboardFundMovementDto(
    Guid Id,
    DateTime AllocatedAtUtc,
    string DestinationName,
    decimal Amount,
    string Currency,
    string? Note);

public sealed record EnrolledStudentsBySectionDto(
    int TotalStudents,
    int TotalBoys,
    int TotalGirls,
    IReadOnlyList<EnrolledSectionGroupDto> Sections);

public sealed record EnrolledSectionGroupDto(
    Guid SectionId,
    string SectionName,
    int TotalStudents,
    int Boys,
    int Girls,
    IReadOnlyList<EnrolledClassRowDto> Classes);

public sealed record EnrolledClassRowDto(
    Guid ClassRoomId,
    string ClassName,
    int TotalStudents,
    int Boys,
    int Girls);
