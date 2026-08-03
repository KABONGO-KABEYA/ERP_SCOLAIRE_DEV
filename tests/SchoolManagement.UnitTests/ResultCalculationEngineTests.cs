using FluentAssertions;
using SchoolManagement.Application.Grades.Calculation;
using SchoolManagement.Application.Grades.Services;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.UnitTests;

public class ResultCalculationEngineTests
{
    private readonly ResultCalculationEngine _engine = new();
    private readonly ResultCalculationRules _rules = ResultCalculationRules.CreateDefault();

    private static CourseContextInput Course(
        Guid courseId,
        string name,
        decimal coef,
        decimal targetMax,
        params EvaluationDefinitionInput[] evaluations) =>
        new(courseId, name, coef, targetMax, evaluations);

    private static EvaluationDefinitionInput Eval(
        Guid id,
        Guid courseId,
        decimal max,
        decimal weight = 1) =>
        new(id, courseId, "Cours", weight, max);

    [Fact]
    public void CalculateCourseResult_NormalScore_SumsConfiguredEvaluations()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();

        var course = Course(courseId, "Math", 1, 30,
            Eval(e1, courseId, 20),
            Eval(e2, courseId, 10));

        var scores = new[]
        {
            new ScoreEntryInput(e1, studentId, 16, ScoreEntryStatus.Scored),
            new ScoreEntryInput(e2, studentId, 8, ScoreEntryStatus.Scored)
        };

        var result = _engine.CalculateCourseResult(studentId, course, scores, _rules);

        result.Result.Should().Be(24);
        result.Maximum.Should().Be(30);
        result.Percentage.Should().Be(80);
        result.NormalizedAverage.Should().Be(24); // 24/30×30
        result.GradedCount.Should().Be(2);
        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void CalculateCourseResult_NormalizedAverage_UsesTargetMax()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();

        // DEV /20 + INT /10 = 30 ; points 13 ; barème période 20 → 13/30×20 = 8,67
        var course = Course(courseId, "Math", 1, targetMax: 20,
            Eval(e1, courseId, 20),
            Eval(e2, courseId, 10));

        var scores = new[]
        {
            new ScoreEntryInput(e1, studentId, 8, ScoreEntryStatus.Scored),
            new ScoreEntryInput(e2, studentId, 5, ScoreEntryStatus.Scored)
        };

        var result = _engine.CalculateCourseResult(studentId, course, scores, _rules);

