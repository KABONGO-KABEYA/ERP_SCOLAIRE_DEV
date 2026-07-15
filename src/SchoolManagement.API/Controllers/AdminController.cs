using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Admin.DTOs;
using SchoolManagement.Application.Admin.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Enrollment.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Admin}")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ITeacherAdminService _teacherAdminService;
    private readonly IEnrollmentMaintenanceService _enrollmentMaintenanceService;
    private readonly ICurrentUserService _currentUser;

    public AdminController(
        IAdminService adminService,
        ITeacherAdminService teacherAdminService,
        IEnrollmentMaintenanceService enrollmentMaintenanceService,
        ICurrentUserService currentUser)
    {
        _adminService = adminService;
        _teacherAdminService = teacherAdminService;
        _enrollmentMaintenanceService = enrollmentMaintenanceService;
        _currentUser = currentUser;
    }
    [HttpGet("users")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserAccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var users = await _adminService.GetUsersAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<UserAccountDto>>.Ok(users));
    }

    [HttpGet("roles")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var roles = await _adminService.GetRolesAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RoleDto>>.Ok(roles));
    }

    [HttpPost("users")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<UserAccountDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var user = await _adminService.CreateUserAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<UserAccountDto>.Ok(user, "Utilisateur créé."));
    }

    [HttpPut("users/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<UserAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var user = await _adminService.UpdateUserAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<UserAccountDto>.Ok(user, "Utilisateur mis à jour."));
    }

    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<UserAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetUserRoles(Guid id, [FromBody] SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var user = await _adminService.SetUserRolesAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<UserAccountDto>.Ok(user, "Rôles mis à jour."));
    }

    [HttpGet("teachers")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TeacherAdminDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeachers(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var teachers = await _teacherAdminService.GetTeachersAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeacherAdminDto>>.Ok(teachers));
    }

    [HttpPost("teachers")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<TeacherAdminDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTeacher([FromBody] CreateTeacherAdminRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var teacher = await _teacherAdminService.CreateTeacherAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<TeacherAdminDto>.Ok(teacher, "Enseignant créé."));
    }

    [HttpPut("teachers/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<TeacherAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTeacher(Guid id, [FromBody] UpdateTeacherAdminRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var teacher = await _teacherAdminService.UpdateTeacherAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<TeacherAdminDto>.Ok(teacher, "Enseignant mis à jour."));
    }

    [HttpPost("reset-enrollment-data")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentResetResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetEnrollmentData(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _enrollmentMaintenanceService.ResetEnrollmentDataAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<EnrollmentResetResultDto>.Ok(result, result.Message));
    }
}
