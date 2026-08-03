using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.ResultValidation.DTOs;
using SchoolManagement.Application.ResultValidation.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.ResultValidation)]
public class ResultValidationController : ControllerBase
{
    private readonly IResultValidationService _service;
    private readonly ICurrentUserService _currentUser;

    public ResultValidationController(
        IResultValidationService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("sheet")]
    [Authorize(Policy = Permissions.ResultsValidationRead)]
    [ProducesResponseType(typeof(ApiResponse<ResultValidationSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSheet(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var sheet = await _service.GetSheetAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
        return Ok(ApiResponse<ResultValidationSheetDto>.Ok(sheet));
    }

    [HttpGet("readiness")]
    [Authorize(Policy = Permissions.ResultsValidationRead)]
    [ProducesResponseType(typeof(ApiResponse<ResultValidationReadinessDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReadiness(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var report = await _service.GetReadinessReportAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
        return Ok(ApiResponse<ResultValidationReadinessDto>.Ok(report));
    }

    [HttpPost("validate")]
    [Authorize(Policy = Permissions.ResultsValidationValidate)]
    [ProducesResponseType(typeof(ApiResponse<ResultValidationSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(
        [FromBody] ResultValidationActionRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var sheet = await _service.ValidateAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<ResultValidationSheetDto>.Ok(sheet, "Résultats validés."));
    }

    [HttpPost("cancel")]
    [Authorize(Policy = Permissions.ResultsValidationValidate)]
    [ProducesResponseType(typeof(ApiResponse<ResultValidationSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(
        [FromBody] ResultValidationActionRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var sheet = await _service.CancelValidationAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<ResultValidationSheetDto>.Ok(sheet, "Validation annulée."));
    }

    [HttpPost("lock")]
    [Authorize(Policy = Permissions.ResultsValidationLock)]
    [ProducesResponseType(typeof(ApiResponse<ResultValidationSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Lock(
        [FromBody] ResultValidationActionRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var sheet = await _service.LockAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<ResultValidationSheetDto>.Ok(sheet, "Résultats verrouillés."));
    }

    [HttpPost("unlock")]
    [Authorize(Policy = Permissions.ResultsValidationUnlock)]
    [ProducesResponseType(typeof(ApiResponse<ResultValidationSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unlock(
        [FromBody] ResultValidationActionRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var sheet = await _service.UnlockAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<ResultValidationSheetDto>.Ok(sheet, "Résultats déverrouillés."));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
}
