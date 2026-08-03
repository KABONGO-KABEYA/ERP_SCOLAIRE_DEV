namespace SchoolManagement.Application.Grades.DTOs;

/// <summary>
/// Feuille pédagogique officielle (Vue globale) — consultation seule, sans calcul métier.
/// </summary>
public enum PedagogicalSheetPeriodMode
{
    SubPeriod = 1,
    MainPeriod = 2
}

public sealed record PedagogicalSheetContextDto(
    Guid ClassRoomId,
    string ClassDisplayName,
    Guid AcademicYearId,
    string AcademicYearLabel,
    IReadOnlyList<PedagogicalSheetPeriodOptionDto> SubPeriods,
    IReadOnlyList<PedagogicalSheetPeriodOptionDto> MainPeriods,
    Guid? DefaultSubPeriodId,
    Guid? DefaultMainPeriodId);

public sealed record PedagogicalSheetPeriodOptionDto(
    Guid Id,
    string Name,
    PedagogicalSheetPeriodMode Mode,
    string? KindLabel,
    int OrderIndex);

public sealed record PedagogicalSheetDto(
    Guid ClassRoomId,
    string ClassDisplayName,
    Guid AcademicYearId,
    PedagogicalSheetPeriodMode Mode,
    Guid SelectedPeriodId,
    string SelectedPeriodLabel,
    IReadOnlyList<Guid> IncludedSubPeriodIds,
    IReadOnlyList<PedagogicalSheetCourseGroupDto> Courses,
    IReadOnlyList<PedagogicalSheetStudentRowDto> Students,
    PedagogicalSheetSummaryDto Summary);

public sealed record PedagogicalSheetCourseGroupDto(
    Guid CourseId,
    string CourseName,
    /// <summary>Barème prévu du cours pour la période (normalisation de la moyenne).</summary>
    int TargetMaxScore,
    IReadOnlyList<PedagogicalSheetEvaluationColumnDto> Evaluations);

public sealed record PedagogicalSheetEvaluationColumnDto(
    Guid EvaluationId,
    string Title,
    DateOnly EvaluationDate,
    int MaxScore,
    Guid AcademicPeriodId,
    string PeriodName);

/// <summary>
/// Ligne élève : cellules indexées par cours, dans le même ordre que <see cref="PedagogicalSheetDto.Courses"/>.
/// </summary>
public sealed record PedagogicalSheetStudentRowDto(
    int RowNumber,
    Guid StudentId,
    string RegistrationNumber,
    string StudentName,
    IReadOnlyList<PedagogicalSheetCourseCellsDto> CourseCells);

public sealed record PedagogicalSheetCourseCellsDto(
    Guid CourseId,
    IReadOnlyList<PedagogicalSheetCellDto> Cells,
    /// <summary>TOTAL — somme des notes (moteur).</summary>
    string TotalDisplay,
    /// <summary>MOYENNE — normalisée sur le barème du cours (moteur).</summary>
    string AverageDisplay);

public sealed record PedagogicalSheetCellDto(
    Guid EvaluationId,
    /// <summary>Note formatée, ABS, DISP, EXC ou —.</summary>
    string Display);

/// <summary>Ligne de synthèse — structure réservée, valeurs placeholder.</summary>
public sealed record PedagogicalSheetSummaryDto(
    string ClassAverageDisplay,
    string MaxObtainedDisplay,
    string MinObtainedDisplay,
    string GradedCountDisplay,
    string AbsentCountDisplay);
