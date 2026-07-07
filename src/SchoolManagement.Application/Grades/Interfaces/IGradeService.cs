namespace SchoolManagement.Application.Grades.Interfaces;

using SchoolManagement.Application.Grades.DTOs;

public interface IGradeService
{
    Task<EvaluationDto> CreateEvaluationAsync(Guid schoolId, CreateEvaluationRequest request, CancellationToken cancellationToken = default);

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
}
