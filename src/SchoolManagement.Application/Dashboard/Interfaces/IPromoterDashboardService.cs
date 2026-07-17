namespace SchoolManagement.Application.Dashboard.Interfaces;

using SchoolManagement.Application.Dashboard.DTOs;

public interface IPromoterDashboardService
{
    Task<PromoterDashboardOverviewDto> GetOverviewAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        RevenueGranularity granularity = RevenueGranularity.Daily,
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
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardActivityDto>> GetActivitiesAsync(
        Guid schoolId,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardAlertDto>> GetAlertsAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default);
}
