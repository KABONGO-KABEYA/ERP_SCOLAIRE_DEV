namespace SchoolManagement.Application.Grades.DTOs;

using SchoolManagement.Domain.Enums;

/// <summary>
/// Feuille officielle des résultats de classe — données exclusivement fournies par ResultCalculationService.
/// </summary>
public sealed record ClassResultsSheetDto(
    Guid ClassRoomId,
    string ClassDisplayName,
    Guid AcademicYearId,
    string AcademicYearLabel,
    PedagogicalSheetPeriodMode Mode,
    Guid SelectedPeriodId,
    string SelectedPeriodLabel,
    IReadOnlyList<Guid> IncludedSubPeriodIds,
    IReadOnlyList<ClassResultsCourseColumnDto> Courses,
    IReadOnlyList<ClassResultsStudentRowDto> Students,
    ClassResultsSummaryDto Summary);

public sealed record ClassResultsCourseColumnDto(
    Guid CourseId,
    string CourseName,
    int TargetMaxScore);

public sealed record ClassResultsStudentRowDto(
    Guid StudentId,
    string RegistrationNumber,
    string StudentName,
    int Rank,
    bool IsTied,
    IReadOnlyList<ClassResultsCourseCellDto> CourseCells,
    decimal? Average,
    decimal? Percentage,
    string AverageDisplay,
    string PercentageDisplay,
    string? Mention,
    ClassCouncilDecision Decision,
    string DecisionLabel,
    string StatusLabel);

public sealed record ClassResultsCourseCellDto(
    Guid CourseId,
    string Display);

public sealed record ClassResultsSummaryDto(
    string ClassAverageDisplay,
    string MaxObtainedDisplay,
    string MinObtainedDisplay,
    int StudentCount,
    int GradedStudentCount);
