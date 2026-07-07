using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Application.Students.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Students}")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly ICurrentUserService _currentUser;

    public StudentsController(IStudentService studentService, ICurrentUserService currentUser)
    {
        _studentService = studentService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<StudentListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] StudentSearchRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _studentService.SearchAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<StudentListDto>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<StudentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var student = await _studentService.GetByIdAsync(schoolId, id, cancellationToken);
        return student is null ? NotFound() : Ok(ApiResponse<StudentDto>.Ok(student));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.StudentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<StudentDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var student = await _studentService.CreateAsync(schoolId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = student.Id }, ApiResponse<StudentDto>.Ok(student, "Élève créé."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.StudentsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<StudentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var student = await _studentService.UpdateAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<StudentDto>.Ok(student, "Élève mis à jour."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.StudentsDelete)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _studentService.ArchiveAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Élève archivé."));
    }
}
