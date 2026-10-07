namespace SchoolManagement.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.ControllerControl.DTOs;
using SchoolManagement.Application.ControllerControl.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

[ApiController]
[Authorize]
[Route(ApiRoutes.ControllerControl)]
public sealed class ControllerControlController : ControllerBase
{
    private readonly IControllerControlService _service;
    private readonly ICurrentUserService _currentUser;

    public ControllerControlController(IControllerControlService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("setup")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> Setup([FromQuery] Guid? academicYearId, CancellationToken cancellationToken)
    {
        var data = await _service.GetSetupAsync(RequireSchoolId(), academicYearId, cancellationToken);
        return Ok(ApiResponse<ControllerSetupDto>.Ok(data));
    }

    [HttpGet("installments")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    public async Task<IActionResult> Installments(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid feeTypeId,
        CancellationToken cancellationToken)
    {
        var data = await _service.GetInstallmentsAsync(RequireSchoolId(), academicYearId, feeTypeId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ControllerInstallmentDto>>.Ok(data));
    }

    [HttpGet("students/search")]
    [Authorize(Policy = Permissions.StudentsRead)]
    public async Task<IActionResult> SearchStudents(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid feeTypeId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var data = await _service.SearchStudentsAsync(RequireSchoolId(), academicYearId, feeTypeId, search, cancellationToken);
        return Ok(ApiResponse<ControllerStudentSearchResultDto>.Ok(data));
    }

    [HttpPost("check/qr")]
    [Authorize(Policy = Permissions.StudentCardsRead)]
    [Authorize(Policy = Permissions.PaymentsRead)]
    public async Task<IActionResult> CheckQr([FromBody] ControllerCheckByQrRequest request, CancellationToken cancellationToken)
    {
        var data = await _service.CheckByQrAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<ControllerCheckResultDto>.Ok(data));
    }

    [HttpPost("check/student")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [Authorize(Policy = Permissions.PaymentsRead)]
    public async Task<IActionResult> CheckStudent([FromBody] ControllerCheckByStudentRequest request, CancellationToken cancellationToken)
    {
        var data = await _service.CheckByStudentAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<ControllerCheckResultDto>.Ok(data));
    }

    private Guid RequireSchoolId() => _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
}
