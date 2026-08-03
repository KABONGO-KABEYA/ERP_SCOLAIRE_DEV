using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Dashboard.DTOs;
using SchoolManagement.Application.Dashboard.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.Dashboard)]
public sealed class DashboardController : ControllerBase
{
    private readonly IPromoterDashboardService _dashboard;
    private readonly ICurrentUserService _currentUser;

    public DashboardController(IPromoterDashboardService dashboard, ICurrentUserService currentUser)
    {
        _dashboard = dashboard;
        _currentUser = currentUser;
    }

    [HttpGet("overview")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<PromoterDashboardOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview(
        [FromQuery] DashboardPeriod period = DashboardPeriod.Month,
        [FromQuery] RevenueGranularity granularity = RevenueGranularity.Daily,
        [FromQuery] Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetOverviewAsync(schoolId, period, granularity, feeTypeId, cancellationToken);
        return Ok(ApiResponse<PromoterDashboardOverviewDto>.Ok(data));
    }

    [HttpGet("summary")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<PromoterFinancialSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetSummaryAsync(schoolId, period, cancellationToken);
        return Ok(ApiResponse<PromoterFinancialSummaryDto>.Ok(data));
    }

    [HttpGet("revenue")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RevenuePointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] DashboardPeriod period = DashboardPeriod.Month,
        [FromQuery] RevenueGranularity granularity = RevenueGranularity.Daily,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetRevenueSeriesAsync(schoolId, period, granularity, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RevenuePointDto>>.Ok(data));
    }

    [HttpGet("repartition")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NamedAmountShareDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRepartition(
        [FromQuery] DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetFeeTypeRepartitionAsync(schoolId, period, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NamedAmountShareDto>>.Ok(data));
    }

    [HttpGet("distribution")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FundAllocationShareDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDistribution(
        [FromQuery] DashboardPeriod period = DashboardPeriod.Month,
        [FromQuery] Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetFundDistributionAsync(schoolId, period, feeTypeId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FundAllocationShareDto>>.Ok(data));
    }

    [HttpGet("activities")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DashboardActivityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivities(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetActivitiesAsync(schoolId, take, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DashboardActivityDto>>.Ok(data));
    }

    [HttpGet("alerts")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DashboardAlertDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetAlertsAsync(schoolId, period, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DashboardAlertDto>>.Ok(data));
    }

    [HttpGet("payments")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DashboardPaymentLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(
        [FromQuery] DashboardDetailScope scope = DashboardDetailScope.Today,
        [FromQuery] Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetPaymentsDetailAsync(schoolId, scope, feeTypeId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DashboardPaymentLineDto>>.Ok(data));
    }

    [HttpGet("revenue-detail")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RevenuePointDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueDetail(
        [FromQuery] DashboardDetailScope scope = DashboardDetailScope.Month,
        [FromQuery] Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetRevenueDetailAsync(schoolId, scope, feeTypeId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RevenuePointDto>>.Ok(data));
    }

    [HttpGet("expenses")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DashboardExpenseLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] DashboardDetailScope scope = DashboardDetailScope.Month,
        [FromQuery] Guid? destinationId = null,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetExpensesDetailAsync(schoolId, scope, destinationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DashboardExpenseLineDto>>.Ok(data));
    }

    [HttpGet("debtors")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DashboardDebtorLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDebtors(
        [FromQuery] Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetDebtorsDetailAsync(schoolId, feeTypeId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DashboardDebtorLineDto>>.Ok(data));
    }

    [HttpGet("receivables-breakdown")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<FeeReceivablesBreakdownDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceivablesBreakdown(
        [FromQuery] Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetFeeReceivablesBreakdownAsync(schoolId, feeTypeId, cancellationToken);
        return Ok(ApiResponse<FeeReceivablesBreakdownDto>.Ok(data));
    }

    [HttpGet("enrolled-students")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<EnrolledStudentsBySectionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnrolledStudents(CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetEnrolledStudentsBySectionAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<EnrolledStudentsBySectionDto>.Ok(data));
    }

    [HttpGet("fund-movements")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DashboardFundMovementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFundMovements(
        [FromQuery] Guid destinationId,
        CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var data = await _dashboard.GetFundMovementsAsync(schoolId, destinationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DashboardFundMovementDto>>.Ok(data));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
}
