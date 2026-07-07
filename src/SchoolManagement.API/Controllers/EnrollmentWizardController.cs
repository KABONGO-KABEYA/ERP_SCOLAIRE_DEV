namespace SchoolManagement.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.EnrollmentWizard.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

[ApiController]
[Authorize]
[Route("api/v1/enrollment-wizard")]
public class EnrollmentWizardController : ControllerBase
{
    private readonly IEnrollmentWizardService _wizardService;
    private readonly ICurrentUserService _currentUser;

    public EnrollmentWizardController(IEnrollmentWizardService wizardService, ICurrentUserService currentUser)
    {
        _wizardService = wizardService;
        _currentUser = currentUser;
    }

    [HttpGet("prerequisites")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentPrerequisitesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrerequisites(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.GetPrerequisitesAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<EnrollmentPrerequisitesDto>.Ok(result));
    }

    [HttpGet("registration-number")]
    [Authorize(Policy = Permissions.StudentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<GeneratedRegistrationNumberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateRegistrationNumber(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.GenerateRegistrationNumberAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<GeneratedRegistrationNumberDto>.Ok(result));
    }

    [HttpGet("search-students")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EnrollmentStudentSearchResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchStudents([FromQuery] string search, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.SearchStudentsAsync(schoolId, search, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EnrollmentStudentSearchResultDto>>.Ok(result));
    }

    [HttpGet("structure-options")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentStructureOptionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStructureOptions(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.GetStructureOptionsAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<EnrollmentStructureOptionsDto>.Ok(result));
    }

    [HttpGet("class-capacity")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<ClassCapacityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassCapacity(
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.GetClassCapacityAsync(schoolId, classRoomId, academicYearId, cancellationToken);
        return Ok(ApiResponse<ClassCapacityDto>.Ok(result));
    }

    [HttpGet("fees")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentFeeSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculateFees(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.CalculateFeesAsync(schoolId, cancellationToken: cancellationToken);
        return Ok(ApiResponse<EnrollmentFeeSummaryDto>.Ok(result));
    }

    [HttpPost("validate")]
    [Authorize(Policy = Permissions.StudentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentValidationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate([FromBody] CompleteEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.ValidateAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<EnrollmentValidationResultDto>.Ok(result));
    }

    [HttpPost("complete")]
    [Authorize(Policy = Permissions.StudentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<CompleteEnrollmentResultDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Complete([FromBody] CompleteEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.CompleteAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<CompleteEnrollmentResultDto>.Ok(result, result.Message));
    }
}
