namespace SchoolManagement.Application.Bulletins.DTOs;

using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Domain.Enums;

/// <summary>
/// DTOs d'affichage des bulletins.
/// Aucune valeur métier n'est calculée ici : les scores / rangs / mentions
/// proviennent exclusivement de <c>IResultCalculationService</c> (via la couche Grades).
/// </summary>
public sealed record IndividualBulletinRequest(
    Guid AcademicYearId,
    Guid ClassRoomId,
    Guid StudentId,
    PedagogicalSheetPeriodMode Mode,
    Guid PeriodId);

public sealed record ClassBulletinsRequest(
    Guid AcademicYearId,
    Guid ClassRoomId,
    PedagogicalSheetPeriodMode Mode,
    Guid PeriodId);

/// <summary>
/// Bulletin d'un élève — projection d'affichage (pas de calcul).
/// S'appuie sur les résultats moteur déjà agrégés (équivalent résultat individuel).
/// </summary>
public sealed record IndividualBulletinDto(
    Guid StudentId,
    string RegistrationNumber,
    string StudentName,
    string? PhotoPath,
    string ClassDisplayName,
    string AcademicYearLabel,
    string PeriodLabel,
    string AverageDisplay,
    string PercentageDisplay,
    string? Mention,
    ClassCouncilDecision Decision,
    string DecisionLabel,
    string RankDisplay,
    IReadOnlyList<IndividualBulletinCourseLineDto> Courses,
    /// <summary>Horodatage éventuel de dernière impression (historique).</summary>
    DateTimeOffset? LastPrintedAtUtc);

public sealed record IndividualBulletinCourseLineDto(
    Guid CourseId,
    string CourseName,
    string MaximumDisplay,
    string TotalObtainedDisplay,
    string ResultDisplay,
    string? Mention,
    string Observation);

/// <summary>Lot de bulletins pour une classe (génération / lot d'impression).</summary>
public sealed record ClassBulletinsBatchDto(
    Guid ClassRoomId,
    string ClassDisplayName,
    string AcademicYearLabel,
    string PeriodLabel,
    IReadOnlyList<IndividualBulletinDto> Bulletins);

/// <summary>Entrée d'historique / réimpression — métadonnées uniquement.</summary>
public sealed record BulletinPrintHistoryDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string RegistrationNumber,
    Guid ClassRoomId,
    string ClassDisplayName,
    string PeriodLabel,
    DateTimeOffset PrintedAtUtc,
    string PrintedByUserName,
    bool IsReprint);

public sealed record RecordBulletinPrintRequest(
    Guid AcademicYearId,
    Guid ClassRoomId,
    Guid StudentId,
    PedagogicalSheetPeriodMode Mode,
    Guid PeriodId,
    bool IsReprint);
