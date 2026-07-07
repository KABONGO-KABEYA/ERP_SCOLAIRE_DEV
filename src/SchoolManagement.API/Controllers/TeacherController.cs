using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Teacher.DTOs;
using SchoolManagement.Application.Teacher.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Teacher}")]
public class TeacherController : ControllerBase
{
    private readonly ITeacherService _teacherService;
    private readonly IUserAccountRepository _userRepository;
    private readonly ICurrentUserService _currentUser;

    public TeacherController(
        ITeacherService teacherService,
        IUserAccountRepository userRepository,
        ICurrentUserService currentUser)
    {
        _teacherService = teacherService;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    [HttpGet("assignments")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TeacherAssignmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(CancellationToken cancellationToken)
    {
        var teacherId = await ResolveTeacherIdAsync(cancellationToken);
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var assignments = await _teacherService.GetMyAssignmentsAsync(teacherId, schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeacherAssignmentDto>>.Ok(assignments));
    }

    [HttpGet("classes/{classRoomId:guid}/students")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TeacherStudentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassStudents(Guid classRoomId, CancellationToken cancellationToken)
    {
        var teacherId = await ResolveTeacherIdAsync(cancellationToken);
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var students = await _teacherService.GetClassStudentsAsync(teacherId, schoolId, classRoomId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeacherStudentDto>>.Ok(students));
    }

    [HttpGet("periods")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TeacherPeriodDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPeriods([FromQuery] Guid academicYearId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var periods = await _teacherService.GetAcademicPeriodsAsync(schoolId, academicYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TeacherPeriodDto>>.Ok(periods));
    }

    private async Task<Guid> ResolveTeacherIdAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException();

        if (user.TeacherId is null)
        {
            throw new UnauthorizedAccessException("Ce compte n'est pas lié à un enseignant.");
        }

        return user.TeacherId.Value;
    }
}
