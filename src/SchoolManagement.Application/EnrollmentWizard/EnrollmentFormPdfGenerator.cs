namespace SchoolManagement.Application.EnrollmentWizard;

using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.EnrollmentWizard.DTOs;

public static class EnrollmentFormPdfGenerator
{
    private static readonly Color PrimaryBlue = Color.FromHex("#1565C0");
    private static readonly Color LightBlue = Color.FromHex("#E3F2FD");
    private static readonly Color BorderBlue = Color.FromHex("#BBDEFB");
    private static readonly Color TextMuted = Color.FromHex("#475569");

    static EnrollmentFormPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] BuildPdfBytes(
        EnrollmentFormDocumentDto form,
        Func<string?, byte[]?> loadImage)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Colors.Black));

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Element(c => BuildHeader(c, form, loadImage));
                    column.Item().Element(c => BuildRegimeStatut(c, form));
                    column.Item().Row(row =>
                    {
                        row.Spacing(8);
                        row.RelativeItem(7).Element(c => BuildIdentityColumn(c, form, loadImage));
                        row.RelativeItem(3).Element(c => BuildSideColumn(c, form, loadImage));
                    });
                    column.Item().Element(c => BuildGuardiansSection(c, form));
                    column.Item().Element(c => BuildParentAccessSection(c, form));
                    column.Item().Element(c => BuildMedicalSection(c, form));
                    column.Item().Element(c => BuildDocumentsSection(c, form));
                    column.Item().Element(c => BuildSignatures(c, form, loadImage));
                    column.Item().Element(c => BuildAuditFooter(c, form));
                });
            });
        }).GeneratePdf();
    }

    private static void BuildHeader(IContainer container, EnrollmentFormDocumentDto form, Func<string?, byte[]?> loadImage)
    {
        container.Border(1).BorderColor(BorderBlue).Background(LightBlue).Padding(8).Column(col =>
        {
            var headerBytes = loadImage(form.Branding.HeaderImagePath) ?? loadImage(form.Branding.PrimaryLogoPath);
            if (headerBytes is not null)
            {
                col.Item().AlignCenter().Height(48).Image(headerBytes).FitArea();
            }

            col.Item().AlignCenter().Text(form.SchoolName).Bold().FontSize(12).FontColor(PrimaryBlue);
            col.Item().AlignCenter().Text("FICHE D'INSCRIPTION").Bold().FontSize(11).FontColor(PrimaryBlue);
            col.Item().AlignCenter().Text($"Année scolaire {form.AcademicYearLabel}").FontColor(TextMuted);
        });
    }

    private static void BuildRegimeStatut(IContainer container, EnrollmentFormDocumentDto form)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().Element(CellHeader).Text("Régime");
            table.Cell().Element(CellHeader).Text("Statut");
            table.Cell().Element(CellHeader).Text("Type d'inscription");
            table.Cell().Element(CellValue).Text(form.EducationRegime);
            table.Cell().Element(CellValue).Text(form.RegistrationStatut);
            table.Cell().Element(CellValue).Text(form.RegistrationKindLabel);
        });
    }

    private static void BuildIdentityColumn(IContainer container, EnrollmentFormDocumentDto form, Func<string?, byte[]?> loadImage)
    {
        container.Column(col =>
        {
            col.Spacing(4);
            col.Item().Element(c => SectionTitle(c, "Identité de l'élève"));
            col.Item().Element(c => KeyValueTable(c, new (string, string?)[]
            {
                ("Matricule", form.RegistrationNumber),
                ("Nom", form.LastName),
                ("Postnom", form.MiddleName),
                ("Prénom", form.FirstName),
                ("Sexe", form.GenderLabel),
                ("Date de naissance", form.DateOfBirth.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                ("Âge", $"{form.Age} ans"),
                ("Lieu de naissance", form.PlaceOfBirth),
                ("Nationalité", form.Nationality),
                ("Téléphone", form.Phone),
                ("Email", form.Email),
            }));

            col.Item().Element(c => SectionTitle(c, "Adresse"));
            col.Item().Element(c => KeyValueTable(c, new (string, string?)[]
            {
                ("Province", form.Province),
                ("Territoire/Ville", form.Territory),
                ("Commune", form.Commune),
                ("Avenue", form.Avenue),
                ("N° maison", form.HouseNumber),
            }));

            col.Item().Element(c => SectionTitle(c, "Scolarité"));
            col.Item().Element(c => KeyValueTable(c, new (string, string?)[]
            {
                ("Section", form.SectionName),
                ("Classe", form.ClassName),
                ("Date d'inscription", form.EnrollmentDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                ("École précédente", form.PreviousSchool),
                ("Classe précédente", form.PreviousClass),
                ("Code élève préc.", form.PreviousStudentCode),
            }));
        });
    }

    private static void BuildSideColumn(IContainer container, EnrollmentFormDocumentDto form, Func<string?, byte[]?> loadImage)
    {
        container.Border(1).BorderColor(BorderBlue).Padding(6).Column(col =>
        {
            col.Spacing(4);
            col.Item().AlignCenter().Text("Photo").SemiBold().FontColor(PrimaryBlue);

            var photoBytes = loadImage(form.PhotoPath);
            if (photoBytes is not null)
            {
                col.Item().AlignCenter().Width(90).Height(110).Image(photoBytes).FitArea();
            }
            else
            {
                col.Item().AlignCenter().Width(90).Height(110).Border(1).BorderColor(BorderBlue)
                    .AlignMiddle().AlignCenter().Text("Photo").FontColor(TextMuted);
            }

            col.Item().PaddingTop(6).AlignCenter().Text("QR Code").SemiBold().FontColor(PrimaryBlue);
            var qrPayload = $"ELV:{form.RegistrationNumber}|{form.LastName}|{form.FirstName}|{form.AcademicYearLabel}";
            var qrBytes = GenerateQrCode(qrPayload);
            col.Item().AlignCenter().Width(62).Height(62).Image(qrBytes);
        });
    }

    private static void BuildGuardiansSection(IContainer container, EnrollmentFormDocumentDto form)
    {
        container.Column(col =>
        {
            col.Item().Element(c => SectionTitle(c, "Responsables / Contacts"));
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Cell().Element(CellHeader).Text("Rôle");
                table.Cell().Element(CellHeader).Text("Nom complet");
                table.Cell().Element(CellHeader).Text("Téléphone");
                table.Cell().Element(CellHeader).Text("Email");
                table.Cell().Element(CellHeader).Text("Profession");

                foreach (var guardian in form.Guardians)
                {
                    table.Cell().Element(CellValue).Text(guardian.Relationship);
                    table.Cell().Element(CellValue).Text(guardian.FullName);
                    table.Cell().Element(CellValue).Text(guardian.Phone ?? "—");
                    table.Cell().Element(CellValue).Text(guardian.Email ?? "—");
                    table.Cell().Element(CellValue).Text(guardian.Profession ?? "—");
                }
            });
        });
    }

    private static void BuildParentAccessSection(IContainer container, EnrollmentFormDocumentDto form)
    {
        var accounts = form.ParentAccessAccounts ?? [];
        container.Column(col =>
        {
            col.Item().Element(c => SectionTitle(c, "Accès application mobile (parent)"));
            if (accounts.Count == 0)
            {
                col.Item().Text("Aucun compte d'accès parent généré pour cette inscription.").FontColor(TextMuted);
                return;
            }

            col.Item().Text("Remettre ces identifiants au responsable pour se connecter à l'application mobile.")
                .FontColor(TextMuted).FontSize(8);
            col.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                });

                table.Cell().Element(CellHeader).Text("Responsable");
                table.Cell().Element(CellHeader).Text("Identifiant");
                table.Cell().Element(CellHeader).Text("Mot de passe");
                table.Cell().Element(CellHeader).Text("Remarque");

                foreach (var account in accounts)
                {
                    var password = string.IsNullOrWhiteSpace(account.TemporaryPassword)
                        ? "———"
                        : account.TemporaryPassword;
                    var remark = !string.IsNullOrWhiteSpace(account.TemporaryPassword)
                        ? "À changer à la 1ère connexion"
                        : account.MustChangePassword
                            ? "Mot de passe déjà communiqué / à réinitialiser"
                            : "Compte existant";

                    table.Cell().Element(CellValue).Text(account.GuardianFullName);
                    table.Cell().Element(CellValue).Text(account.UserName).SemiBold();
                    table.Cell().Element(CellValue).Text(password).SemiBold();
                    table.Cell().Element(CellValue).Text(remark).FontSize(7).FontColor(TextMuted);
                }
            });
        });
    }

    private static void BuildMedicalSection(IContainer container, EnrollmentFormDocumentDto form)
    {
        container.Column(col =>
        {
            col.Item().Element(c => SectionTitle(c, "Informations médicales"));
            col.Item().Element(c => KeyValueTable(c, new (string, string?)[]
            {
                ("Groupe sanguin", form.BloodGroup),
                ("Allergies", form.Allergies),
                ("Maladies chroniques", form.ChronicDiseases),
                ("Handicap", form.Disability),
                ("Médecin", form.DoctorName),
                ("Centre médical", form.MedicalCenter),
                ("Observations", form.Observations),
            }));
        });
    }

    private static void BuildDocumentsSection(IContainer container, EnrollmentFormDocumentDto form)
    {
        container.Column(col =>
        {
            col.Item().Element(c => SectionTitle(c, "Pièces justificatives"));
            col.Item().Text(form.ProvidedDocuments.Count == 0
                ? "Aucune pièce enregistrée."
                : string.Join(", ", form.ProvidedDocuments)).FontColor(TextMuted);
        });
    }

    private static void BuildSignatures(IContainer container, EnrollmentFormDocumentDto form, Func<string?, byte[]?> loadImage)
    {
        container.PaddingTop(8).Row(row =>
        {
            row.Spacing(8);
            foreach (var signature in form.Branding.Signatures.Take(3))
            {
                row.RelativeItem().Border(1).BorderColor(BorderBlue).Padding(6).Column(col =>
                {
                    var imageBytes = loadImage(signature.ImagePath);
                    if (imageBytes is not null)
                    {
                        col.Item().AlignCenter().Height(36).Image(imageBytes).FitArea();
                    }

                    col.Item().AlignCenter().Text(signature.SignatoryName).SemiBold().FontSize(8);
                    col.Item().AlignCenter().Text(signature.Function).FontColor(TextMuted).FontSize(7);
                    col.Item().PaddingTop(12).AlignCenter().Text("Signature").FontColor(TextMuted).FontSize(7);
                });
            }
        });
    }

    private static void BuildAuditFooter(IContainer container, EnrollmentFormDocumentDto form)
    {
        container.PaddingTop(6).BorderTop(1).BorderColor(BorderBlue).Column(col =>
        {
            col.Item().Text(text =>
            {
                text.Span("Généré le ").FontColor(TextMuted);
                text.Span(form.GeneratedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                text.Span($"  •  Par {form.PrintedBy ?? "Système"}").FontColor(TextMuted);
                text.Span($"  •  Poste {form.Workstation}").FontColor(TextMuted);
                text.Span($"  •  ERP {form.ErpVersion}").FontColor(TextMuted);
            });

            if (form.Branding.Footer is not null)
            {
                var footer = form.Branding.Footer;
                var parts = new[] { footer.Address, footer.Phone, footer.Email, footer.Website }
                    .Where(p => !string.IsNullOrWhiteSpace(p));
                col.Item().Text(string.Join("  •  ", parts)).FontSize(7).FontColor(TextMuted);
            }
        });
    }

    private static void SectionTitle(IContainer container, string title) =>
        container.Background(LightBlue).Border(1).BorderColor(BorderBlue).Padding(4)
            .Text(title).SemiBold().FontSize(9).FontColor(PrimaryBlue);

    private static void KeyValueTable(IContainer container, IReadOnlyList<(string Label, string? Value)> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(95);
                columns.RelativeColumn();
            });

            foreach (var (label, value) in rows)
            {
                table.Cell().Element(CellHeader).Text(label);
                table.Cell().Element(CellValue).Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
            }
        });
    }

    private static IContainer CellHeader(IContainer container) =>
        container.BorderBottom(1).BorderColor(BorderBlue).PaddingVertical(2).PaddingHorizontal(3)
            .Background(LightBlue).DefaultTextStyle(x => x.SemiBold().FontSize(8));

    private static IContainer CellValue(IContainer container) =>
        container.BorderBottom(1).BorderColor(BorderBlue).PaddingVertical(2).PaddingHorizontal(3);

    private static byte[] GenerateQrCode(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(4);
    }
}
