using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Academic.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Academic}")]
public class AcademicController : ControllerBase
{
    private readonly IAcademicService _academicService;
    private readonly ICurrentUserService _currentUser;

    public AcademicController(IAcademicService academicService, ICurrentUserService currentUser)
    {
        _academicService = academicService;
        _currentUser = currentUser;
    }

    [HttpGet("sections")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSections(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var sections = await _academicService.GetSectionsAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SectionDto>>.Ok(sections));
    }

    [HttpGet("classrooms")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassRoomDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassRooms([FromQuery] Guid? academicYearId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var classes = await _academicService.GetClassRoomsAsync(schoolId, academicYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ClassRoomDto>>.Ok(classes));
    }

    [HttpPost("classrooms")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ClassRoomDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateClassRoom([FromBody] CreateClassRoomRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var classRoom = await _academicService.CreateClassRoomAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<ClassRoomDto>.Ok(classRoom, "Classe créée."));
    }

    [HttpGet("courses")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CourseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourses([FromQuery] Guid? classRoomId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var courses = await _academicService.GetCoursesAsync(schoolId, classRoomId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CourseDto>>.Ok(courses));
    }

    [HttpPost("courses")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<CourseDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var course = await _academicService.CreateCourseAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<CourseDto>.Ok(course, "Matière créée."));
    }

    [HttpPut("courses/{courseId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<CourseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCourse(
        Guid courseId,
        [FromBody] UpdateCourseRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var course = await _academicService.UpdateCourseAsync(schoolId, courseId, request, cancellationToken);
        return Ok(ApiResponse<CourseDto>.Ok(course, "Matière mise à jour."));
    }

    [HttpDelete("courses/{courseId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCourse(Guid courseId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _academicService.DeleteCourseAsync(schoolId, courseId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Matière supprimée."));
    }

    [HttpGet("enrollments")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EnrollmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnrollments(
        [FromQuery] Guid? classRoomId,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var enrollments = await _academicService.GetEnrollmentsAsync(schoolId, classRoomId, academicYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EnrollmentDto>>.Ok(enrollments));
    }

    [HttpPost("enrollments")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEnrollment([FromBody] CreateEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var enrollment = await _academicService.CreateEnrollmentAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<EnrollmentDto>.Ok(enrollment, "Inscription créée."));
    }
}
