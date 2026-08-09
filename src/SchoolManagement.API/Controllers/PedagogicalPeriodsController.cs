using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Application.PedagogicalPeriods.DTOs;
using SchoolManagement.Application.PedagogicalPeriods.Interfaces;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/pedagogical-periods")]
public sealed class PedagogicalPeriodsController : ControllerBase
{
    private readonly IPedagogicalPeriodService _service;
    private readonly IGradeService _gradeService;
    private readonly ICurrentUserService _currentUser;

    public PedagogicalPeriodsController(
        IPedagogicalPeriodService service,
        IGradeService gradeService,
        ICurrentUserService currentUser)
    {
        _service = service;
        _gradeService = gradeService;
        _currentUser = currentUser;
    }

    [HttpGet("structure")]
    [Authorize(Policy = Permissions.PedagogicalPeriodsManage)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalPeriodStructureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStructure(
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var structure = await _service.GetStructureAsync(schoolId, academicYearId, cancellationToken);
        return Ok(ApiResponse<PedagogicalPeriodStructureDto>.Ok(structure));
    }

    [HttpPost("structure")]
    [Authorize(Policy = Permissions.PedagogicalPeriodsManage)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalPeriodStructureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateStructure(
        [FromBody] CreatePedagogicalStructureRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var structure = await _service.CreateDefaultStructureAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<PedagogicalPeriodStructureDto>.Ok(structure, "Structure pédagogique créée."));
    }

    [HttpPost("structure/propose-dates")]
    [Authorize(Policy = Permissions.PedagogicalPeriodsManage)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalPeriodStructureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProposeDates(
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var structure = await _service.ProposeSequentialDatesAsync(schoolId, academicYearId, cancellationToken);
        return Ok(ApiResponse<PedagogicalPeriodStructureDto>.Ok(structure, "Dates proposées pour les périodes à venir."));
    }

    [HttpPost("sub-periods/{id:guid}/open")]
    [Authorize(Policy = Permissions.PedagogicalPeriodsManage)]
    public async Task<IActionResult> Open(
        Guid id,
        [FromBody] OpenSubPeriodRequest? request,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var dto = await _service.OpenSubPeriodAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<PedagogicalSubPeriodDto>.Ok(dto, "Sous-période ouverte."));
    }

    [HttpPost("sub-periods/{id:guid}/close")]
    [Authorize(Policy = Permissions.PedagogicalPeriodsManage)]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var dto = await _service.CloseSubPeriodAsync(schoolId, id, cancellationToken);
        if (dto.Kind == AcademicSubPeriodKind.Examen)
        {
            await _gradeService.CalculateResultsForClosedExamAsync(schoolId, id, cancellationToken);
        }

        return Ok(ApiResponse<PedagogicalSubPeriodDto>.Ok(dto, "Sous-période clôturée."));
    }

    [HttpPost("sub-periods/{id:guid}/lock")]
    [Authorize(Policy = Permissions.PedagogicalPeriodsManage)]
    public async Task<IActionResult> Lock(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var dto = await _service.LockSubPeriodAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<PedagogicalSubPeriodDto>.Ok(dto, "Sous-période verrouillée."));
    }

    [HttpPost("sub-periods/{id:guid}/unlock")]
    [Authorize(Policy = Permissions.PedagogicalPeriodsManage)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var dto = await _service.UnlockSubPeriodAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<PedagogicalSubPeriodDto>.Ok(dto, "Sous-période déverrouillée."));
    }

    [HttpPut("sub-periods/{id:guid}/settings")]
    [Authorize(Policy = Permissions.PedagogicalPeriodsManage)]
    public async Task<IActionResult> UpdateSettings(
        Guid id,
        [FromBody] UpdateSubPeriodSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var dto = await _service.UpdateSubPeriodSettingsAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<PedagogicalSubPeriodDto>.Ok(dto, "Paramètres mis à jour."));
    }

    [HttpGet("active")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<ActiveSubPeriodDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive(
        [FromQuery] Guid academicYearId,
        [FromQuery] PedagogicalCycleGroup cycleGroup,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var active = await _service.GetActiveSubPeriodAsync(schoolId, academicYearId, cycleGroup, cancellationToken);
        return Ok(ApiResponse<ActiveSubPeriodDto?>.Ok(active));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
}
