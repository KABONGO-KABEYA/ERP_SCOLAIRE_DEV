using SchoolManagement.Application.EnrollmentWizard.DTOs;

namespace SchoolManagement.Application.EnrollmentWizard;

/// <summary>
/// Catalogue compact des pièces affichées sur la fiche d'inscription (cases à cocher).
/// </summary>
public static class EnrollmentFormDocumentChecklist
{
    public static readonly string[] KnownDocuments =
    [
        "Acte de naissance",
        "Photos",
        "Pièce d'identité",
        "Bulletin",
        "Certificat médical",
        "Attestation",
        "Transfert",
        "Autres"
    ];

    public static bool IsProvided(IReadOnlyList<string> provided, string label)
    {
        if (provided.Count == 0)
        {
            return false;
        }

        foreach (var document in provided)
        {
            if (document.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (label.Equals("Photos", StringComparison.OrdinalIgnoreCase)
                && document.Contains("photo", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (label.Equals("Pièce d'identité", StringComparison.OrdinalIgnoreCase)
                && document.Contains("identité", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (label.Equals("Bulletin", StringComparison.OrdinalIgnoreCase)
                && document.Contains("bulletin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (label.Equals("Attestation", StringComparison.OrdinalIgnoreCase)
                && (document.Contains("attestation", StringComparison.OrdinalIgnoreCase)
                    || document.Contains("réussite", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (label.Equals("Transfert", StringComparison.OrdinalIgnoreCase)
                && document.Contains("transfert", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (label.Equals("Certificat médical", StringComparison.OrdinalIgnoreCase)
                && document.Contains("médical", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (label.Equals("Acte de naissance", StringComparison.OrdinalIgnoreCase)
                && document.Contains("naissance", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (label.Equals("Autres", StringComparison.OrdinalIgnoreCase)
                && document.Contains("autre", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<string> ExtraDocuments(IReadOnlyList<string> provided) =>
        provided.Where(d => !KnownDocuments.Any(k => IsProvided([d], k)));

    public static string BuildQrPayload(EnrollmentFormDocumentDto form) =>
        $"ELV:{form.RegistrationNumber}|{form.LastName}|{form.FirstName}|{form.AcademicYearLabel}";
}
