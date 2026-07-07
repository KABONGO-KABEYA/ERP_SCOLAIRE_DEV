using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Application.Payments.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Payments}")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ICurrentUserService _currentUser;

    public PaymentsController(IPaymentService paymentService, ICurrentUserService currentUser)
    {
        _paymentService = paymentService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<PaymentListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] PaymentSearchRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _paymentService.SearchAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<PaymentListDto>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var payment = await _paymentService.GetByIdAsync(schoolId, id, cancellationToken);
        return payment is null ? NotFound() : Ok(ApiResponse<PaymentDto>.Ok(payment));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.PaymentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var payment = await _paymentService.CreatePaymentAsync(schoolId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = payment.Id }, ApiResponse<PaymentDto>.Ok(payment, "Paiement enregistré."));
    }

    [HttpGet("student/{studentId:guid}/summary")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<StudentFinancialSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentSummary(
        Guid studentId,
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var summary = await _paymentService.GetStudentFinancialSummaryAsync(schoolId, studentId, academicYearId, cancellationToken);
        return summary is null ? NotFound() : Ok(ApiResponse<StudentFinancialSummaryDto>.Ok(summary));
    }
}
