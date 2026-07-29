using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.CourseConfiguration.DTOs;
using SchoolManagement.Application.CourseConfiguration.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Base}/course-configuration")]
public class CourseConfigurationController : ControllerBase
{
    private readonly ICourseConfigurationService _courseConfigurationService;
    private readonly ICurrentUserService _currentUser;

    public CourseConfigurationController(
        ICourseConfigurationService courseConfigurationService,
        ICurrentUserService currentUser)
    {
        _courseConfigurationService = courseConfigurationService;
        _currentUser = currentUser;
    }

    [HttpGet("branches")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetBranches(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _courseConfigurationService.GetBranchesAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BranchOptionDto>>.Ok(items));
    }

    [HttpGet("available-courses")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetAvailableCourses(
        [FromQuery] Guid pedagogicalClassId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _courseConfigurationService.GetAvailableCoursesAsync(
            schoolId,
            pedagogicalClassId,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AvailableCourseBranchGroupDto>>.Ok(items));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetConfiguration(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid pedagogicalClassId,
        [FromQuery] Guid classRoomId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var configuration = await _courseConfigurationService.GetConfigurationAsync(
            schoolId,
            academicYearId,
            pedagogicalClassId,
            classRoomId,
            cancellationToken);

        return Ok(ApiResponse<CourseConfigurationDto>.Ok(configuration));
    }

    [HttpPut]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> SaveConfiguration(
        [FromBody] SaveCourseConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var configuration = await _courseConfigurationService.SaveConfigurationAsync(
            schoolId,
            request,
            cancellationToken);

        return Ok(ApiResponse<CourseConfigurationDto>.Ok(configuration, "Configuration des cours enregistrée."));
    }

    [HttpPost("courses")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CreateCatalogCourse(
        [FromBody] CreateCatalogCourseRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var created = await _courseConfigurationService.CreateCatalogCourseAsync(
            schoolId,
            request,
            cancellationToken);

        return Ok(ApiResponse<CreateCatalogCourseResultDto>.Ok(created, "Cours créé."));
    }
}
