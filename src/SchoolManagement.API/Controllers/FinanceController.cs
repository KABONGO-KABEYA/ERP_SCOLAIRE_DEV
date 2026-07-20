using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.Finance.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.Finance)]
public sealed class FinanceController : ControllerBase
{
    private readonly IFinanceOperationService _financeService;
    private readonly ICurrentUserService _currentUser;

    public FinanceController(IFinanceOperationService financeService, ICurrentUserService currentUser)
    {
        _financeService = financeService;
        _currentUser = currentUser;
    }

    [HttpGet("payment-situations")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<StudentPaymentSituationSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPaymentSituations(
        [FromQuery] StudentPaymentSituationSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _financeService.SearchPaymentSituationsAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<StudentPaymentSituationSearchResultDto>.Ok(result));
    }

    [HttpGet("payment-situations/{enrollmentId:guid}/installment-plan")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<StudentInstallmentPaymentPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstallmentPaymentPlan(
        Guid enrollmentId,
        [FromQuery] Guid feeTypeId,
        CancellationToken cancellationToken)
    {
        var result = await _financeService.GetInstallmentPaymentPlanAsync(
            RequireSchoolId(),
            enrollmentId,
            feeTypeId,
            cancellationToken);
        return Ok(ApiResponse<StudentInstallmentPaymentPlanDto>.Ok(result));
    }

    [HttpGet("pricing-assignments")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<StudentPricingAssignmentSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPricingAssignments(
        [FromQuery] StudentPricingAssignmentSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _financeService.SearchPricingAssignmentsAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<StudentPricingAssignmentSearchResultDto>.Ok(result));
    }

    [HttpPut("pricing-assignments/{enrollmentId:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<StudentPricingAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePricingAssignment(
        Guid enrollmentId,
        [FromBody] UpdateEnrollmentPricingCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _financeService.UpdateEnrollmentPricingCategoryAsync(
            RequireSchoolId(),
            enrollmentId,
            request,
            cancellationToken);
        return Ok(ApiResponse<StudentPricingAssignmentDto>.Ok(item, "Catégorie tarifaire mise à jour."));
    }

    [HttpGet("pricing-assignments/{enrollmentId:guid}/history")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PricingCategoryHistoryLineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPricingAssignmentHistory(
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        var items = await _financeService.GetPricingCategoryHistoryAsync(
            RequireSchoolId(), enrollmentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PricingCategoryHistoryLineDto>>.Ok(items));
    }

    [HttpGet("pricing-assignments/{enrollmentId:guid}/applicable-fees")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<StudentApplicableFeesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApplicableFees(
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        var item = await _financeService.GetApplicableFeesAsync(
            RequireSchoolId(), enrollmentId, cancellationToken);
        return Ok(ApiResponse<StudentApplicableFeesDto>.Ok(item));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
}
