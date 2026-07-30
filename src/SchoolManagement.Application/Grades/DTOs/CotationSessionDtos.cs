namespace SchoolManagement.Application.Grades.DTOs;

using SchoolManagement.Domain.Enums;

/// <summary>
/// Portée d'accès cotation — extensible (Titulaire / Préfet) sans changer l'UI.
/// </summary>
public enum CotationAccessScope
{
    /// <summary>Uniquement ses affectations cours/classe.</summary>
    Teacher = 1,

    /// <summary>Toutes les évaluations des classes où il est titulaire / affecté.</summary>
    ClassHolder = 2,

    /// <summary>Toutes les classes et cours de l'établissement (année).</summary>
    Prefet = 3,

    /// <summary>Direction / Administrateur — accès complet.</summary>
    Full = 4
}

public sealed record OpenCotationSessionRequest(
    Guid AcademicYearId,
    string EmployeeNumber,
    string? Password);

public sealed record CotationSessionDto(
    Guid TeacherId,
    string EmployeeNumber,
    string TeacherDisplayName,
    CotationAccessScope AccessScope,
    Guid AcademicYearId,
    string AcademicYearLabel,
    bool PasswordValidated,
    IReadOnlyList<CotationClassDto> Classes,
    IReadOnlyList<CotationAssignmentDto> Assignments,
    IReadOnlyList<EvaluationTypeDto> EvaluationTypes);

public sealed record CotationClassDto(
    Guid ClassRoomId,
    string DisplayName,
    Guid? PedagogicalClassId,
    string? PedagogicalClassName,
    Guid SectionId,
    string SectionName,
    EducationCycle SectionCycle,
    SchoolProgram? Program,
    AcademicPeriodType PeriodType);

public sealed record CotationAssignmentDto(
    Guid AssignmentId,
    Guid ClassRoomId,
    string ClassDisplayName,
    string SectionName,
    Guid CourseId,
    string CourseName,
    Guid TeacherId,
    string TeacherDisplayName,
    int MaxScore,
    int WeeklyHours,
    int StudentCount);

public sealed record CotationPeriodDto(
    Guid Id,
    string Name,
    AcademicPeriodType PeriodType,
    int OrderIndex,
    bool IsClosed,
    AcademicSubPeriodKind Kind,
    string KindLabel,
    DateOnly? StartDate,
    DateOnly? EndDate);
