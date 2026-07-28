using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Accounting.DTOs;
using SchoolManagement.Application.Accounting.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.Accounting)]
public sealed class AccountingController : ControllerBase
{
    private readonly IAccountingService _service;
    private readonly ICurrentUserService _currentUser;

    public AccountingController(IAccountingService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("expense-requests")]
    [Authorize(Policy = Permissions.AccountingRead)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseRequestSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchExpenseRequests(
        [FromQuery] ExpenseSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SearchExpenseRequestsAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<ExpenseRequestSearchResultDto>.Ok(result));
    }

    [HttpPost("expense-requests")]
    [Authorize(Policy = Permissions.AccountingManage)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateExpenseRequest(
        [FromBody] CreateExpenseRequestRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var result = await _service.CreateExpenseRequestAsync(RequireSchoolId(), request, userId, cancellationToken);
        return Ok(ApiResponse<ExpenseRequestDto>.Ok(result, "Demande de paiement créée."));
    }

    [HttpPost("expense-requests/{id:guid}/submit")]
    [Authorize(Policy = Permissions.AccountingManage)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitExpenseRequest(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.SubmitExpenseRequestAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<ExpenseRequestDto>.Ok(result, "Demande soumise."));
    }

    [HttpPost("expense-requests/{id:guid}/approve")]
    [Authorize(Policy = Permissions.AccountingManage)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveExpenseRequest(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var result = await _service.ApproveExpenseRequestAsync(RequireSchoolId(), id, userId, cancellationToken);
        return Ok(ApiResponse<ExpenseRequestDto>.Ok(result, "Demande approuvée."));
    }

    [HttpGet("expense-payments")]
    [Authorize(Policy = Permissions.AccountingRead)]
    [ProducesResponseType(typeof(ApiResponse<ExpensePaymentSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchExpensePayments(
        [FromQuery] ExpenseSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SearchExpensePaymentsAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<ExpensePaymentSearchResultDto>.Ok(result));
    }

    [HttpPost("expense-payments")]
    [Authorize(Policy = Permissions.AccountingManage)]
    [ProducesResponseType(typeof(ApiResponse<ExpensePaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateExpensePayment(
        [FromBody] CreateExpensePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var result = await _service.CreateExpensePaymentAsync(RequireSchoolId(), request, userId, cancellationToken);
        return Ok(ApiResponse<ExpensePaymentDto>.Ok(result, "Dépense enregistrée."));
    }

    [HttpGet("expense-payments/{id:guid}")]
    [Authorize(Policy = Permissions.AccountingRead)]
    [ProducesResponseType(typeof(ApiResponse<ExpensePaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpensePayment(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetExpensePaymentByIdAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<ExpensePaymentDto>.Ok(result));
    }

    [HttpGet("expense-balances")]
    [Authorize(Policy = Permissions.AccountingRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExpenseDestinationBalanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseBalances(
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetExpenseBalancesAsync(RequireSchoolId(), academicYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ExpenseDestinationBalanceDto>>.Ok(result));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
}
