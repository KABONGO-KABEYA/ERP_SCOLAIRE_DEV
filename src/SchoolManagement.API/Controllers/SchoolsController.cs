using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Schools}")]
public class SchoolsController : ControllerBase
{
    private readonly ISchoolService _schoolService;
    private readonly ICurrentUserService _currentUser;

    public SchoolsController(ISchoolService schoolService, ICurrentUserService currentUser)
    {
        _schoolService = schoolService;
        _currentUser = currentUser;
    }

    [HttpGet("current")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<SchoolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var school = await _schoolService.GetSchoolAsync(schoolId, cancellationToken);
        return school is null ? NotFound() : Ok(ApiResponse<SchoolDto>.Ok(school));
    }

    [HttpPut("current")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<SchoolDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCurrent([FromBody] UpdateSchoolRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var school = await _schoolService.UpdateSchoolAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<SchoolDto>.Ok(school, "École mise à jour."));
    }

    [HttpGet("current/academic-years")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AcademicYearDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAcademicYears(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var years = await _schoolService.GetAcademicYearsAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AcademicYearDto>>.Ok(years));
    }

    [HttpPost("current/academic-years")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<AcademicYearDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAcademicYear([FromBody] CreateAcademicYearRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var year = await _schoolService.CreateAcademicYearAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<AcademicYearDto>.Ok(year, "Année scolaire créée."));
    }

    [HttpPut("current/academic-years/{yearId:guid}/set-current")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetCurrentYear(Guid yearId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _schoolService.SetCurrentAcademicYearAsync(schoolId, yearId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Année courante mise à jour."));
    }

    [HttpGet("current/lookups")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<SchoolLookupsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookups(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var lookups = await _schoolService.GetLookupsAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<SchoolLookupsDto>.Ok(lookups));
    }

    [HttpGet("current/regulation")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<SchoolRegulationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegulation(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var regulation = await _schoolService.GetRegulationAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<SchoolRegulationDto>.Ok(regulation));
    }

    [HttpPut("current/regulation")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<SchoolRegulationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRegulation([FromBody] UpdateSchoolRegulationRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var regulation = await _schoolService.UpdateRegulationAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<SchoolRegulationDto>.Ok(regulation, "Règlement mis à jour."));
    }
}