        result.Result.Should().Be(13);
        result.Maximum.Should().Be(30);
        result.NormalizedAverage.Should().Be(8.67m);
        result.Percentage.Should().Be(43.33m);
    }

    [Fact]
    public void CalculateCourseResult_MaxScore_ProducesHundredPercent()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var course = Course(courseId, "Français", 1, 20, Eval(e1, courseId, 20));
        var scores = new[] { new ScoreEntryInput(e1, studentId, 20, ScoreEntryStatus.Scored) };

        var result = _engine.CalculateCourseResult(studentId, course, scores, _rules);

        result.Result.Should().Be(20);
        result.Percentage.Should().Be(100);
    }

    [Fact]
    public void CalculateCourseResult_ZeroScore_IsValid()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var course = Course(courseId, "Sciences", 1, 20, Eval(e1, courseId, 20));
        var scores = new[] { new ScoreEntryInput(e1, studentId, 0, ScoreEntryStatus.Scored) };

        var result = _engine.CalculateCourseResult(studentId, course, scores, _rules);

        result.Result.Should().Be(0);
        result.Percentage.Should().Be(0);
    }

    [Fact]
    public void CalculateCourseResult_AbsenceExcluded_ByDefaultRules()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();
        var course = Course(courseId, "Math", 1, 30,
            Eval(e1, courseId, 20),
            Eval(e2, courseId, 10));

        var scores = new[]
        {
            new ScoreEntryInput(e1, studentId, 16, ScoreEntryStatus.Scored),
            new ScoreEntryInput(e2, studentId, null, ScoreEntryStatus.AbsentUnjustified)
        };

        var result = _engine.CalculateCourseResult(studentId, course, scores, _rules);

        result.Result.Should().Be(16);
        result.Maximum.Should().Be(20);
        result.AbsentCount.Should().Be(1);
        result.Percentage.Should().Be(80);
    }

    [Fact]
    public void CalculateCourseResult_JustifiedAbsence_CanCountAsZero_WhenConfigured()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var course = Course(courseId, "Math", 1, 20, Eval(e1, courseId, 20));
        var rules = new ResultCalculationRules
        {
            RoundingMode = ScoreRoundingMode.TwoDecimals,
            CourseAggregationMode = CourseAggregationMode.Sum,
            UnjustifiedAbsenceMode = AbsenceContributionMode.Exclude,
            JustifiedAbsenceMode = AbsenceContributionMode.CountAsZero,
            ExcusedMode = AbsenceContributionMode.Exclude,
            DispensedMode = AbsenceContributionMode.Exclude,
            Mentions = [],
            Decision = null
        };

        var scores = new[]
        {
            new ScoreEntryInput(e1, studentId, null, ScoreEntryStatus.AbsentJustified)
        };

        var result = _engine.CalculateCourseResult(studentId, course, scores, rules);

        result.Result.Should().Be(0);
        result.Maximum.Should().Be(20);
        result.Percentage.Should().Be(0);
        result.AbsentCount.Should().Be(1);
    }

    [Fact]
    public void ValidateScores_AboveMax_ReturnsError()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var evaluations = new[] { Eval(e1, courseId, 20) };
        var scores = new[] { new ScoreEntryInput(e1, studentId, 21, ScoreEntryStatus.Scored) };

        var validation = _engine.ValidateScores(evaluations, scores, _rules);

        validation.IsValid.Should().BeFalse();
        validation.Issues.Should().Contain(i => i.Code == "ABOVE_MAX");
    }

    [Fact]
    public void ValidateScores_Negative_ReturnsError()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var evaluations = new[] { Eval(e1, courseId, 20) };
        var scores = new[] { new ScoreEntryInput(e1, studentId, -1, ScoreEntryStatus.Scored) };

        var validation = _engine.ValidateScores(evaluations, scores, _rules);

        validation.IsValid.Should().BeFalse();
        validation.Issues.Should().Contain(i => i.Code == "NEGATIVE");
    }

    [Fact]
    public void ValidateScores_Duplicates_ReturnsError()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var evaluations = new[] { Eval(e1, courseId, 20) };
        var scores = new[]
        {
            new ScoreEntryInput(e1, studentId, 10, ScoreEntryStatus.Scored),
            new ScoreEntryInput(e1, studentId, 12, ScoreEntryStatus.Scored)
        };

        var validation = _engine.ValidateScores(evaluations, scores, _rules);

        validation.IsValid.Should().BeFalse();
        validation.Issues.Should().Contain(i => i.Code == "DUPLICATE");
    }

    [Fact]
    public void CalculateRanking_HandlesTies_WithSameRank()
    {
        var s1 = new StudentResultDto(Guid.NewGuid(), "A", 16, 20, 80, 16, null,
            ClassCouncilDecision.EnAttente, 1, 0, 0, true, false, []);
        var s2 = new StudentResultDto(Guid.NewGuid(), "B", 16, 20, 80, 16, null,
            ClassCouncilDecision.EnAttente, 1, 0, 0, true, false, []);
        var s3 = new StudentResultDto(Guid.NewGuid(), "C", 10, 20, 50, 10, null,
            ClassCouncilDecision.EnAttente, 1, 0, 0, true, false, []);

        var ranking = _engine.CalculateRanking([s1, s2, s3], _rules);

        ranking.Should().HaveCount(3);
        ranking.Where(r => r.Average == 16).Should().OnlyContain(r => r.Rank == 1 && r.IsTied);
        ranking.Single(r => r.Average == 10).Rank.Should().Be(3);
        ranking.Single(r => r.Average == 10).IsTied.Should().BeFalse();
    }

    [Fact]
    public void CalculateStudentResults_NoScores_ReturnsIncomplete()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var courses = new[] { Course(courseId, "Math", 2, 20, Eval(e1, courseId, 20)) };
        var student = new StudentScoresInput(studentId, "Sans note",
        [
            new ScoreEntryInput(e1, studentId, null, ScoreEntryStatus.NotGraded)
        ]);

        var result = _engine.CalculateStudentResults(student, courses, _rules);

        result.Percentage.Should().BeNull();
        result.GradedCourseCount.Should().Be(0);
        result.NotGradedCourseCount.Should().Be(1);
        result.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void CalculateClassStatistics_EmptyClass_ReturnsZeros()
    {
        var stats = _engine.CalculateClassStatistics([], _rules);

        stats.ClassSize.Should().Be(0);
        stats.ClassAverage.Should().BeNull();
        stats.GradedStudentCount.Should().Be(0);
    }

    [Fact]
    public void RoundScore_SupportsConfiguredModes()
    {
        _engine.RoundScore(12.4m, ScoreRoundingMode.Integer).Should().Be(12);
        _engine.RoundScore(12.6m, ScoreRoundingMode.Integer).Should().Be(13);
        _engine.RoundScore(12.24m, ScoreRoundingMode.Half).Should().Be(12.0m);
        _engine.RoundScore(12.26m, ScoreRoundingMode.Half).Should().Be(12.5m);
        _engine.RoundScore(12.12m, ScoreRoundingMode.Quarter).Should().Be(12.00m);
        _engine.RoundScore(12.13m, ScoreRoundingMode.Quarter).Should().Be(12.25m);
        _engine.RoundScore(12.126m, ScoreRoundingMode.TwoDecimals).Should().Be(12.13m);
    }

    [Fact]
    public void ResolveDecision_UsesInjectedRules_OrStaysPending()
    {
        _engine.ResolveDecision(70, _rules).Should().Be(ClassCouncilDecision.EnAttente);

        var withDecision = new ResultCalculationRules
        {
            RoundingMode = ScoreRoundingMode.TwoDecimals,
            CourseAggregationMode = CourseAggregationMode.Sum,
            UnjustifiedAbsenceMode = AbsenceContributionMode.Exclude,
            JustifiedAbsenceMode = AbsenceContributionMode.Exclude,
            ExcusedMode = AbsenceContributionMode.Exclude,
            DispensedMode = AbsenceContributionMode.Exclude,
            Mentions =
            [
                new MentionThreshold(80, "Grande distinction"),
                new MentionThreshold(50, "Passable")
            ],
            Decision = new DecisionRules(50, 40, EnableExclusion: true, ExcludeMaxPercentageExclusive: 20)
        };

        _engine.ResolveDecision(55, withDecision).Should().Be(ClassCouncilDecision.Admis);
        _engine.ResolveDecision(45, withDecision).Should().Be(ClassCouncilDecision.Ajourne);
        _engine.ResolveDecision(10, withDecision).Should().Be(ClassCouncilDecision.Exclu);
        _engine.ResolveMention(85, withDecision).Should().Be("Grande distinction");
        _engine.ResolveMention(55, withDecision).Should().Be("Passable");
    }

    [Fact]
    public void CalculateCourseResult_DetectsChange_AgainstPreviousSnapshot()
    {
        var courseId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var e1 = Guid.NewGuid();
        var course = Course(courseId, "Math", 1, 20, Eval(e1, courseId, 20));
        var scores = new[] { new ScoreEntryInput(e1, studentId, 14, ScoreEntryStatus.Scored) };
        var previous = new PreviousCourseResultSnapshot(studentId, courseId, 12, 60);

        var result = _engine.CalculateCourseResult(studentId, course, scores, _rules, previous);

        result.HasChanged.Should().BeTrue();
        result.PreviousResult.Should().Be(12);
    }

    [Fact]
    public void CalculateClassStatistics_ComputesMedianAndStdDev()
    {
        var students = new[]
        {
            new StudentResultDto(Guid.NewGuid(), "A", null, null, 40, 8, null,
                ClassCouncilDecision.EnAttente, 1, 0, 0, true, false, []),
            new StudentResultDto(Guid.NewGuid(), "B", null, null, 50, 10, null,
                ClassCouncilDecision.EnAttente, 1, 0, 0, true, false, []),
            new StudentResultDto(Guid.NewGuid(), "C", null, null, 60, 12, null,
                ClassCouncilDecision.EnAttente, 1, 1, 0, false, false, [])
        };

        var stats = _engine.CalculateClassStatistics(students, _rules);

        stats.ClassAverage.Should().Be(10);
        stats.Maximum.Should().Be(12);
        stats.Minimum.Should().Be(8);
        stats.Median.Should().Be(10);
        stats.GradedStudentCount.Should().Be(3);
        stats.IncompleteStudentCount.Should().Be(1);
        stats.AbsentCount.Should().Be(1);
        stats.StandardDeviation.Should().NotBeNull();
    }

    [Fact]
    public void CalculateCourseResult_IgnoresScoresFromOtherCourses()
    {
        var mathId = Guid.NewGuid();
        var frId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var mathEval = Guid.NewGuid();
        var frEval = Guid.NewGuid();

        var math = Course(mathId, "Math", 1, 20, Eval(mathEval, mathId, 20));
        var scores = new[]
        {
            new ScoreEntryInput(mathEval, studentId, 14, ScoreEntryStatus.Scored),
            new ScoreEntryInput(frEval, studentId, 16, ScoreEntryStatus.Scored)
        };

        var result = _engine.CalculateCourseResult(studentId, math, scores, _rules);

        result.Result.Should().Be(14);
        result.ValidationErrors.Should().BeEmpty();
        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void CalculateStudentResults_MultiCourse_NoUnknownEvaluationErrors()
    {
        var mathId = Guid.NewGuid();
        var frId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var mathEval = Guid.NewGuid();
        var frEval = Guid.NewGuid();

        var courses = new[]
        {
            Course(mathId, "Math", 1, 20, Eval(mathEval, mathId, 20)),
            Course(frId, "Français", 1, 20, Eval(frEval, frId, 20))
        };
        var student = new StudentScoresInput(studentId, "Élève",
        [
            new ScoreEntryInput(mathEval, studentId, 14, ScoreEntryStatus.Scored),
            new ScoreEntryInput(frEval, studentId, 16, ScoreEntryStatus.Scored)
        ]);

        var result = _engine.CalculateStudentResults(student, courses, _rules);

        result.IsComplete.Should().BeTrue();
        result.CourseResults.Should().OnlyContain(c => c.ValidationErrors.Count == 0);
        result.Average.Should().Be(15);
    }
}
