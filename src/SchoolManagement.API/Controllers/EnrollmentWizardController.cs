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
    private readonly IEnrollmentFormService _enrollmentFormService;
    private readonly ICurrentUserService _currentUser;

    public EnrollmentWizardController(
        IEnrollmentWizardService wizardService,
        IEnrollmentFormService enrollmentFormService,
        ICurrentUserService currentUser)
    {
        _wizardService = wizardService;
        _enrollmentFormService = enrollmentFormService;
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
    public async Task<IActionResult> SearchStudents(
        [FromQuery] string search,
        [FromQuery] bool forReinscription = false,
        CancellationToken cancellationToken = default)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.SearchStudentsAsync(schoolId, search, forReinscription, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EnrollmentStudentSearchResultDto>>.Ok(result));
    }

    [HttpGet("search-guardians")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EnrollmentGuardianSearchResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchGuardians([FromQuery] string search, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.SearchGuardiansAsync(schoolId, search, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EnrollmentGuardianSearchResultDto>>.Ok(result));
    }

    [HttpPost("store-file")]
    [Authorize(Policy = Permissions.StudentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<StoredEnrollmentFileDto>), StatusCodes.Status200OK)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> StoreEnrollmentFile(
        [FromForm] string lastName,
        [FromForm] string firstName,
        [FromForm] string registrationNumber,
        [FromForm] string academicYearLabel,
        [FromForm] string documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await using var stream = file.OpenReadStream();
        var result = await _wizardService.StoreEnrollmentFileAsync(
            schoolId,
            lastName,
            firstName,
            registrationNumber,
            academicYearLabel,
            documentType,
            file.FileName,
            stream,
            cancellationToken);
        return Ok(ApiResponse<StoredEnrollmentFileDto>.Ok(result));
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
    public async Task<IActionResult> CalculateFees(
        [FromQuery] Guid? pedagogicalClassId,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.CalculateFeesAsync(
            schoolId,
            pedagogicalClassId,
            academicYearId,
            cancellationToken: cancellationToken);
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

    [HttpGet("fiche-inscription/{enrollmentId:guid}")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentFormDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnrollmentForm(Guid enrollmentId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _enrollmentFormService.GetFormAsync(schoolId, enrollmentId, cancellationToken);
        return Ok(ApiResponse<EnrollmentFormDocumentDto>.Ok(result));
    }

    [HttpGet("student-dossier/{studentId:guid}")]
    [Authorize(Policy = Permissions.StudentsRead)]
    [ProducesResponseType(typeof(ApiResponse<StudentDossierEditDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentDossierForEdit(Guid studentId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.GetStudentDossierForEditAsync(schoolId, studentId, cancellationToken);
        return Ok(ApiResponse<StudentDossierEditDto>.Ok(result));
    }

    [HttpPost("student-dossier/{enrollmentId:guid}/validate")]
    [Authorize(Policy = Permissions.StudentsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<EnrollmentValidationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateStudentDossierUpdate(
        Guid enrollmentId,
        [FromBody] CompleteEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.ValidateStudentDossierUpdateAsync(
            schoolId,
            enrollmentId,
            request,
            cancellationToken);
        return Ok(ApiResponse<EnrollmentValidationResultDto>.Ok(result));
    }

    [HttpPut("student-dossier/{enrollmentId:guid}")]
    [Authorize(Policy = Permissions.StudentsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<UpdateStudentDossierResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStudentDossier(
        Guid enrollmentId,
        [FromBody] CompleteEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _wizardService.UpdateStudentDossierAsync(
            schoolId,
            enrollmentId,
            request,
            cancellationToken);
        return Ok(ApiResponse<UpdateStudentDossierResultDto>.Ok(result, result.Message));
    }
}
