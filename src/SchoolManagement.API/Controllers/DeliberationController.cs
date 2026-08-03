using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Deliberation.DTOs;
using SchoolManagement.Application.Deliberation.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.Deliberation)]
public class DeliberationController : ControllerBase
{
    private readonly IDeliberationService _service;
    private readonly ICurrentUserService _currentUser;

    public DeliberationController(IDeliberationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Conseil de classe : consultation PeriodResult + contexte période (mode auto).
    /// </summary>
    [HttpGet("sheet")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<DeliberationSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSheet(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var sheet = await _service.GetSheetAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
        return Ok(ApiResponse<DeliberationSheetDto>.Ok(sheet));
    }

    [HttpGet("minutes")]
    [Authorize(Policy = Permissions.DeliberationPvRead)]
    [ProducesResponseType(typeof(ApiResponse<DeliberationMinutesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMinutes(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var minutes = await _service.GetMinutesAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
        return Ok(ApiResponse<DeliberationMinutesDto>.Ok(minutes));
    }

    [HttpPut("minutes")]
    [Authorize(Policy = Permissions.DeliberationPvWrite)]
    [ProducesResponseType(typeof(ApiResponse<DeliberationMinutesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveMinutes(
        [FromBody] SaveDeliberationMinutesRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var minutes = await _service.SaveMinutesAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<DeliberationMinutesDto>.Ok(minutes, "Procès-verbal enregistré."));
    }

    [HttpGet("decision")]
    [Authorize(Policy = Permissions.DeliberationDecisionRead)]
    [ProducesResponseType(typeof(ApiResponse<DeliberationDecisionDialogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDecision(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        [FromQuery] Guid studentId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var dialog = await _service.GetDecisionDialogAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, studentId, cancellationToken);
        return Ok(ApiResponse<DeliberationDecisionDialogDto>.Ok(dialog));
    }

    [HttpPut("decision")]
    [Authorize(Policy = Permissions.DeliberationDecisionWrite)]
    [ProducesResponseType(typeof(ApiResponse<DeliberationDecisionDialogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveDecision(
        [FromBody] SaveDeliberationDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var dialog = await _service.SaveDecisionAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<DeliberationDecisionDialogDto>.Ok(dialog, "Décision du Conseil enregistrée."));
    }

    [HttpPut("conduct")]
    [Authorize(Policy = Permissions.DeliberationDecisionWrite)]
    [ProducesResponseType(typeof(ApiResponse<DeliberationSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveConduct(
        [FromBody] SaveStudentConductRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var sheet = await _service.SaveConductAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<DeliberationSheetDto>.Ok(sheet, "Conduite enregistrée."));
    }

    [HttpGet("bonus-dialog")]
    [Authorize(Policy = Permissions.DeliberationDecisionRead)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalBonusDialogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBonusDialog(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        [FromQuery] Guid studentId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var dialog = await _service.GetPedagogicalBonusDialogAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, studentId, cancellationToken);
        return Ok(ApiResponse<PedagogicalBonusDialogDto>.Ok(dialog));
    }

    [HttpPost("bonus")]
    [Authorize(Policy = Permissions.DeliberationDecisionWrite)]
    [ProducesResponseType(typeof(ApiResponse<DeliberationSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveBonus(
        [FromBody] SavePedagogicalBonusRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var sheet = await _service.SavePedagogicalBonusAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<DeliberationSheetDto>.Ok(sheet, "Bonus pédagogique enregistré. Résultats recalculés."));
    }

    [HttpGet("bonuses")]
    [Authorize(Policy = Permissions.DeliberationDecisionRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PedagogicalBonusDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBonuses(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        [FromQuery] Guid? studentId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var list = await _service.GetPedagogicalBonusesAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, studentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PedagogicalBonusDto>>.Ok(list));
    }

    /// <summary>Validation officielle de la classe (dernière étape du conseil).</summary>
    [HttpPost("validate-class")]
    [Authorize(Policy = Permissions.ResultsValidationValidate)]
    [ProducesResponseType(typeof(ApiResponse<ValidateDeliberationClassResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateClass(
        [FromBody] ValidateDeliberationClassRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _service.ValidateClassAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<ValidateDeliberationClassResultDto>.Ok(result, result.Message));
    }

    /// <summary>
    /// Annule la validation de classe tant que la sous-période n'est pas clôturée.
    /// </summary>
    [HttpPost("cancel-class-validation")]
    [Authorize(Policy = Permissions.ResultsValidationValidate)]
    [ProducesResponseType(typeof(ApiResponse<ValidateDeliberationClassResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelClassValidation(
        [FromBody] ValidateDeliberationClassRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _service.CancelClassValidationAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<ValidateDeliberationClassResultDto>.Ok(result, result.Message));
    }
}
