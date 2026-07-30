using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Grades}")]
public class GradesController : ControllerBase
{
    private readonly IGradeService _gradeService;
    private readonly ICurrentUserService _currentUser;

    public GradesController(IGradeService gradeService, ICurrentUserService currentUser)
    {
        _gradeService = gradeService;
        _currentUser = currentUser;
    }

    [HttpPost("cotation/session")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<CotationSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> OpenCotationSession(
        [FromBody] OpenCotationSessionRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var session = await _gradeService.OpenCotationSessionAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<CotationSessionDto>.Ok(session, "Session de cotation ouverte."));
    }

    [HttpGet("cotation/periods")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CotationPeriodDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCotationPeriods(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var periods = await _gradeService.GetCotationPeriodsAsync(schoolId, academicYearId, classRoomId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CotationPeriodDto>>.Ok(periods));
    }

    [HttpGet("evaluation-types")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EvaluationTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvaluationTypes(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var types = await _gradeService.GetEvaluationTypesAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EvaluationTypeDto>>.Ok(types));
    }

    [HttpPost("evaluations")]
    [Authorize(Policy = Permissions.GradesCreate)]
    [ProducesResponseType(typeof(ApiResponse<EvaluationDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEvaluation([FromBody] CreateEvaluationRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var evaluation = await _gradeService.CreateEvaluationAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<EvaluationDto>.Ok(evaluation, "Évaluation créée."));
    }

    [HttpPut("evaluations/{evaluationId:guid}")]
    [Authorize(Policy = Permissions.GradesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<EvaluationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEvaluation(
        Guid evaluationId,
        [FromBody] UpdateEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var evaluation = await _gradeService.UpdateEvaluationAsync(schoolId, evaluationId, request, cancellationToken);
        return Ok(ApiResponse<EvaluationDto>.Ok(evaluation, "Évaluation mise à jour."));
    }

    [HttpDelete("evaluations/{evaluationId:guid}")]
    [Authorize(Policy = Permissions.GradesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteEvaluation(Guid evaluationId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _gradeService.DeleteEvaluationAsync(schoolId, evaluationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Évaluation supprimée."));
    }

    [HttpGet("evaluations")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EvaluationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvaluations(
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var evaluations = await _gradeService.GetEvaluationsByClassAsync(schoolId, classRoomId, academicPeriodId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EvaluationDto>>.Ok(evaluations));
    }

    [HttpGet("evaluations/{evaluationId:guid}/entries")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GradeEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGradeEntries(Guid evaluationId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var grades = await _gradeService.GetGradesAsync(schoolId, evaluationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GradeEntryDto>>.Ok(grades));
    }

    [HttpPost("entries")]
    [Authorize(Policy = Permissions.GradesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitGrades([FromBody] SubmitGradesRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _gradeService.SubmitGradesAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Notes enregistrées."));
    }

    [HttpGet("period-results")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PeriodResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPeriodResults(
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var results = await _gradeService.GetPeriodResultsAsync(schoolId, classRoomId, academicPeriodId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PeriodResultDto>>.Ok(results));
    }

    [HttpPost("period-results/calculate")]
    [Authorize(Policy = Permissions.GradesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PeriodResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculatePeriodResults([FromBody] CalculatePeriodResultsRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var results = await _gradeService.CalculatePeriodResultsAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PeriodResultDto>>.Ok(results, "Moyennes et rangs calculés."));
    }
}
