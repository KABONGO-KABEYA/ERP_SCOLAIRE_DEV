using System.Net;
using System.Text;
using SchoolManagement.Application.EnrollmentWizard.DTOs;

namespace SchoolManagement.Application.EnrollmentWizard;

public static class EnrollmentFormHtmlGenerator
{
    public static byte[] BuildHtmlBytes(EnrollmentFormDocumentDto form)
    {
        var html = BuildHtml(form);
        return Encoding.UTF8.GetBytes(html);
    }

    private static string BuildHtml(EnrollmentFormDocumentDto form)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"fr\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<title>Fiche d'inscription</title>");
        sb.AppendLine("""
            <style>
            body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#111827;font-size:12px}
            h1{font-size:20px;margin:0 0 4px}
            h2{font-size:14px;margin:24px 0 8px;border-bottom:1px solid #e5e7eb;padding-bottom:4px}
            .meta{color:#6b7280;margin-bottom:16px}
            table{width:100%;border-collapse:collapse;margin-bottom:8px}
            td,th{border:1px solid #e5e7eb;padding:6px 8px;vertical-align:top}
            th{background:#f9fafb;text-align:left;width:28%}
            .footer{margin-top:24px;color:#6b7280;font-size:11px}
            </style></head><body>
            """);

        sb.Append("<h1>").Append(E(form.SchoolName)).AppendLine("</h1>");
        sb.Append("<div class=\"meta\">Fiche d'inscription — Année ")
            .Append(E(form.AcademicYearLabel))
            .Append(" — générée le ")
            .Append(form.GeneratedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"))
            .AppendLine("</div>");

        AppendSection(sb, "Identité de l'élève", [
            ("Matricule", form.RegistrationNumber),
            ("Nom complet", form.FullName),
            ("Sexe", form.GenderLabel),
            ("Date de naissance", form.DateOfBirth.ToString("dd/MM/yyyy")),
            ("Âge", form.Age.ToString()),
            ("Lieu de naissance", form.PlaceOfBirth),
            ("Nationalité", form.Nationality),
            ("Téléphone", form.Phone),
            ("Email", form.Email),
        ]);

        AppendSection(sb, "Adresse", [
            ("Province", form.Province),
            ("Territoire", form.Territory),
            ("Commune", form.Commune),
            ("Avenue", form.Avenue),
            ("N° maison", form.HouseNumber),
        ]);

        AppendSection(sb, "Scolarité", [
            ("Classe", form.ClassName),
            ("Section", form.SectionName),
            ("Régime", form.EducationRegime),
            ("Statut", form.RegistrationStatut),
            ("Type d'inscription", form.RegistrationKindLabel),
            ("Date d'inscription", form.EnrollmentDate.ToString("dd/MM/yyyy")),
            ("École précédente", form.PreviousSchool),
            ("Classe précédente", form.PreviousClass),
        ]);

        AppendGuardian(sb, "Père", form.Father);
        AppendGuardian(sb, "Mère", form.Mother);
        AppendGuardian(sb, "Responsable légal", form.LegalGuardian);

        AppendSection(sb, "Santé", [
            ("Groupe sanguin", form.BloodGroup),
            ("Allergies", form.Allergies),
            ("Maladies chroniques", form.ChronicDiseases),
            ("Handicap", form.Disability),
            ("Médecin", form.DoctorName),
            ("Centre médical", form.MedicalCenter),
            ("Observations", form.Observations),
        ]);

        if (form.ProvidedDocuments.Count > 0)
        {
            sb.AppendLine("<h2>Documents fournis</h2><ul>");
            foreach (var doc in form.ProvidedDocuments)
            {
                sb.Append("<li>").Append(E(doc)).AppendLine("</li>");
            }

            sb.AppendLine("</ul>");
        }

        AppendSection(sb, "Frais d'inscription", [
            ("Montant", form.RegistrationFee?.ToString("N2")),
            ("Payé", form.AmountPaid.ToString("N2")),
            ("Devise", form.Currency),
            ("Solde", form.BalanceDue?.ToString("N2")),
        ]);

        sb.Append("<div class=\"footer\">Imprimé par ")
            .Append(E(form.PrintedBy ?? "Système"))
            .Append(" — Poste ")
            .Append(E(form.Workstation))
            .Append(" — ERP ")
            .Append(E(form.ErpVersion))
            .AppendLine("</div>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string title, (string Label, string? Value)[] rows)
    {
        sb.Append("<h2>").Append(E(title)).AppendLine("</h2><table>");
        foreach (var (label, value) in rows)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            sb.Append("<tr><th>").Append(E(label)).Append("</th><td>").Append(E(value)).AppendLine("</td></tr>");
        }

        sb.AppendLine("</table>");
    }

    private static void AppendGuardian(StringBuilder sb, string title, EnrollmentFormGuardianDto? guardian)
    {
        if (guardian is null)
        {
            return;
        }

        AppendSection(sb, title, [
            ("Nom", guardian.FullName),
            ("Lien", guardian.Relationship),
            ("Téléphone", guardian.Phone),
            ("Email", guardian.Email),
            ("Adresse", guardian.Address),
            ("Profession", guardian.Profession),
        ]);
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
