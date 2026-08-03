namespace SchoolManagement.Application.Grades.Calculation;

/// <summary>
/// Mappe les cotes persistées (IsAbsent + commentaire) vers un statut de calcul.
/// Les codes ABS / DISP / EXC sont lus depuis les données saisies, pas depuis une règle métier du moteur.
/// </summary>
public static class ScoreEntryStatusMapper
{
    public static ScoreEntryStatus FromGradeEntry(bool isAbsent, string? comment, decimal? score)
    {
        if (!isAbsent && score is null)
        {
            return ScoreEntryStatus.NotGraded;
        }

        if (!isAbsent)
        {
            return ScoreEntryStatus.Scored;
        }

        var code = (comment ?? string.Empty).Trim().ToUpperInvariant();
        return code switch
        {
            "DISP" => ScoreEntryStatus.Dispensed,
            "EXC" => ScoreEntryStatus.Excused,
            "ABS-J" or "AJ" or "JUSTIFIEE" or "JUSTIFIÉE" => ScoreEntryStatus.AbsentJustified,
            "ABS-I" or "AI" or "INJUSTIFIEE" or "INJUSTIFIÉE" => ScoreEntryStatus.AbsentUnjustified,
            "ABS" => ScoreEntryStatus.AbsentUnjustified,
            _ => ScoreEntryStatus.AbsentUnjustified
        };
    }

    public static ScoreEntryInput ToInput(
        Guid evaluationId,
        Guid studentId,
        decimal score,
        bool isAbsent,
        string? comment)
    {
        var status = FromGradeEntry(isAbsent, comment, isAbsent ? null : score);
        return new ScoreEntryInput(
            evaluationId,
            studentId,
            status == ScoreEntryStatus.Scored ? score : null,
            status);
    }
}
