namespace SchoolManagement.Application.Grades.Interfaces;

using SchoolManagement.Application.Grades.Calculation;
using SchoolManagement.Domain.Enums;

/// <summary>
/// Moteur pur de calcul des résultats (indépendant de l'UI et de la persistance).
/// Toutes les valeurs métier (barèmes, absences, arrondis, mentions) sont fournies via <see cref="ResultCalculationRules"/>.
/// </summary>
public interface IResultCalculationEngine
{
    decimal RoundScore(decimal value, ScoreRoundingMode mode);

    ScoreValidationResult ValidateScores(
        IReadOnlyList<EvaluationDefinitionInput> evaluations,
        IReadOnlyList<ScoreEntryInput> scores,
        ResultCalculationRules rules);

    CourseResultDto CalculateCourseResult(
        Guid studentId,
        CourseContextInput course,
        IReadOnlyList<ScoreEntryInput> studentScores,
        ResultCalculationRules rules,
        PreviousCourseResultSnapshot? previous = null,
        decimal pedagogicalBonusPoints = 0);

    StudentResultDto CalculateStudentResults(
        StudentScoresInput student,
        IReadOnlyList<CourseContextInput> courses,
        ResultCalculationRules rules,
        IReadOnlyList<PreviousCourseResultSnapshot>? previousCourseResults = null);

    ClassStatisticsDto CalculateClassStatistics(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules rules);

    IReadOnlyList<RankingEntryDto> CalculateRanking(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules rules);

    decimal? CalculateCourseAverage(
        IReadOnlyList<CourseResultDto> courseResultsForSameCourse,
        ResultCalculationRules rules);

    decimal? CalculateClassAverage(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules rules);

    ClassCouncilDecision ResolveDecision(decimal? percentage, ResultCalculationRules rules);

    string? ResolveMention(decimal? percentage, ResultCalculationRules rules);
}

/// <summary>
/// Façade applicative : journalisation, cascade de recalcul, détection de changements.
/// Consommée par GradeService, bulletins, classements, API, mobile.
/// </summary>
public interface IResultCalculationService
{
    IResultCalculationEngine Engine { get; }

    ResultCalculationRules GetDefaultRules();

    ScoreValidationResult ValidateScores(
        IReadOnlyList<EvaluationDefinitionInput> evaluations,
        IReadOnlyList<ScoreEntryInput> scores,
        ResultCalculationRules? rules = null);

    CourseResultDto CalculateCourseResult(
        Guid studentId,
        CourseContextInput course,
        IReadOnlyList<ScoreEntryInput> studentScores,
        ResultCalculationRules? rules = null,
        PreviousCourseResultSnapshot? previous = null,
        decimal pedagogicalBonusPoints = 0);

    StudentResultDto CalculateStudentResults(
        StudentScoresInput student,
        IReadOnlyList<CourseContextInput> courses,
        ResultCalculationRules? rules = null,
        IReadOnlyList<PreviousCourseResultSnapshot>? previousCourseResults = null);

    ClassStatisticsDto CalculateClassStatistics(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules? rules = null);

    IReadOnlyList<RankingEntryDto> CalculateRanking(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules? rules = null);

    /// <summary>
    /// Cascade : cours → élève → statistiques de classe → classement,
    /// en ne recalculant que les élèves fournis (données déjà chargées).
    /// </summary>
    ClassRecalculationResult RecalculateClass(
        IReadOnlyList<StudentScoresInput> students,
        IReadOnlyList<CourseContextInput> courses,
        ResultCalculationRules? rules = null,
        IReadOnlyList<PreviousCourseResultSnapshot>? previousCourseResults = null);
}

public sealed record ClassRecalculationResult(
    IReadOnlyList<StudentResultDto> Students,
    ClassStatisticsDto Statistics,
    IReadOnlyList<RankingEntryDto> Ranking);
