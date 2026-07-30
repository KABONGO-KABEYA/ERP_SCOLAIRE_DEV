namespace SchoolManagement.Application.Grades.Interfaces;

using SchoolManagement.Application.Grades.DTOs;

public interface IGradeService
{
    Task<IReadOnlyList<EvaluationTypeDto>> GetEvaluationTypesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    /// <summary>Ouvre une session de cotation après identification de l'enseignant.</summary>
    Task<CotationSessionDto> OpenCotationSessionAsync(
        Guid schoolId,
        OpenCotationSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Périodes filtrées selon le cycle de la classe (trimestres vs semestres).</summary>
    Task<IReadOnlyList<CotationPeriodDto>> GetCotationPeriodsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        CancellationToken cancellationToken = default);

    Task<EvaluationDto> CreateEvaluationAsync(Guid schoolId, CreateEvaluationRequest request, CancellationToken cancellationToken = default);

    Task<EvaluationDto> UpdateEvaluationAsync(
        Guid schoolId,
        Guid evaluationId,
        UpdateEvaluationRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteEvaluationAsync(Guid schoolId, Guid evaluationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvaluationDto>> GetEvaluationsByClassAsync(
        Guid schoolId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradeEntryDto>> GetGradesAsync(Guid schoolId, Guid evaluationId, CancellationToken cancellationToken = default);

    Task SubmitGradesAsync(Guid schoolId, SubmitGradesRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PeriodResultDto>> CalculatePeriodResultsAsync(
        Guid schoolId,
        CalculatePeriodResultsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PeriodResultDto>> GetPeriodResultsAsync(
        Guid schoolId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Après clôture d'un examen : calcule les résultats de période pour toutes les classes concernées.
    /// </summary>
    Task CalculateResultsForClosedExamAsync(
        Guid schoolId,
        Guid examSubPeriodId,
        CancellationToken cancellationToken = default);
}
