namespace SchoolManagement.Application.Grades.DTOs;

/// <summary>
/// Grille consolidée lecture seule : élèves × évaluations du cours (sous-période ouverte).
/// Aucun calcul de moyenne — réservé au futur moteur pédagogique.
/// </summary>
public sealed record CourseNotesGridDto(
    Guid CourseId,
    string CourseName,
    Guid ClassRoomId,
    string ClassDisplayName,
    Guid AcademicYearId,
    Guid AcademicPeriodId,
    string PeriodName,
    int EvaluationCount,
    int StudentCount,
    IReadOnlyList<CourseNotesEvaluationColumnDto> Evaluations,
    IReadOnlyList<CourseNotesStudentRowDto> Students);

public sealed record CourseNotesEvaluationColumnDto(
    Guid EvaluationId,
    string Title,
    string EvaluationTypeName,
    DateOnly EvaluationDate,
    int MaxScore,
    decimal Weight);

public sealed record CourseNotesStudentRowDto(
    int RowNumber,
    Guid StudentId,
    string RegistrationNumber,
    string StudentName,
    IReadOnlyList<CourseNotesCellDto> Cells);

public sealed record CourseNotesCellDto(
    Guid EvaluationId,
    decimal? Score,
    bool IsAbsent,
    bool HasGrade);
