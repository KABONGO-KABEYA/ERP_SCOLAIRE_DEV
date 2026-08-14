namespace SchoolManagement.Application.EnrollmentWizard;

using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.DocumentBranding;
using SchoolManagement.Application.EnrollmentWizard.DTOs;

public static class EnrollmentFormPdfGenerator
{
    private static readonly Color PrimaryBlue = Color.FromHex("#1565C0");
    private static readonly Color LightBlue = Color.FromHex("#E3F2FD");
    private static readonly Color BorderBlue = Color.FromHex("#BBDEFB");
    private static readonly Color TextMuted = Color.FromHex("#475569");
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

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
                page.MarginHorizontal(16);
                page.MarginVertical(12);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Black));

                page.Content().Column(column =>
                {
                    column.Spacing(3);
                    column.Item().Element(c => BuildHeader(c, form, loadImage));
                    column.Item().Element(c => BuildRegimeStatut(c, form));
                    column.Item().Row(row =>
                    {
                        row.Spacing(6);
                        row.RelativeItem(7).Element(c => BuildMainColumn(c, form));
                        row.RelativeItem(3).Element(c => BuildSideColumn(c, form, loadImage));
                    });
                    column.Item().Element(c => BuildSignatures(c, form, loadImage));
                    column.Item().Element(c => BuildAuditFooter(c, form));
                });
            });
        }).GeneratePdf();
    }

    private static void BuildHeader(IContainer container, EnrollmentFormDocumentDto form, Func<string?, byte[]?> loadImage)
    {
        container.Border(1).BorderColor(BorderBlue).Background(LightBlue).Padding(4).Column(col =>
        {
            col.Spacing(1);
            if (!DocumentPrintHeaderComposer.TryComposeFullWidthImage(col.Item(), form.Branding, loadImage))
            {
                var headerBytes = loadImage(form.Branding.HeaderImagePath) ?? loadImage(form.Branding.PrimaryLogoPath);
                if (headerBytes is not null)
                {
                    col.Item().MaxHeight(42).Image(headerBytes).FitUnproportionally();
                }
            }

            col.Item().AlignCenter().Text(form.SchoolName).Bold().FontSize(10).FontColor(PrimaryBlue);
            col.Item().AlignCenter().Text("FICHE D'INSCRIPTION").Bold().FontSize(10).FontColor(PrimaryBlue);
            col.Item().AlignCenter().Text($"Année scolaire {form.AcademicYearLabel}").FontSize(8).FontColor(TextMuted);
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

    private static void BuildMainColumn(IContainer container, EnrollmentFormDocumentDto form)
    {
        container.Column(col =>
        {
            col.Spacing(3);
            col.Item().Element(c => SectionTitle(c, "1. Identification de l'élève"));
            col.Item().Element(c => CompactTwoColTable(c,
            [
                ("Matricule", form.RegistrationNumber),
                ("Sexe", form.GenderLabel),
                ("Nom", form.LastName),
                ("Date naissance", form.DateOfBirth.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                ("Postnom", form.MiddleName),
                ("Âge", $"{form.Age} ans"),
                ("Prénom", form.FirstName),
                ("Lieu naissance", form.PlaceOfBirth),
                ("Nationalité", form.Nationality),
                ("Téléphone", form.Phone),
                ("Email", form.Email),
                ("", null),
            ]));

            col.Item().Element(c => SectionTitle(c, "2. Adresse"));
            col.Item().Element(c => CompactTwoColTable(c,
            [
                ("Province", form.Province),
                ("Territoire/Ville", form.Territory),
                ("Commune", form.Commune),
                ("Avenue", form.Avenue),
                ("N° maison", form.HouseNumber),
                ("", null),
            ]));

            col.Item().Element(c => SectionTitle(c, "3. Scolarité"));
            col.Item().Element(c => CompactTwoColTable(c,
            [
                ("Section", form.SectionName),
                ("Classe", form.ClassName),
                ("Date inscription", form.EnrollmentDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                ("École préc.", form.PreviousSchool),
                ("Classe préc.", form.PreviousClass),
                ("Code élève préc.", form.PreviousStudentCode),
            ]));

            col.Item().Element(c => BuildGuardiansSection(c, form));
            col.Item().Element(c => BuildParentAccessSection(c, form));
            col.Item().Element(c => SectionTitle(c, "6. Informations médicales"));
            col.Item().Element(c => CompactTwoColTable(c,
            [
                ("Groupe sanguin", form.BloodGroup),
                ("Allergies", form.Allergies),
                ("Maladies chroniques", form.ChronicDiseases),
                ("Handicap", form.Disability),
                ("Médecin", form.DoctorName),
                ("Centre médical", form.MedicalCenter),
            ]));

            col.Item().Element(c => SectionTitle(c, "7. Pièces justificatives"));
            col.Item().Element(c => BuildDocumentsChecklist(c, form));

            col.Item().Element(c => SectionTitle(c, "8. Observations"));
            col.Item().Border(1).BorderColor(BorderBlue).Padding(3).MinHeight(18)
                .Text(string.IsNullOrWhiteSpace(form.Observations) ? "—" : form.Observations)
                .FontSize(7.5f).FontColor(TextMuted);
        });
    }

    private static void BuildSideColumn(IContainer container, EnrollmentFormDocumentDto form, Func<string?, byte[]?> loadImage)
    {
        container.Column(col =>
        {
            col.Spacing(3);

            col.Item().Border(1).BorderColor(BorderBlue).Padding(4).Column(photo =>
            {
                photo.Item().AlignCenter().Text("Photo").SemiBold().FontSize(8).FontColor(PrimaryBlue);
                var photoBytes = loadImage(form.PhotoPath);
                if (photoBytes is not null)
                {
                    photo.Item().AlignCenter().Width(72).Height(88).Image(photoBytes).FitArea();
                }
                else
                {
                    photo.Item().AlignCenter().Width(72).Height(88).Border(1).BorderColor(BorderBlue)
                        .AlignMiddle().AlignCenter().Text("Photo").FontSize(7).FontColor(TextMuted);
                }
            });

            col.Item().Border(1).BorderColor(BorderBlue).Padding(4).Column(id =>
            {
                id.Spacing(2);
                id.Item().AlignCenter().Text("Identification").SemiBold().FontSize(8).FontColor(PrimaryBlue);
                id.Item().AlignCenter().Text(form.RegistrationNumber).Bold().FontSize(9);
                id.Item().AlignCenter().Text("QR Code").SemiBold().FontSize(7).FontColor(PrimaryBlue);
                var qrBytes = GenerateQrCode(EnrollmentFormDocumentChecklist.BuildQrPayload(form));
                id.Item().AlignCenter().Width(54).Height(54).Image(qrBytes);
            });

            col.Item().Border(1).BorderColor(BorderBlue).Padding(4).Column(fin =>
            {
                fin.Spacing(1);
                fin.Item().AlignCenter().Text("Informations financières").SemiBold().FontSize(8).FontColor(PrimaryBlue);
                fin.Item().Element(c => KeyValueTable(c,
                [
                    ("Frais d'inscription", FormatMoney(form.RegistrationFee, form.Currency)),
                    ("Montant payé", FormatMoney(form.AmountPaid, form.Currency)),
                    ("Devise", string.IsNullOrWhiteSpace(form.Currency) ? "—" : form.Currency),
                    ("Solde", FormatMoney(form.BalanceDue, form.Currency)),
                ]));
            });
        });
    }

    private static void BuildGuardiansSection(IContainer container, EnrollmentFormDocumentDto form)
    {
        container.Column(col =>
        {
            col.Item().Element(c => SectionTitle(c, "4. Responsables / Contacts"));
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.4f);
                    columns.RelativeColumn(2.2f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.6f);
                });

                table.Cell().Element(CellHeader).Text("Rôle");
                table.Cell().Element(CellHeader).Text("Nom complet");
                table.Cell().Element(CellHeader).Text("Téléphone");
                table.Cell().Element(CellHeader).Text("Email");
                table.Cell().Element(CellHeader).Text("Profession");

                var rows = form.Guardians.Take(4).ToList();
                if (rows.Count == 0)
                {
                    table.Cell().ColumnSpan(5).Element(CellValue).Text("Aucun responsable enregistré.").FontColor(TextMuted);
                    return;
                }

                foreach (var guardian in rows)
                {
                    table.Cell().Element(CellValue).Text(guardian.Relationship).FontSize(7);
                    table.Cell().Element(CellValue).Text(guardian.FullName).FontSize(7);
                    table.Cell().Element(CellValue).Text(guardian.Phone ?? "—").FontSize(7);
                    table.Cell().Element(CellValue).Text(guardian.Email ?? "—").FontSize(7);
                    table.Cell().Element(CellValue).Text(guardian.Profession ?? "—").FontSize(7);
                }
            });
        });
    }

    private static void BuildParentAccessSection(IContainer container, EnrollmentFormDocumentDto form)
    {
        var accounts = form.ParentAccessAccounts ?? [];
        container.Column(col =>
        {
            col.Item().Element(c => SectionTitle(c, "5. Accès application mobile (parent)"));
            if (accounts.Count == 0)
            {
                col.Item().Text("Aucun compte d'accès parent généré.").FontSize(7).FontColor(TextMuted);
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(2.2f);
                });

                table.Cell().Element(CellHeader).Text("Responsable");
                table.Cell().Element(CellHeader).Text("Identifiant");
                table.Cell().Element(CellHeader).Text("Mot de passe");
                table.Cell().Element(CellHeader).Text("Remarque");

                foreach (var account in accounts.Take(3))
                {
                    var password = string.IsNullOrWhiteSpace(account.TemporaryPassword)
                        ? "———"
                        : account.TemporaryPassword;
                    var remark = !string.IsNullOrWhiteSpace(account.TemporaryPassword)
                        ? "À changer à la 1ère connexion"
                        : account.MustChangePassword
                            ? "Mot de passe déjà communiqué / à réinitialiser"
                            : "Compte existant";

                    table.Cell().Element(CellValue).Text(account.GuardianFullName).FontSize(7);
                    table.Cell().Element(CellValue).Text(account.UserName).SemiBold().FontSize(7);
                    table.Cell().Element(CellValue).Text(password).SemiBold().FontSize(7);
                    table.Cell().Element(CellValue).Text(remark).FontSize(6.5f).FontColor(TextMuted);
                }
            });
        });
    }

    private static void BuildDocumentsChecklist(IContainer container, EnrollmentFormDocumentDto form)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            foreach (var label in EnrollmentFormDocumentChecklist.KnownDocuments)
            {
                var mark = EnrollmentFormDocumentChecklist.IsProvided(form.ProvidedDocuments, label) ? "☑" : "☐";
                table.Cell().Element(CellValue).Text($"{mark} {label}").FontSize(7);
            }

            var extras = EnrollmentFormDocumentChecklist.ExtraDocuments(form.ProvidedDocuments).Take(4).ToList();
            foreach (var extra in extras)
            {
                table.Cell().Element(CellValue).Text($"☑ {extra}").FontSize(7);
            }
        });
    }

    private static void BuildSignatures(IContainer container, EnrollmentFormDocumentDto form, Func<string?, byte[]?> loadImage)
    {
        var signatures = form.Branding.Signatures.Take(3).ToList();
        if (signatures.Count == 0)
        {
            container.PaddingTop(2).Row(row =>
            {
                row.Spacing(6);
                foreach (var title in new[] { "Parents / Tuteur", "Secrétariat", "Direction" })
                {
                    row.RelativeItem().Border(1).BorderColor(BorderBlue).Padding(4).Column(col =>
                    {
                        col.Item().AlignCenter().Text(title).SemiBold().FontSize(7);
                        col.Item().PaddingTop(18).AlignCenter().Text("Signature").FontSize(6.5f).FontColor(TextMuted);
                    });
                }
            });
            return;
        }

        container.PaddingTop(2).Row(row =>
        {
            row.Spacing(6);
            foreach (var signature in signatures)
            {
                row.RelativeItem().Border(1).BorderColor(BorderBlue).Padding(4).Column(col =>
                {
                    var imageBytes = loadImage(signature.ImagePath);
                    if (imageBytes is not null)
                    {
                        col.Item().AlignCenter().Height(28).Image(imageBytes).FitArea();
                    }
                    else
                    {
                        col.Item().Height(14);
                    }

                    col.Item().AlignCenter().Text(signature.SignatoryName).SemiBold().FontSize(7);
                    col.Item().AlignCenter().Text(signature.Function).FontColor(TextMuted).FontSize(6.5f);
                    col.Item().PaddingTop(8).AlignCenter().Text("Signature").FontColor(TextMuted).FontSize(6.5f);
                });
            }
        });
    }

    private static void BuildAuditFooter(IContainer container, EnrollmentFormDocumentDto form)
    {
        container.BorderTop(1).BorderColor(BorderBlue).PaddingTop(2).Column(col =>
        {
            col.Item().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(6.5f).FontColor(TextMuted));
                text.Span("Généré le ");
                text.Span(form.GeneratedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                text.Span($"  •  Par {form.PrintedBy ?? "Système"}");
                text.Span($"  •  Poste {form.Workstation}");
                text.Span($"  •  ERP {form.ErpVersion}");
            });

            if (form.Branding.Footer is not null)
            {
                var footer = form.Branding.Footer;
                var parts = new[] { footer.Address, footer.Phone, footer.Email, footer.Website }
                    .Where(p => !string.IsNullOrWhiteSpace(p));
                col.Item().Text(string.Join("  •  ", parts)).FontSize(6.5f).FontColor(TextMuted);
            }
        });
    }

    private static string FormatMoney(decimal? amount, string? currency)
    {
        if (amount is null)
        {
            return "—";
        }

        var cur = string.IsNullOrWhiteSpace(currency) ? "" : $" {currency}";
        return $"{amount.Value.ToString("N0", Fr)}{cur}";
    }

    private static void SectionTitle(IContainer container, string title) =>
        container.Background(LightBlue).Border(1).BorderColor(BorderBlue).PaddingVertical(2).PaddingHorizontal(4)
            .Text(title).SemiBold().FontSize(8).FontColor(PrimaryBlue);

    private static void CompactTwoColTable(IContainer container, IReadOnlyList<(string Label, string? Value)> pairs)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(72);
                columns.RelativeColumn();
                columns.ConstantColumn(78);
                columns.RelativeColumn();
            });

            for (var i = 0; i < pairs.Count; i += 2)
            {
                var left = pairs[i];
                if (string.IsNullOrEmpty(left.Label))
                {
                    table.Cell().ColumnSpan(2).Element(EmptyCell);
                }
                else
                {
                    table.Cell().Element(CellHeader).Text(left.Label).FontSize(7);
                    table.Cell().Element(CellValue).Text(Display(left.Value)).FontSize(7.5f);
                }

                if (i + 1 < pairs.Count)
                {
                    var right = pairs[i + 1];
                    if (string.IsNullOrEmpty(right.Label))
                    {
                        table.Cell().ColumnSpan(2).Element(EmptyCell);
                    }
                    else
                    {
                        table.Cell().Element(CellHeader).Text(right.Label).FontSize(7);
                        table.Cell().Element(CellValue).Text(Display(right.Value)).FontSize(7.5f);
                    }
                }
            }
        });
    }

    private static void KeyValueTable(IContainer container, IReadOnlyList<(string Label, string? Value)> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn();
            });

            foreach (var (label, value) in rows)
            {
                table.Cell().Element(CellHeader).Text(label).FontSize(6.5f);
                table.Cell().Element(CellValue).Text(Display(value)).FontSize(7);
            }
        });
    }

    private static string Display(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static IContainer CellHeader(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(BorderBlue).PaddingVertical(1).PaddingHorizontal(2)
            .Background(LightBlue).DefaultTextStyle(x => x.SemiBold().FontSize(7));

    private static IContainer CellValue(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(BorderBlue).PaddingVertical(1).PaddingHorizontal(2);

    private static IContainer EmptyCell(IContainer container) =>
        container.Padding(0);

    private static byte[] GenerateQrCode(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(4);
    }
}
