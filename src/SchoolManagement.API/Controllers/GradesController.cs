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

    [HttpGet("cotation/assignments")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CotationAssignmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCotationAssignments(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid teacherId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var assignments = await _gradeService.GetCotationAssignmentsAsync(
            schoolId,
            academicYearId,
            teacherId,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CotationAssignmentDto>>.Ok(assignments));
    }

    [HttpGet("cotation/global-grid")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<GlobalCotationGridDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGlobalCotationGrid(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid teacherId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var grid = await _gradeService.GetGlobalCotationGridAsync(
            schoolId,
            academicYearId,
            classRoomId,
            teacherId,
            cancellationToken);
        return Ok(ApiResponse<GlobalCotationGridDto>.Ok(grid));
    }

    [HttpPost("cotation/global-save")]
    [Authorize(Policy = Permissions.GradesCreate)]
    [ProducesResponseType(typeof(ApiResponse<SaveGlobalCotationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveGlobalCotation(
        [FromBody] SaveGlobalCotationRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _gradeService.SaveGlobalCotationAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<SaveGlobalCotationResultDto>.Ok(
            result,
            $"{result.EvaluationsCreated} évaluation(s), {result.GradesSaved} note(s) enregistrée(s)."));
    }

    [HttpGet("cotation/global-sessions")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GlobalCotationSessionSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGlobalCotationSessions(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        [FromQuery] Guid teacherId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var sessions = await _gradeService.GetGlobalCotationSessionsAsync(
            schoolId,
            academicYearId,
            classRoomId,
            academicPeriodId,
            teacherId,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GlobalCotationSessionSummaryDto>>.Ok(sessions));
    }

    [HttpGet("cotation/global-session")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<GlobalCotationSessionLoadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadGlobalCotationSession(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid academicPeriodId,
        [FromQuery] Guid teacherId,
        [FromQuery] Guid evaluationTypeId,
        [FromQuery] string title,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var detail = await _gradeService.LoadGlobalCotationSessionAsync(
            schoolId,
            academicYearId,
            classRoomId,
            academicPeriodId,
            teacherId,
            evaluationTypeId,
            title,
            cancellationToken);
        return Ok(ApiResponse<GlobalCotationSessionLoadDto>.Ok(detail));
    }

    [HttpGet("cotation/course-notes-grid")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<CourseNotesGridDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourseNotesGrid(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid courseId,
        [FromQuery] Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var grid = await _gradeService.GetCourseNotesGridAsync(
            schoolId,
            academicYearId,
            classRoomId,
            courseId,
            academicPeriodId,
            cancellationToken);
        return Ok(ApiResponse<CourseNotesGridDto>.Ok(grid));
    }

    [HttpGet("cotation/pedagogical-sheet/context")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalSheetContextDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPedagogicalSheetContext(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var context = await _gradeService.GetPedagogicalSheetContextAsync(
            schoolId,
            academicYearId,
            classRoomId,
            cancellationToken);
        return Ok(ApiResponse<PedagogicalSheetContextDto>.Ok(context));
    }

    [HttpGet("cotation/pedagogical-sheet")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<PedagogicalSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPedagogicalSheet(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] PedagogicalSheetPeriodMode mode,
        [FromQuery] Guid periodId,
        [FromQuery] Guid teacherId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var sheet = await _gradeService.GetPedagogicalSheetAsync(
            schoolId,
            academicYearId,
            classRoomId,
            mode,
            periodId,
            teacherId,
            cancellationToken);
        return Ok(ApiResponse<PedagogicalSheetDto>.Ok(sheet));
    }

    /// <summary>Feuille officielle des résultats de classe (moteur ResultCalculationService).</summary>
    [HttpGet("results/class-sheet")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<ClassResultsSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassResultsSheet(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] PedagogicalSheetPeriodMode mode,
        [FromQuery] Guid periodId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var sheet = await _gradeService.GetClassResultsSheetAsync(
            schoolId,
            academicYearId,
            classRoomId,
            mode,
            periodId,
            cancellationToken);
        return Ok(ApiResponse<ClassResultsSheetDto>.Ok(sheet));
    }

    /// <summary>Résultat individuel (base bulletin) — moteur ResultCalculationService.</summary>
    [HttpGet("results/individual")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IndividualResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIndividualResult(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid classRoomId,
        [FromQuery] Guid studentId,
        [FromQuery] PedagogicalSheetPeriodMode mode,
        [FromQuery] Guid periodId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _gradeService.GetIndividualResultAsync(
            schoolId,
            academicYearId,
            classRoomId,
            studentId,
            mode,
            periodId,
            cancellationToken);
        return Ok(ApiResponse<IndividualResultDto>.Ok(result));
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
