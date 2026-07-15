using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Schools}/current/pedagogical-structure")]
public class PedagogicalStructureController : ControllerBase
{
    private readonly IPedagogicalStructureService _service;
    private readonly ICurrentUserService _currentUser;

    public PedagogicalStructureController(IPedagogicalStructureService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpPost("initialize")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalStructureSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Initialize(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _service.EnsureInitializedAsync(schoolId, cancellationToken);
        var summary = await _service.GetSummaryAsync(schoolId, skipEnsure: true, academicYearId: null, cancellationToken);
        return Ok(ApiResponse<PedagogicalStructureSummaryDto>.Ok(summary, "Structure pédagogique initialisée."));
    }

    [HttpGet("summary")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalStructureSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var summary = await _service.GetSummaryAsync(schoolId, academicYearId: academicYearId, cancellationToken: cancellationToken);
        return Ok(ApiResponse<PedagogicalStructureSummaryDto>.Ok(summary));
    }

    [HttpGet("classes")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PedagogicalClassDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClasses(
        [FromQuery] string? search,
        [FromQuery] SchoolProgram? program,
        [FromQuery] bool? enabledOnly,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var classes = await _service.GetClassesAsync(schoolId, search, program, enabledOnly, academicYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PedagogicalClassDto>>.Ok(classes));
    }

    [HttpPut("classes/{classId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalClassDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateClass(
        Guid classId,
        [FromBody] UpdatePedagogicalClassRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _service.UpdateClassAsync(schoolId, classId, request, cancellationToken);
        return Ok(ApiResponse<PedagogicalClassDto>.Ok(result, "Classe mise à jour."));
    }

    [HttpPut("classes")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PedagogicalClassDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkUpdateClasses(
        [FromBody] BulkUpdatePedagogicalClassesRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _service.BulkUpdateClassesAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PedagogicalClassDto>>.Ok(result, "Classes mises à jour."));
    }

    [HttpGet("classes/{classId:guid}/locals")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassLocalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocals(
        Guid classId,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var locals = await _service.GetLocalsAsync(schoolId, classId, academicYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ClassLocalDto>>.Ok(locals));
    }

    [HttpPost("locals")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ClassLocalDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateLocal(
        [FromBody] CreateClassLocalRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var local = await _service.CreateLocalAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<ClassLocalDto>.Ok(local, "Local créé."));
    }

    [HttpPut("locals/{localId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ClassLocalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateLocal(
        Guid localId,
        [FromBody] UpdateClassLocalRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var local = await _service.UpdateLocalAsync(schoolId, localId, request, cancellationToken);
        return Ok(ApiResponse<ClassLocalDto>.Ok(local, "Local mis à jour."));
    }

    [HttpDelete("locals/{localId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteLocal(Guid localId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _service.DeleteLocalAsync(schoolId, localId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Local supprimé."));
    }
}
