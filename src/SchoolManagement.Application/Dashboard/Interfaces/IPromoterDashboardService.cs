namespace SchoolManagement.Application.Dashboard.Interfaces;

using SchoolManagement.Application.Dashboard.DTOs;

public interface IPromoterDashboardService
{
    Task<PromoterDashboardOverviewDto> GetOverviewAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        RevenueGranularity granularity = RevenueGranularity.Daily,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    Task<PromoterFinancialSummaryDto> GetSummaryAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenuePointDto>> GetRevenueSeriesAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        RevenueGranularity granularity = RevenueGranularity.Daily,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NamedAmountShareDto>> GetFeeTypeRepartitionAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FundAllocationShareDto>> GetFundDistributionAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardActivityDto>> GetActivitiesAsync(
        Guid schoolId,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardAlertDto>> GetAlertsAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardPaymentLineDto>> GetPaymentsDetailAsync(
        Guid schoolId,
        DashboardDetailScope scope,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenuePointDto>> GetRevenueDetailAsync(
        Guid schoolId,
        DashboardDetailScope scope,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardExpenseLineDto>> GetExpensesDetailAsync(
        Guid schoolId,
        DashboardDetailScope scope,
        Guid? destinationId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardDebtorLineDto>> GetDebtorsDetailAsync(
        Guid schoolId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    Task<FeeReceivablesBreakdownDto> GetFeeReceivablesBreakdownAsync(
        Guid schoolId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    Task<EnrolledStudentsBySectionDto> GetEnrolledStudentsBySectionAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardFundMovementDto>> GetFundMovementsAsync(
        Guid schoolId,
        Guid destinationId,
        CancellationToken cancellationToken = default);
}
