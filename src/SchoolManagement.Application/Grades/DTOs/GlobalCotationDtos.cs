namespace SchoolManagement.Application.Grades.DTOs;

using SchoolManagement.Domain.Enums;

/// <summary>
/// Grille de cotation globale (titulaire) — lecture seule, aucune évaluation créée à l'ouverture.
/// </summary>
public sealed record GlobalCotationGridDto(
    Guid ClassRoomId,
    string ClassDisplayName,
    string SectionName,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid AcademicPeriodId,
    string PeriodName,
    AcademicSubPeriodKind PeriodKind,
    string PeriodKindLabel,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    IReadOnlyList<GlobalCotationCourseColumnDto> Courses,
    IReadOnlyList<GlobalCotationStudentRowDto> Students,
    IReadOnlyList<EvaluationTypeDto> EvaluationTypes);

public sealed record GlobalCotationCourseColumnDto(
    Guid CourseId,
    Guid AssignmentId,
    string CourseName,
    int MaxScore);

public sealed record GlobalCotationStudentRowDto(
    int RowNumber,
    Guid StudentId,
    string RegistrationNumber,
    string StudentName);

/// <summary>
/// Enregistrement transactionnel : crée évaluations + notes uniquement pour les cours cotés.
/// </summary>
public sealed record SaveGlobalCotationRequest(
    Guid AcademicYearId,
    Guid AcademicPeriodId,
    Guid ClassRoomId,
    Guid EvaluationTypeId,
    string Title,
    DateOnly EvaluationDate,
    IReadOnlyList<GlobalCotationCourseSaveDto> Courses);

public sealed record GlobalCotationCourseSaveDto(
    Guid CourseId,
    int MaxScore,
    IReadOnlyList<GradeEntryInput> Grades);

public sealed record SaveGlobalCotationResultDto(
    int EvaluationsCreated,
    int GradesSaved);

/// <summary>Vague d'évaluation déjà enregistrée (même type + libellé sur plusieurs cours).</summary>
public sealed record GlobalCotationSessionSummaryDto(
    Guid EvaluationTypeId,
    string EvaluationTypeName,
    string Title,
    DateOnly EvaluationDate,
    int CourseCount,
    int GradedEntryCount,
    bool CanEdit,
    string DisplayLabel);

/// <summary>Détail d'une vague pour préremplir la grille de saisie globale.</summary>
public sealed record GlobalCotationSessionLoadDto(
    Guid EvaluationTypeId,
    string Title,
    DateOnly EvaluationDate,
    bool CanEdit,
    string? ReadOnlyReason,
    IReadOnlyList<GlobalCotationSessionCourseLoadDto> Courses);

public sealed record GlobalCotationSessionCourseLoadDto(
    Guid CourseId,
    Guid EvaluationId,
    int MaxScore,
    bool IsOpen,
    IReadOnlyList<GlobalCotationSessionGradeDto> Grades);

public sealed record GlobalCotationSessionGradeDto(
    Guid StudentId,
    decimal Score,
    bool IsAbsent,
    string? Comment);
