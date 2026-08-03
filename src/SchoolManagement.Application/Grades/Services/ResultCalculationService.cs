namespace SchoolManagement.Application.Grades.Services;

using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Grades.Calculation;
using SchoolManagement.Application.Grades.Interfaces;

/// <summary>
/// Service unique de calcul des résultats — point d'entrée pour cotation, vue globale,
/// bulletin, classement, statistiques, dashboard, mobile et API.
/// </summary>
public sealed class ResultCalculationService : IResultCalculationService
{
    private readonly IResultCalculationEngine _engine;
    private readonly ILogger<ResultCalculationService> _logger;

    public ResultCalculationService(
        IResultCalculationEngine engine,
        ILogger<ResultCalculationService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public IResultCalculationEngine Engine => _engine;

    public ResultCalculationRules GetDefaultRules() => ResultCalculationRules.CreateDefault();

    public ScoreValidationResult ValidateScores(
        IReadOnlyList<EvaluationDefinitionInput> evaluations,
        IReadOnlyList<ScoreEntryInput> scores,
        ResultCalculationRules? rules = null)
    {
        var effective = rules ?? GetDefaultRules();
        var result = _engine.ValidateScores(evaluations, scores, effective);
        if (!result.IsValid)
        {
            _logger.LogDebug(
                "ValidateScores: {IssueCount} anomalie(s) détectée(s).",
                result.Issues.Count);
        }

        return result;
    }

    public CourseResultDto CalculateCourseResult(
        Guid studentId,
        CourseContextInput course,
        IReadOnlyList<ScoreEntryInput> studentScores,
        ResultCalculationRules? rules = null,
        PreviousCourseResultSnapshot? previous = null,
        decimal pedagogicalBonusPoints = 0)
    {
        var effective = rules ?? GetDefaultRules();
        var result = _engine.CalculateCourseResult(
            studentId, course, studentScores, effective, previous, pedagogicalBonusPoints);
        _logger.LogDebug(
            "CalculateCourseResult student={StudentId} course={CourseId} result={Result}/{Max} pct={Percentage} changed={Changed}",
            studentId,
            course.CourseId,
            result.Result,
            result.Maximum,
            result.Percentage,
            result.HasChanged);
        return result;
    }

    public StudentResultDto CalculateStudentResults(
        StudentScoresInput student,
        IReadOnlyList<CourseContextInput> courses,
        ResultCalculationRules? rules = null,
        IReadOnlyList<PreviousCourseResultSnapshot>? previousCourseResults = null)
    {
        var effective = rules ?? GetDefaultRules();
        var result = _engine.CalculateStudentResults(student, courses, effective, previousCourseResults);
        _logger.LogDebug(
            "CalculateStudentResults student={StudentId} avg={Average} pct={Percentage} courses={Graded}/{Total} changed={Changed}",
            student.StudentId,
            result.Average,
            result.Percentage,
            result.GradedCourseCount,
            courses.Count,
            result.HasChanged);
        return result;
    }

    public ClassStatisticsDto CalculateClassStatistics(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules? rules = null)
    {
        var effective = rules ?? GetDefaultRules();
        var result = _engine.CalculateClassStatistics(studentResults, effective);
        _logger.LogDebug(
            "CalculateClassStatistics size={Size} graded={Graded} avg={Average} max={Max} min={Min}",
            result.ClassSize,
            result.GradedStudentCount,
            result.ClassAverage,
            result.Maximum,
            result.Minimum);
        return result;
    }

    public IReadOnlyList<RankingEntryDto> CalculateRanking(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules? rules = null)
    {
        var effective = rules ?? GetDefaultRules();
        var ranking = _engine.CalculateRanking(studentResults, effective);
        _logger.LogDebug("CalculateRanking entries={Count}", ranking.Count);
        return ranking;
    }

    public ClassRecalculationResult RecalculateClass(
        IReadOnlyList<StudentScoresInput> students,
        IReadOnlyList<CourseContextInput> courses,
        ResultCalculationRules? rules = null,
        IReadOnlyList<PreviousCourseResultSnapshot>? previousCourseResults = null)
    {
        ArgumentNullException.ThrowIfNull(students);
        ArgumentNullException.ThrowIfNull(courses);

        var effective = rules ?? GetDefaultRules();
        _logger.LogDebug(
            "RecalculateClass start students={StudentCount} courses={CourseCount}",
            students.Count,
            courses.Count);

        var studentResults = students
            .Select(s => _engine.CalculateStudentResults(s, courses, effective, previousCourseResults))
            .ToList();

        var statistics = _engine.CalculateClassStatistics(studentResults, effective);
        var ranking = _engine.CalculateRanking(studentResults, effective);

        _logger.LogDebug(
            "RecalculateClass done graded={Graded} classAvg={Average} changedStudents={Changed}",
            statistics.GradedStudentCount,
            statistics.ClassAverage,
            studentResults.Count(s => s.HasChanged));

        return new ClassRecalculationResult(studentResults, statistics, ranking);
    }
}
