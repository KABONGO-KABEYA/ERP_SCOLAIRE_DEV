namespace SchoolManagement.Application.Grades.Services;

using SchoolManagement.Application.Grades.Calculation;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

/// <summary>
/// Moteur de calcul des résultats — logique pure, sans accès DB ni UI.
/// </summary>
public sealed class ResultCalculationEngine : IResultCalculationEngine
{
    public decimal RoundScore(decimal value, ScoreRoundingMode mode) =>
        mode switch
        {
            ScoreRoundingMode.Integer => Math.Round(value, 0, MidpointRounding.AwayFromZero),
            ScoreRoundingMode.Half => RoundToStep(value, 0.5m),
            ScoreRoundingMode.Quarter => RoundToStep(value, 0.25m),
            ScoreRoundingMode.TwoDecimals => Math.Round(value, 2, MidpointRounding.AwayFromZero),
            _ => throw new DomainException($"Mode d'arrondi inconnu : {mode}.")
        };

    public ScoreValidationResult ValidateScores(
        IReadOnlyList<EvaluationDefinitionInput> evaluations,
        IReadOnlyList<ScoreEntryInput> scores,
        ResultCalculationRules rules)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(rules);

        var issues = new List<ScoreValidationIssue>();
        var evalMap = evaluations.ToDictionary(e => e.EvaluationId);

        var duplicates = scores
            .GroupBy(s => (s.EvaluationId, s.StudentId))
            .Where(g => g.Count() > 1);

        foreach (var dup in duplicates)
        {
            issues.Add(new ScoreValidationIssue(
                dup.Key.StudentId,
                dup.Key.EvaluationId,
                "DUPLICATE",
                "Doublon de cote pour le même élève et la même évaluation."));
        }

        foreach (var score in scores)
        {
            if (!evalMap.TryGetValue(score.EvaluationId, out var evaluation))
            {
                issues.Add(new ScoreValidationIssue(
                    score.StudentId,
                    score.EvaluationId,
                    "UNKNOWN_EVALUATION",
                    "Évaluation inconnue pour cette cote."));
                continue;
            }

            if (evaluation.MaxScore <= 0)
            {
                issues.Add(new ScoreValidationIssue(
                    score.StudentId,
                    score.EvaluationId,
                    "INVALID_MAX",
                    $"Le maximum de l'évaluation doit être supérieur à 0 (cours « {evaluation.CourseName} »)."));
            }

            if (score.Status != ScoreEntryStatus.Scored)
            {
                continue;
            }

            if (score.Score is null)
            {
                issues.Add(new ScoreValidationIssue(
                    score.StudentId,
                    score.EvaluationId,
                    "MISSING_SCORE",
                    "Une cote marquée comme notée n'a pas de valeur."));
                continue;
            }

            if (score.Score < 0)
            {
                issues.Add(new ScoreValidationIssue(
                    score.StudentId,
                    score.EvaluationId,
                    "NEGATIVE",
                    "Les notes négatives sont interdites."));
            }

            if (evaluation.MaxScore > 0 && score.Score > evaluation.MaxScore)
            {
                issues.Add(new ScoreValidationIssue(
                    score.StudentId,
                    score.EvaluationId,
                    "ABOVE_MAX",
                    $"La note {score.Score} dépasse le maximum /{evaluation.MaxScore}."));
            }
        }

