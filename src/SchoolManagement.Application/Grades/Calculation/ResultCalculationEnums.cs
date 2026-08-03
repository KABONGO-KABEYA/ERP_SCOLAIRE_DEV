namespace SchoolManagement.Application.Grades.Calculation;

/// <summary>Statut d'une cote individuelle (fourni par les données / commentaires, jamais déduit en dur).</summary>
public enum ScoreEntryStatus
{
    Scored = 1,
    AbsentUnjustified = 2,
    AbsentJustified = 3,
    Excused = 4,
    Dispensed = 5,
    NotGraded = 6
}

/// <summary>Mode d'arrondi paramétrable.</summary>
public enum ScoreRoundingMode
{
    Integer = 1,
    Half = 2,
    Quarter = 3,
    TwoDecimals = 4
}

/// <summary>Comportement d'une absence / dispense dans le calcul (paramétrable).</summary>
public enum AbsenceContributionMode
{
    /// <summary>Exclure du total et du maximum.</summary>
    Exclude = 1,

    /// <summary>Compter comme 0 en conservant le maximum.</summary>
    CountAsZero = 2,

    /// <summary>Traiter comme non coté (incomplet), exclure du total.</summary>
    TreatAsNotGraded = 3
}

/// <summary>Agrégation des évaluations d'un cours.</summary>
public enum CourseAggregationMode
{
    /// <summary>Résultat = Σ notes / Σ maxima (évaluations réellement configurées).</summary>
    Sum = 1,

    /// <summary>Moyenne pondérée par Evaluation.Weight, normalisée sur TargetMax.</summary>
    WeightedNormalized = 2
}
