namespace SchoolManagement.Application.Grades.Calculation;

using SchoolManagement.Domain.Enums;

/// <summary>
/// Règles de calcul injectées (DB / configuration).
/// Aucune valeur métier n'est codée dans le moteur : tout passe par cet objet.
/// </summary>
public sealed class ResultCalculationRules
{
    public required ScoreRoundingMode RoundingMode { get; init; }

    public required CourseAggregationMode CourseAggregationMode { get; init; }

    public required AbsenceContributionMode UnjustifiedAbsenceMode { get; init; }

    public required AbsenceContributionMode JustifiedAbsenceMode { get; init; }

    public required AbsenceContributionMode ExcusedMode { get; init; }

    public required AbsenceContributionMode DispensedMode { get; init; }

    /// <summary>Seuils de mention triés par pourcentage minimal décroissant. Liste vide = pas de mention.</summary>
    public IReadOnlyList<MentionThreshold> Mentions { get; init; } = [];

    /// <summary>Null = décision toujours « Non délibéré » / EnAttente.</summary>
    public DecisionRules? Decision { get; init; }

    /// <summary>
    /// Fabrication d'un jeu de règles de démarrage.
    /// Les valeurs viennent d'ici (ou plus tard de la DB), jamais du cœur du moteur.
    /// </summary>
    public static ResultCalculationRules CreateDefault() => new()
    {
        RoundingMode = ScoreRoundingMode.TwoDecimals,
        CourseAggregationMode = CourseAggregationMode.Sum,
        UnjustifiedAbsenceMode = AbsenceContributionMode.Exclude,
        JustifiedAbsenceMode = AbsenceContributionMode.Exclude,
        ExcusedMode = AbsenceContributionMode.Exclude,
        DispensedMode = AbsenceContributionMode.Exclude,
        Mentions = [],
        Decision = null
    };
}

/// <summary>Seuils de mention. MaxPercentageInclusive = borne haute inclusive (ex. 69 pour Satisfaction).</summary>
public sealed record MentionThreshold(
    decimal MinPercentageInclusive,
    string Label,
    decimal? MaxPercentageInclusive = null);

public sealed record DecisionRules(
    decimal AdmitMinPercentageInclusive,
    decimal DeferMinPercentageInclusive,
    bool EnableExclusion,
    decimal? ExcludeMaxPercentageExclusive);

public sealed record EvaluationDefinitionInput(
    Guid EvaluationId,
    Guid CourseId,
    string CourseName,
    decimal Weight,
    decimal MaxScore,
    Guid? ScopedEnrollmentId = null);

public sealed record ScoreEntryInput(
    Guid EvaluationId,
    Guid StudentId,
    decimal? Score,
    ScoreEntryStatus Status);

public sealed record CourseContextInput(
    Guid CourseId,
    string CourseName,
    decimal Coefficient,
    /// <summary>Barème cible du cours (affectation / cours / période) pour normalisation pondérée.</summary>
    decimal TargetMaxScore,
    IReadOnlyList<EvaluationDefinitionInput> Evaluations);

public sealed record StudentScoresInput(
    Guid StudentId,
    string StudentName,
    IReadOnlyList<ScoreEntryInput> Scores,
    /// <summary>Bonus pédagogiques du conseil (CourseId → points), hors notes d'origine.</summary>
    IReadOnlyDictionary<Guid, decimal>? CourseBonusPoints = null);

public sealed record PreviousCourseResultSnapshot(
    Guid StudentId,
    Guid CourseId,
    decimal? Result,
    decimal? Percentage);

public sealed record CourseResultDto(
    Guid StudentId,
    Guid CourseId,
    string CourseName,
    /// <summary>TOTAL : somme des points obtenus (évaluations contribuantes).</summary>
    decimal? Result,
    /// <summary>Somme des maxima des évaluations contribuantes.</summary>
    decimal? Maximum,
    decimal? Percentage,
    /// <summary>
    /// MOYENNE normalisée : Total / Σ maxima × TargetMaxScore du cours / période.
    /// </summary>
    decimal? NormalizedAverage,
    string? Mention,
    int GradedCount,
    int AbsentCount,
    int NotGradedCount,
    bool IsComplete,
    bool HasChanged,
    decimal? PreviousResult,
    decimal? PreviousPercentage,
    IReadOnlyList<string> ValidationErrors);

public sealed record StudentResultDto(
    Guid StudentId,
    string StudentName,
    decimal? TotalObtained,
    decimal? TotalMaximum,
    decimal? Percentage,
    decimal? Average,
    string? Mention,
    ClassCouncilDecision Decision,
    int GradedCourseCount,
    int AbsentCount,
    int NotGradedCourseCount,
    bool IsComplete,
    bool HasChanged,
    IReadOnlyList<CourseResultDto> CourseResults);

public sealed record ClassStatisticsDto(
    decimal? ClassAverage,
    decimal? Maximum,
    decimal? Minimum,
    decimal? Median,
    decimal? StandardDeviation,
    int GradedStudentCount,
    int IncompleteStudentCount,
    int AbsentCount,
    int ClassSize);

public sealed record RankingEntryDto(
    Guid StudentId,
    string StudentName,
    decimal? Average,
    decimal? Percentage,
    int Rank,
    bool IsTied);

public sealed record ScoreValidationIssue(
    Guid? StudentId,
    Guid? EvaluationId,
    string Code,
    string Message);

public sealed record ScoreValidationResult(
    bool IsValid,
    IReadOnlyList<ScoreValidationIssue> Issues);