        return new ScoreValidationResult(issues.Count == 0, issues);
    }

    public CourseResultDto CalculateCourseResult(
        Guid studentId,
        CourseContextInput course,
        IReadOnlyList<ScoreEntryInput> studentScores,
        ResultCalculationRules rules,
        PreviousCourseResultSnapshot? previous = null,
        decimal pedagogicalBonusPoints = 0)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(studentScores);
        ArgumentNullException.ThrowIfNull(rules);

        // Ne valider / lire que les cotes du cours courant.
        // studentScores contient souvent toutes les évaluations de la classe (RecalculateClass) :
        // les traiter comme « inconnues » produisait de faux positifs bloquants.
        var courseEvalIds = course.Evaluations
            .Where(e => e.CourseId == course.CourseId)
            .Select(e => e.EvaluationId)
            .ToHashSet();
        var courseScores = studentScores
            .Where(s => courseEvalIds.Contains(s.EvaluationId))
            .ToList();

        var validation = ValidateScores(course.Evaluations, courseScores, rules);
        var errors = validation.Issues
            .Where(i => i.StudentId == studentId || i.StudentId is null)
            .Select(i => i.Message)
            .Distinct()
            .ToList();

        var scoreByEval = courseScores
            .Where(s => s.StudentId == studentId)
            .GroupBy(s => s.EvaluationId)
            .ToDictionary(g => g.Key, g => g.First());

        decimal obtained = 0;
        decimal maximum = 0;
        decimal weightedSum = 0;
        decimal totalWeight = 0;
        var graded = 0;
        var absent = 0;
        var notGraded = 0;
        var contributing = 0;

        foreach (var evaluation in course.Evaluations)
        {
            if (evaluation.CourseId != course.CourseId)
            {
                continue;
            }

            if (!scoreByEval.TryGetValue(evaluation.EvaluationId, out var entry)
                || entry.Status == ScoreEntryStatus.NotGraded)
            {
                notGraded++;
                continue;
            }

            var mode = ResolveAbsenceMode(entry.Status, rules);
            if (entry.Status is ScoreEntryStatus.AbsentUnjustified
                or ScoreEntryStatus.AbsentJustified
                or ScoreEntryStatus.Excused
                or ScoreEntryStatus.Dispensed)
            {
                absent++;
            }

            if (mode == AbsenceContributionMode.TreatAsNotGraded
                || (mode == AbsenceContributionMode.Exclude
                    && entry.Status != ScoreEntryStatus.Scored))
            {
                if (entry.Status != ScoreEntryStatus.Scored)
                {
                    notGraded++;
                }

                continue;
            }

            var scoreValue = entry.Status == ScoreEntryStatus.Scored
                ? entry.Score ?? 0
                : mode == AbsenceContributionMode.CountAsZero ? 0 : 0;

            if (entry.Status == ScoreEntryStatus.Scored)
            {
                graded++;
            }

            contributing++;
            var evalMax = evaluation.MaxScore > 0 ? evaluation.MaxScore : 0;
            var weight = evaluation.Weight <= 0 ? 1 : evaluation.Weight;

            if (rules.CourseAggregationMode == CourseAggregationMode.Sum)
            {
                obtained += scoreValue;
                maximum += evalMax;
            }
            else
            {
                var targetMax = course.TargetMaxScore > 0 ? course.TargetMaxScore : evalMax;
                var normalized = evalMax > 0
                    ? scoreValue / evalMax * targetMax
                    : scoreValue;
                weightedSum += normalized * weight;
                totalWeight += weight;
            }
        }

        decimal? result;
        decimal? max;
        decimal? percentage;
        decimal? normalizedAverage;

        if (contributing == 0)
        {
            result = null;
            max = null;
            percentage = null;
            normalizedAverage = null;
        }
        else if (rules.CourseAggregationMode == CourseAggregationMode.Sum)
        {
            var bonus = pedagogicalBonusPoints > 0 ? pedagogicalBonusPoints : 0m;
            var obtainedWithBonus = obtained + bonus;
            if (maximum > 0 && obtainedWithBonus > maximum)
            {
                obtainedWithBonus = maximum;
            }

            result = RoundScore(obtainedWithBonus, rules.RoundingMode);
            max = maximum;
            percentage = maximum > 0
                ? RoundScore(obtainedWithBonus / maximum * 100m, rules.RoundingMode)
                : null;
            // Moyenne pédagogique : points / Σ maxima × barème prévu (TargetMaxScore).
            normalizedAverage = maximum > 0 && course.TargetMaxScore > 0
                ? RoundScore(obtainedWithBonus / maximum * course.TargetMaxScore, rules.RoundingMode)
                : null;
        }
        else
        {
            var average = totalWeight > 0 ? weightedSum / totalWeight : 0;
            var bonus = pedagogicalBonusPoints > 0 ? pedagogicalBonusPoints : 0m;
            average += bonus;
            if (course.TargetMaxScore > 0 && average > course.TargetMaxScore)
            {
                average = course.TargetMaxScore;
            }

            result = RoundScore(average, rules.RoundingMode);
            max = course.TargetMaxScore > 0 ? course.TargetMaxScore : null;
            percentage = max is > 0
                ? RoundScore(average / max.Value * 100m, rules.RoundingMode)
                : null;
            normalizedAverage = result;
        }

        var mention = ResolveMention(percentage, rules);
        var previousResult = previous is { StudentId: var ps, CourseId: var pc }
            && ps == studentId && pc == course.CourseId
            ? previous.Result
            : null;
        var previousPct = previous is { StudentId: var ps2, CourseId: var pc2 }
            && ps2 == studentId && pc2 == course.CourseId
            ? previous.Percentage
            : null;

        var hasChanged = previous is not null
            && (previousResult != result || previousPct != percentage);

        return new CourseResultDto(
            studentId,
            course.CourseId,
            course.CourseName,
            result,
            max,
            percentage,
            normalizedAverage,
            mention,
            graded,
            absent,
            notGraded,
            notGraded == 0 && course.Evaluations.Count > 0,
            hasChanged,
            previousResult,
            previousPct,
            errors);
    }

    public StudentResultDto CalculateStudentResults(
        StudentScoresInput student,
        IReadOnlyList<CourseContextInput> courses,
        ResultCalculationRules rules,
        IReadOnlyList<PreviousCourseResultSnapshot>? previousCourseResults = null)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(courses);
        ArgumentNullException.ThrowIfNull(rules);

        var previousMap = (previousCourseResults ?? [])
            .Where(p => p.StudentId == student.StudentId)
            .ToDictionary(p => p.CourseId);

        var courseResults = new List<CourseResultDto>(courses.Count);
        decimal obtainedSum = 0;
        decimal maximumSum = 0;
        decimal weightedPctSum = 0;
        decimal weightedScaleSum = 0;
        decimal coefSum = 0;
        var gradedCourses = 0;
        var absentCount = 0;
        var notGradedCourses = 0;

        foreach (var course in courses)
        {
            previousMap.TryGetValue(course.CourseId, out var prev);
            var bonus = 0m;
            if (student.CourseBonusPoints is not null)
            {
                student.CourseBonusPoints.TryGetValue(course.CourseId, out bonus);
            }

            var courseResult = CalculateCourseResult(
                student.StudentId,
                course,
                student.Scores,
                rules,
                prev,
                bonus);

            courseResults.Add(courseResult);
            absentCount += courseResult.AbsentCount;

            if (courseResult.Result is null)
            {
                notGradedCourses++;
                continue;
            }

            gradedCourses++;
            var coef = course.Coefficient <= 0 ? 1 : course.Coefficient;
            coefSum += coef;
            weightedScaleSum += (courseResult.NormalizedAverage ?? courseResult.Result ?? 0) * coef;

            if (courseResult.Maximum is > 0)
            {
                obtainedSum += courseResult.Result!.Value * coef;
                maximumSum += courseResult.Maximum.Value * coef;
            }

            if (courseResult.Percentage is not null)
            {
                weightedPctSum += courseResult.Percentage.Value * coef;
            }
        }

        decimal? percentage = coefSum > 0 && gradedCourses > 0
            ? RoundScore(weightedPctSum / coefSum, rules.RoundingMode)
            : null;

        // Moyenne générale : moyenne pondérée des moyennes normalisées de cours.
        decimal? average = null;
        if (gradedCourses > 0 && coefSum > 0)
        {
            average = RoundScore(weightedScaleSum / coefSum, rules.RoundingMode);
        }

        var totalObtained = gradedCourses > 0 ? RoundScore(obtainedSum, rules.RoundingMode) : (decimal?)null;
        var totalMaximum = gradedCourses > 0 ? maximumSum : (decimal?)null;

        var mention = ResolveMention(percentage, rules);
        var decision = ResolveDecision(percentage, rules);
        var hasChanged = courseResults.Any(c => c.HasChanged);

        return new StudentResultDto(
            student.StudentId,
            student.StudentName,
            totalObtained,
            totalMaximum,
            percentage,
            average,
            mention,
            decision,
            gradedCourses,
            absentCount,
            notGradedCourses,
            notGradedCourses == 0 && courses.Count > 0,
            hasChanged,
            courseResults);
    }

    public ClassStatisticsDto CalculateClassStatistics(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules rules)
    {
        ArgumentNullException.ThrowIfNull(studentResults);
        ArgumentNullException.ThrowIfNull(rules);

        var classSize = studentResults.Count;
        if (classSize == 0)
        {
            return new ClassStatisticsDto(null, null, null, null, null, 0, 0, 0, 0);
        }

        var graded = studentResults
            .Select(s => s.Average ?? s.Percentage)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .OrderBy(v => v)
            .ToList();

        var incomplete = studentResults.Count(s => !s.IsComplete);
        var absents = studentResults.Sum(s => s.AbsentCount);

        if (graded.Count == 0)
        {
            return new ClassStatisticsDto(null, null, null, null, null, 0, incomplete, absents, classSize);
        }

        var avg = RoundScore(graded.Average(), rules.RoundingMode);
        var max = graded.Max();
        var min = graded.Min();
        var median = ComputeMedian(graded);
        var stdDev = ComputeStdDev(graded);

        return new ClassStatisticsDto(
            avg,
            max,
            min,
            median is null ? null : RoundScore(median.Value, rules.RoundingMode),
            stdDev is null ? null : RoundScore(stdDev.Value, rules.RoundingMode),
            graded.Count,
            incomplete,
            absents,
            classSize);
    }

    public IReadOnlyList<RankingEntryDto> CalculateRanking(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules rules)
    {
        ArgumentNullException.ThrowIfNull(studentResults);
        ArgumentNullException.ThrowIfNull(rules);

        var ordered = studentResults
            .OrderByDescending(s => s.Average ?? s.Percentage ?? decimal.MinValue)
            .ThenBy(s => s.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var result = new List<RankingEntryDto>(ordered.Count);
        var index = 0;
        while (index < ordered.Count)
        {
            var current = ordered[index];
            var rank = index + 1;
            var tieEnd = index;
            while (tieEnd + 1 < ordered.Count
                   && ValuesEqual(
                       ordered[tieEnd + 1].Average ?? ordered[tieEnd + 1].Percentage,
                       current.Average ?? current.Percentage))
            {
                tieEnd++;
            }

            var isTied = tieEnd > index;
            for (var i = index; i <= tieEnd; i++)
            {
                var s = ordered[i];
                result.Add(new RankingEntryDto(
                    s.StudentId,
                    s.StudentName,
                    s.Average,
                    s.Percentage,
                    rank,
                    isTied));
            }

            index = tieEnd + 1;
        }

        return result;
    }

    public decimal? CalculateCourseAverage(
        IReadOnlyList<CourseResultDto> courseResultsForSameCourse,
        ResultCalculationRules rules)
    {
        ArgumentNullException.ThrowIfNull(courseResultsForSameCourse);
        ArgumentNullException.ThrowIfNull(rules);

        var values = courseResultsForSameCourse
            .Where(c => c.Percentage is not null)
            .Select(c => c.Percentage!.Value)
            .ToList();

        return values.Count == 0
            ? null
            : RoundScore(values.Average(), rules.RoundingMode);
    }

    public decimal? CalculateClassAverage(
        IReadOnlyList<StudentResultDto> studentResults,
        ResultCalculationRules rules) =>
        CalculateClassStatistics(studentResults, rules).ClassAverage;

    public ClassCouncilDecision ResolveDecision(decimal? percentage, ResultCalculationRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (percentage is null || rules.Decision is null)
        {
            return ClassCouncilDecision.EnAttente;
        }

        var d = rules.Decision;
        if (d.EnableExclusion
            && d.ExcludeMaxPercentageExclusive is decimal excludeMax
            && percentage < excludeMax)
        {
            return ClassCouncilDecision.Exclu;
        }

        if (percentage >= d.AdmitMinPercentageInclusive)
        {
            return ClassCouncilDecision.Admis;
        }

        if (percentage >= d.DeferMinPercentageInclusive)
        {
            return ClassCouncilDecision.Ajourne;
        }

        return ClassCouncilDecision.EnAttente;
    }

    public string? ResolveMention(decimal? percentage, ResultCalculationRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (percentage is null || rules.Mentions.Count == 0)
        {
            return null;
        }

        var pct = percentage.Value;
        return rules.Mentions
            .OrderByDescending(m => m.MinPercentageInclusive)
            .FirstOrDefault(m =>
                pct >= m.MinPercentageInclusive
                && (m.MaxPercentageInclusive is null || pct <= m.MaxPercentageInclusive.Value))
            ?.Label;
    }

    private static AbsenceContributionMode ResolveAbsenceMode(
        ScoreEntryStatus status,
        ResultCalculationRules rules) =>
        status switch
        {
            ScoreEntryStatus.Scored => AbsenceContributionMode.CountAsZero, // unused for scored
            ScoreEntryStatus.AbsentUnjustified => rules.UnjustifiedAbsenceMode,
            ScoreEntryStatus.AbsentJustified => rules.JustifiedAbsenceMode,
            ScoreEntryStatus.Excused => rules.ExcusedMode,
            ScoreEntryStatus.Dispensed => rules.DispensedMode,
            ScoreEntryStatus.NotGraded => AbsenceContributionMode.TreatAsNotGraded,
            _ => AbsenceContributionMode.Exclude
        };

    private static decimal RoundToStep(decimal value, decimal step)
    {
        if (step <= 0)
        {
            throw new DomainException("Le pas d'arrondi doit être positif.");
        }

        return Math.Round(value / step, 0, MidpointRounding.AwayFromZero) * step;
    }

    private static decimal? ComputeMedian(IReadOnlyList<decimal> ordered)
    {
        if (ordered.Count == 0)
        {
            return null;
        }

        var mid = ordered.Count / 2;
        if (ordered.Count % 2 == 1)
        {
            return ordered[mid];
        }

        return (ordered[mid - 1] + ordered[mid]) / 2m;
    }

    private static decimal? ComputeStdDev(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        if (values.Count == 1)
        {
            return 0m;
        }

        var avg = values.Average();
        var variance = values.Sum(v => (v - avg) * (v - avg)) / values.Count;
        return (decimal)Math.Sqrt((double)variance);
    }

    private static bool ValuesEqual(decimal? a, decimal? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.Value == b.Value;
    }
}
