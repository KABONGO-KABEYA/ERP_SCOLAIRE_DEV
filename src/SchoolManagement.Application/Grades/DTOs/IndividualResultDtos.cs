namespace SchoolManagement.Application.Grades.DTOs;

using SchoolManagement.Domain.Enums;

/// <summary>
/// Résultat individuel — base du futur bulletin. Données exclusivement fournies par ResultCalculationService.
/// </summary>
public sealed record IndividualResultDto(
    Guid StudentId,
    string RegistrationNumber,
    string StudentName,
    string? PhotoPath,
    Guid ClassRoomId,
    string ClassDisplayName,
    Guid AcademicYearId,
    string AcademicYearLabel,
    PedagogicalSheetPeriodMode Mode,
    Guid SelectedPeriodId,
    string SelectedPeriodLabel,
    int Rank,
    bool IsTied,
    int ClassSize,
    decimal? Average,
    decimal? Percentage,
    string AverageDisplay,
    string PercentageDisplay,
    string? Mention,
    ClassCouncilDecision Decision,
    string DecisionLabel,
    string RankDisplay,
    IReadOnlyList<IndividualResultCourseRowDto> Courses);

public sealed record IndividualResultCourseRowDto(
    Guid CourseId,
    string CourseName,
    string MaximumDisplay,
    string TotalObtainedDisplay,
    string ResultDisplay,
    string? Mention,
    string Observation,
    decimal? Result = null,
    decimal? Maximum = null);
