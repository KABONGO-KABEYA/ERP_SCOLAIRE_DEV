namespace SchoolManagement.Application.Students;

using SchoolManagement.Domain.Exceptions;

public sealed record WithdrawalReasonOption(string Code, string Label);

public sealed record WithdrawalReasonsDto(
    IReadOnlyList<WithdrawalReasonOption> ExclusionReasons,
    IReadOnlyList<WithdrawalReasonOption> AbandonReasons);

public static class StudentWithdrawalReasons
{
    public const string CustomCode = "AUTRE";

    public static readonly IReadOnlyList<WithdrawalReasonOption> ExclusionReasons =
    [
        new("DISCIPLINE", "Manquement grave au règlement intérieur"),
        new("VIOLENCE", "Violence ou agression"),
        new("FRAUDE", "Fraude ou falsification de documents"),
        new("ASSIDUITE", "Absences répétées non justifiées"),
        new("IMPAYES", "Non-paiement persistant des frais scolaires"),
        new("AUTORITE", "Décision de l'autorité scolaire"),
        new(CustomCode, "Autre (préciser)")
    ];

    public static readonly IReadOnlyList<WithdrawalReasonOption> AbandonReasons =
    [
        new("DEMENAGEMENT", "Déménagement de la famille"),
        new("TRANSFERT", "Transfert vers un autre établissement"),
        new("SANTE", "Problème de santé"),
        new("TRAVAIL", "Travail ou activité génératrice de revenus"),
        new("GROSSESSE", "Grossesse / maternité"),
        new("DECES", "Décès d'un proche / situation familiale"),
        new("COUT", "Difficultés financières"),
        new("DESINTERET", "Désintérêt pour les études"),
        new(CustomCode, "Autre (préciser)")
    ];

    public static WithdrawalReasonsDto ToDto() =>
        new(ExclusionReasons, AbandonReasons);

    public static string ResolveLabel(StudentWithdrawalType type, string reasonCode, string? customReason)
    {
        var reasons = type == StudentWithdrawalType.Exclusion ? ExclusionReasons : AbandonReasons;
        var option = reasons.FirstOrDefault(r => string.Equals(r.Code, reasonCode, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            throw new DomainException("Code de raison invalide.");
        }

        if (string.Equals(reasonCode, CustomCode, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(customReason))
            {
                throw new DomainException("Veuillez préciser la raison.");
            }

            return customReason.Trim();
        }

        return option.Label;
    }
}

public enum StudentWithdrawalType
{
    Exclusion = 1,
    Abandon = 2
}
