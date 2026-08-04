using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Parent;
using SchoolManagement.Application.Parent.DTOs;
using SchoolManagement.Application.Parent.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/parent")]
public class ParentController : ControllerBase
{
    private readonly IParentService _parentService;
    private readonly IUserAccountRepository _userRepository;
    private readonly ICurrentUserService _currentUser;

    public ParentController(
        IParentService parentService,
        IUserAccountRepository userRepository,
        ICurrentUserService currentUser)
    {
        _parentService = parentService;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    [HttpGet("children")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentChildDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildren(CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var children = await _parentService.GetMyChildrenAsync(schoolId, guardianId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentChildDto>>.Ok(children));
    }

    [HttpGet("children/{studentId:guid}/payments")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentPaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildPayments(Guid studentId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var payments = await _parentService.GetChildPaymentsAsync(
            schoolId,
            guardianId,
            studentId,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentPaymentDto>>.Ok(payments));
    }

    [HttpGet("children/{studentId:guid}/payment-summary")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<ParentPaymentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildPaymentSummary(Guid studentId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var summary = await _parentService.GetChildPaymentSummaryAsync(
            schoolId,
            guardianId,
            studentId,
            cancellationToken);
        return Ok(ApiResponse<ParentPaymentSummaryDto>.Ok(summary));
    }

    [HttpGet("children/{studentId:guid}/fee-situations")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<ParentFeeSituationsResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildFeeSituations(
        Guid studentId,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var situations = await _parentService.GetChildFeeSituationsAsync(
            schoolId,
            guardianId,
            studentId,
            academicYearId,
            cancellationToken);
        return Ok(ApiResponse<ParentFeeSituationsResultDto>.Ok(situations));
    }

    [HttpGet("children/{studentId:guid}/grades")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<ParentGradesOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildGrades(Guid studentId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var grades = await _parentService.GetChildGradesAsync(
            schoolId,
            guardianId,
            studentId,
            cancellationToken);
        return Ok(ApiResponse<ParentGradesOverviewDto>.Ok(grades));
    }

    [HttpGet("children/{studentId:guid}/bulletins/{academicPeriodId:guid}/pdf")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportChildBulletinPdf(
        Guid studentId,
        Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var bytes = await _parentService.ExportChildBulletinPdfAsync(
            schoolId,
            guardianId,
            studentId,
            academicPeriodId,
            cancellationToken);
        return File(bytes, "application/pdf", $"bulletin-{academicPeriodId:N}.pdf");
    }

    [HttpGet("children/{studentId:guid}/photo")]
    [Authorize(Policy = Permissions.ReportsRead)]
    public async Task<IActionResult> GetChildPhoto(Guid studentId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var photo = await _parentService.OpenChildPhotoAsync(
            schoolId,
            guardianId,
            studentId,
            cancellationToken);
        if (photo is null)
        {
            return NotFound();
        }

        return File(photo.Value.Stream, photo.Value.MimeType, photo.Value.FileName);
    }

    [HttpGet("payments/{paymentId:guid}/receipt/pdf")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPaymentReceiptPdf(
        Guid paymentId,
        [FromQuery] Guid? feeTypeId,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var bytes = await _parentService.ExportChildPaymentReceiptPdfAsync(
            schoolId,
            guardianId,
            paymentId,
            feeTypeId,
            cancellationToken);
        return File(bytes, "application/pdf", $"recu-{paymentId:N}.pdf");
    }

    [HttpGet("children/{studentId:guid}/bulletins")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentBulletinSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildBulletins(Guid studentId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var bulletins = await _parentService.GetChildBulletinsAsync(
            schoolId,
            guardianId,
            studentId,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentBulletinSummaryDto>>.Ok(bulletins));
    }

    [HttpGet("children/{studentId:guid}/attendance")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentAttendanceDayDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildAttendance(Guid studentId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var attendance = await _parentService.GetChildAttendanceAsync(
            schoolId,
            guardianId,
            studentId,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentAttendanceDayDto>>.Ok(attendance));
    }

    [HttpGet("children/{studentId:guid}/communications")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentCommunicationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildCommunications(Guid studentId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var communications = await _parentService.GetChildCommunicationsAsync(
            schoolId,
            guardianId,
            studentId,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentCommunicationDto>>.Ok(communications));
    }

    private Guid RequireSchoolId() => ParentApiSchoolContext.RequireSchoolId(_currentUser);

    private async Task<Guid> ResolveGuardianIdAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException();

        if (user.GuardianId is null)
        {
            throw new UnauthorizedAccessException("Ce compte n'est pas lié à un tuteur.");
        }

        if (user.SchoolId != RequireSchoolId())
        {
            throw new UnauthorizedAccessException("Compte hors contexte école.");
        }

        return user.GuardianId.Value;
    }
}
