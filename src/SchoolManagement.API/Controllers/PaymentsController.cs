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
    private readonly IFeeTypeStatementService _statementService;
    private readonly ICurrentUserService _currentUser;

    public PaymentsController(
        IPaymentService paymentService,
        IFeeTypeStatementService statementService,
        ICurrentUserService currentUser)
    {
        _paymentService = paymentService;
        _statementService = statementService;
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
    [ProducesResponseType(typeof(ApiResponse<PaymentDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var payment = await _paymentService.GetByIdAsync(schoolId, id, cancellationToken);
        return payment is null ? NotFound() : Ok(ApiResponse<PaymentDetailDto>.Ok(payment));
    }

    [HttpGet("fee-type-statement")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<FeeTypeStatementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeeTypeStatementForStudent(
        [FromQuery] Guid studentId,
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid feeTypeId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var statement = await _statementService.GetStatementForStudentAsync(
            schoolId, studentId, academicYearId, feeTypeId, cancellationToken);
        return Ok(ApiResponse<FeeTypeStatementDto>.Ok(statement));
    }

    [HttpGet("fee-type-statement/pdf")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportFeeTypeStatementPdfForStudent(
        [FromQuery] Guid studentId,
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid feeTypeId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var bytes = await _statementService.ExportPdfForStudentAsync(
            schoolId, studentId, academicYearId, feeTypeId, cancellationToken);
        return File(bytes, "application/pdf", $"releve-{feeTypeId:N}-{studentId:N}.pdf");
    }

    [HttpGet("{id:guid}/fee-type-statement")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<FeeTypeStatementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeeTypeStatement(
        Guid id,
        [FromQuery] Guid? feeTypeId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var statement = await _statementService.GetStatementAsync(schoolId, id, feeTypeId, cancellationToken);
        return Ok(ApiResponse<FeeTypeStatementDto>.Ok(statement));
    }

    [HttpGet("{id:guid}/fee-type-statement/pdf")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportFeeTypeStatementPdf(
        Guid id,
        [FromQuery] Guid? feeTypeId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var bytes = await _statementService.ExportPdfAsync(schoolId, id, feeTypeId, cancellationToken);
        return File(bytes, "application/pdf", $"releve-frais-{id:N}.pdf");
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

    [HttpGet("mutation-gate")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<PaymentMutationGateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMutationGate(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid feeTypeId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var gate = await _paymentService.GetMutationGateAsync(
            schoolId, academicYearId, feeTypeId, cancellationToken);
        return Ok(ApiResponse<PaymentMutationGateDto>.Ok(gate));
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

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        await _paymentService.CancelPaymentAsync(schoolId, userId, id, request.Reason, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Paiement annulé."));
    }

    [HttpPut("{id:guid}/notes")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<PaymentDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNotes(
        Guid id,
        [FromBody] UpdatePaymentNotesRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var payment = await _paymentService.UpdatePaymentNotesAsync(schoolId, id, request.Notes, cancellationToken);
        return Ok(ApiResponse<PaymentDetailDto>.Ok(payment, "Notes mises à jour."));
    }

    [HttpPut("{id:guid}/amount")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<PaymentDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAmount(
        Guid id,
        [FromBody] UpdatePaymentAmountRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var payment = await _paymentService.UpdatePaymentAmountAsync(schoolId, userId, id, request, cancellationToken);
        return Ok(ApiResponse<PaymentDetailDto>.Ok(payment, "Montant du versement mis à jour."));
    }
}
