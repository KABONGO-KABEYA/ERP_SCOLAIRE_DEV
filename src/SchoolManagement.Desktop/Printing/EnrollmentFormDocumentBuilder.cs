using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Printing;

public static class EnrollmentFormDocumentBuilder
{
    // A4 @ 96 DPI — marges ~12 mm
    private const double PageWidth = 794;
    private const double PageHeight = 1123;
    private const double PageMargin = 45;

    private const double ContentWidth = PageWidth - PageMargin * 2;
    private const double MainColWidth = ContentWidth * 0.70;
    private const double SideColWidth = ContentWidth * 0.30;

    private const double LeftInnerWidth = MainColWidth - 2;
    private const double LabelColWidth = 88;
    private const double ValueColWidth = LeftInnerWidth / 2 - LabelColWidth;

    // Photo ~3,5 × 4,5 cm
    private const double PhotoWidth = 132;
    private const double PhotoHeight = 170;
    private const double QrSize = 62;

    private const double FontBody = 8.5;
    private const double FontSmall = 7.75;
    private const double FontSection = 8.75;
    private const double FontTitle = 12;
    private const double FontMatricule = 10.5;
    private const double CellPad = 3;
    private const double SectionGap = 3;

    private static readonly Brush PrimaryBlue = new SolidColorBrush(Color.FromRgb(21, 101, 192));
    private static readonly Brush LightBlueBg = new SolidColorBrush(Color.FromRgb(227, 242, 253));
    private static readonly Brush BorderBlue = new SolidColorBrush(Color.FromRgb(187, 222, 251));
    private static readonly Brush TextMuted = new SolidColorBrush(Color.FromRgb(71, 85, 105));
    private static readonly Brush UnpaidRed = new SolidColorBrush(Color.FromRgb(220, 38, 38));
    private static readonly FontFamily UiFont = new("Segoe UI");

    public static FlowDocument Build(
        EnrollmentFormDocumentDto form,
        IDocumentBrandingPathResolver brandingPathResolver,
        IStudentDossierPathResolver dossierPathResolver)
    {
        var document = new FlowDocument
        {
            PageWidth = PageWidth,
            PageHeight = PageHeight,
            PagePadding = new Thickness(PageMargin),
            FontFamily = UiFont,
            FontSize = FontBody,
            ColumnWidth = double.PositiveInfinity,
            LineHeight = 11
        };

        var page = CreateTable();
        page.Columns.Add(Col(MainColWidth));
        page.Columns.Add(Col(SideColWidth));
        var body = RowGroup();

        body.Rows.Add(FullRow(BuildHeader(form, brandingPathResolver)));
        body.Rows.Add(FullRow(BuildRegimeStatut(form)));
        body.Rows.Add(TwoColRow(BuildLeftColumn(form), BuildRightColumn(form, dossierPathResolver)));
        body.Rows.Add(FullRow(BuildSignatures(form, brandingPathResolver)));
        body.Rows.Add(FullRow(BuildAuditFooter(form)));
        body.Rows.Add(FullRow(BuildSchoolFooter(form)));

        page.RowGroups.Add(body);
        document.Blocks.Add(page);
        return document;
    }

    private static Block BuildHeader(
        EnrollmentFormDocumentDto form,
        IDocumentBrandingPathResolver brandingPathResolver)
    {
        var wrapper = BorderedSection(new Thickness(0, 0, 0, SectionGap));
        var headerPath = brandingPathResolver.ResolveAbsolutePath(form.Branding.HeaderImagePath)
            ?? brandingPathResolver.ResolveAbsolutePath(form.Branding.PrimaryLogoPath);

        if (headerPath is not null)
        {
            var image = CreateImage(headerPath, ContentWidth - 8, 58, Stretch.Uniform);
            if (image is not null)
            {
                wrapper.Blocks.Add(new Paragraph(new InlineUIContainer(image))
                {
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(CellPad, CellPad, CellPad, 0),
                    LineHeight = 1
                });
            }
        }

        wrapper.Blocks.Add(new Paragraph(new Run("FICHE D'INSCRIPTION"))
        {
            FontSize = FontTitle,
            FontWeight = FontWeights.Bold,
            Foreground = PrimaryBlue,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, CellPad),
            LineHeight = 14
        });

        return wrapper;
    }

    private static Block BuildRegimeStatut(EnrollmentFormDocumentDto form)
    {
        var table = CreateTable();
        table.Columns.Add(Col(ContentWidth / 2));
        table.Columns.Add(Col(ContentWidth / 2));
        var group = RowGroup();
        var row = new TableRow();
        row.Cells.Add(BorderedCell(CreateCheckboxGroup(
            "Régime :",
            [
                ("Maternelle", form.EducationRegime == "Maternelle"),
                ("Primaire", form.EducationRegime == "Primaire"),
                ("Secondaire", form.EducationRegime == "Secondaire")
            ])));
        row.Cells.Add(BorderedCell(CreateCheckboxGroup(
            "Statut :",
            [
                ("Nouveau", form.RegistrationStatut == "Nouveau"),
                ("Ancien élève", form.RegistrationStatut == "Ancien élève"),
                ("Transfert", form.RegistrationStatut == "Transfert")
            ])));
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        table.Margin = new Thickness(0, 0, 0, SectionGap);
        return table;
    }

    private static Block BuildLeftColumn(EnrollmentFormDocumentDto form)
    {
        var section = new Section { Margin = new Thickness(0, 0, 3, 0) };
        section.Blocks.Add(SectionBlock("1. IDENTIFICATION DE L'ÉLÈVE", BuildIdentityGrid(form)));
        section.Blocks.Add(SectionBlock("2. FILIATION", BuildFiliationGrid(form)));
        section.Blocks.Add(SectionBlock("3. INFORMATIONS SCOLAIRES", BuildScolariteGrid(form)));
        section.Blocks.Add(SectionBlock("4. DOCUMENTS FOURNIS", BuildDocumentsGrid(form)));
        section.Blocks.Add(SectionBlock("5. RENSEIGNEMENTS MÉDICAUX", BuildMedicalGrid(form)));
        section.Blocks.Add(SectionBlock("6. OBSERVATIONS", BuildObservationsBlock(form)));
        return section;
    }

    private static Block BuildRightColumn(
        EnrollmentFormDocumentDto form,
        IStudentDossierPathResolver dossierPathResolver)
    {
        var section = new Section { Margin = new Thickness(3, 0, 0, 0) };
        section.Blocks.Add(SidebarPanel("PHOTO DE L'ÉLÈVE", BuildPhotoBlock(form, dossierPathResolver)));
        section.Blocks.Add(SidebarPanel("IDENTIFICATION", BuildIdentificationBlock(form)));
        section.Blocks.Add(SidebarPanel("INFORMATIONS FINANCIÈRES", BuildFinancialBlock(form)));
        return section;
    }

    private static Block BuildIdentityGrid(EnrollmentFormDocumentDto form)
    {
        var table = TwoColumnFieldTable();
        AddTwoColRow(table, ("Nom", form.LastName), ("Province", form.Province ?? "—"));
        AddTwoColRow(table, ("Postnom", form.MiddleName ?? "—"), ("Territoire", form.Territory ?? "—"));
        AddTwoColRow(table, ("Prénom", form.FirstName), ("Commune", form.Commune ?? "—"));
        AddTwoColRow(table, ("Sexe", form.GenderLabel), ("Quartier", "—"));
        AddTwoColRow(table, ("Date naissance", form.DateOfBirth.ToString("dd/MM/yyyy")), ("Avenue", form.Avenue ?? "—"));
        AddTwoColRow(table, ("Lieu naissance", form.PlaceOfBirth ?? "—"), ("N° maison", form.HouseNumber ?? "—"));
        AddTwoColRow(table, ("Nationalité", form.Nationality ?? "—"), ("Téléphone", form.Phone ?? "—"));
        return table;
    }

    private static Block BuildFiliationGrid(EnrollmentFormDocumentDto form)
    {
        var section = new Section { Margin = new Thickness(0) };

        section.Blocks.Add(SubBlockTitle("PÈRE"));
        section.Blocks.Add(BuildGuardianFields(
            form.Father?.FullName ?? "—",
            form.Father?.Profession ?? "—",
            form.Father?.Phone ?? "—"));

        section.Blocks.Add(SubBlockTitle("MÈRE"));
        section.Blocks.Add(BuildGuardianFields(
            form.Mother?.FullName ?? "—",
            form.Mother?.Profession ?? "—",
            form.Mother?.Phone ?? "—"));

        section.Blocks.Add(SubBlockTitle("RESPONSABLE LÉGAL"));
        var legalTable = TwoColumnFieldTable();
        AddTwoColRow(legalTable,
            ("Nom", form.LegalGuardian?.FullName ?? "—"),
            ("Lien", form.LegalGuardian?.Relationship ?? "—"));
        AddTwoColRow(legalTable,
            ("Téléphone", form.LegalGuardian?.Phone ?? "—"),
            ("Adresse", form.LegalGuardian?.Address ?? "—"));
        AddTwoColRow(legalTable,
            ("E-mail", form.LegalGuardian?.Email ?? "—"),
            ("", ""));
        section.Blocks.Add(legalTable);

        return section;
    }

    private static Block BuildGuardianFields(string name, string profession, string phone)
    {
        var table = TwoColumnFieldTable();
        AddTwoColRow(table, ("Nom", name), ("Profession", profession));
        AddTwoColRow(table, ("Téléphone", phone), ("", ""));
        return table;
    }

    private static Block BuildScolariteGrid(EnrollmentFormDocumentDto form)
    {
        var table = TwoColumnFieldTable();
        AddTwoColRow(table,
            ("Année scolaire", form.AcademicYearLabel),
            ("Section", form.SectionName ?? "—"));
        AddTwoColRow(table,
            ("Classe / Local", form.ClassName),
            ("Type inscription", form.RegistrationKindLabel));
        AddTwoColRow(table,
            ("Date inscription", form.EnrollmentDate.ToString("dd/MM/yyyy")),
            ("Code élève ant.", form.PreviousStudentCode ?? "—"));
        AddTwoColRow(table,
            ("École provenance", form.PreviousSchool ?? "—"),
            ("Classe antérieure", form.PreviousClass ?? form.ClassName));
        return table;
    }

    private static Block BuildDocumentsGrid(EnrollmentFormDocumentDto form)
    {
        var knownDocuments = new[]
        {
            "Acte de naissance", "Photos", "Pièce d'identité", "Bulletin",
            "Certificat médical", "Attestation de réussite", "Certificat de transfert", "Autres"
        };

        var table = CreateTable();
        table.Columns.Add(Col(LeftInnerWidth / 2));
        table.Columns.Add(Col(LeftInnerWidth / 2));
        var group = RowGroup();

        for (var i = 0; i < knownDocuments.Length; i += 2)
        {
            var row = new TableRow();
            row.Cells.Add(DocCell(knownDocuments[i], IsDocumentProvided(form.ProvidedDocuments, knownDocuments[i])));
            row.Cells.Add(i + 1 < knownDocuments.Length
                ? DocCell(knownDocuments[i + 1], IsDocumentProvided(form.ProvidedDocuments, knownDocuments[i + 1]))
                : EmptyCell());
            group.Rows.Add(row);
        }

        var extras = form.ProvidedDocuments
            .Where(d => !knownDocuments.Contains(d, StringComparer.OrdinalIgnoreCase))
            .ToList();
        for (var i = 0; i < extras.Count; i += 2)
        {
            var row = new TableRow();
            row.Cells.Add(DocCell(extras[i], true));
            row.Cells.Add(i + 1 < extras.Count ? DocCell(extras[i + 1], true) : EmptyCell());
            group.Rows.Add(row);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static Block BuildMedicalGrid(EnrollmentFormDocumentDto form)
    {
        var table = TwoColumnFieldTable();
        AddTwoColRow(table,
            ("Groupe sanguin", form.BloodGroup ?? "—"),
            ("Allergies", form.Allergies ?? "Aucune"));
        AddTwoColRow(table,
            ("Maladies connues", form.ChronicDiseases ?? "Aucune"),
            ("Handicap", form.Disability ?? "Aucun"));
        AddTwoColRow(table,
            ("Médecin traitant", form.DoctorName ?? "—"),
            ("Téléphone", form.MedicalCenter ?? "—"));
        return table;
    }

    private static Block BuildObservationsBlock(EnrollmentFormDocumentDto form)
    {
        var table = CreateTable();
        table.Columns.Add(Col(LeftInnerWidth));
        var group = RowGroup();

        if (!string.IsNullOrWhiteSpace(form.Observations))
        {
            var textRow = new TableRow();
            textRow.Cells.Add(new TableCell(new Paragraph(new Run(form.Observations.Trim()))
            {
                FontSize = FontBody,
                Margin = new Thickness(0),
                LineHeight = 11
            })
            {
                Padding = new Thickness(CellPad, CellPad, CellPad, 2)
            });
            group.Rows.Add(textRow);
        }

        for (var i = 0; i < 3; i++)
        {
            var lineRow = new TableRow();
            lineRow.Cells.Add(new TableCell(new Paragraph(new Run(" "))
            {
                Margin = new Thickness(0),
                LineHeight = 16
            })
            {
                Padding = new Thickness(CellPad, 4, CellPad, 4),
                BorderBrush = BorderBlue,
                BorderThickness = new Thickness(0, 0, 0, 1)
            });
            group.Rows.Add(lineRow);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static Block BuildPhotoBlock(
        EnrollmentFormDocumentDto form,
        IStudentDossierPathResolver dossierPathResolver)
    {
        var photoPath = dossierPathResolver.ResolveAbsolutePath(form.PhotoPath);
        if (photoPath is not null)
        {
            var photo = CreateImage(photoPath, PhotoWidth, PhotoHeight, Stretch.UniformToFill);
            if (photo is not null)
            {
                return new Paragraph(new InlineUIContainer(photo))
                {
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(CellPad),
                    LineHeight = 1
                };
            }
        }

        return new Paragraph(new Run("Photo non disponible"))
        {
            TextAlignment = TextAlignment.Center,
            Foreground = TextMuted,
            FontSize = FontSmall,
            Margin = new Thickness(CellPad),
            LineHeight = PhotoHeight
        };
    }

    private static Block BuildIdentificationBlock(EnrollmentFormDocumentDto form)
    {
        var section = new Section { Margin = new Thickness(CellPad) };

        section.Blocks.Add(new Paragraph(new Run(form.RegistrationNumber))
        {
            FontWeight = FontWeights.Bold,
            FontSize = FontMatricule,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
            LineHeight = 12
        });

        var qr = CreateQrCodeImage(form.RegistrationNumber, QrSize);
        if (qr is not null)
        {
            section.Blocks.Add(new Paragraph(new InlineUIContainer(qr))
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0),
                LineHeight = 1
            });
        }

        return section;
    }

    private static Block BuildFinancialBlock(EnrollmentFormDocumentDto form)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var currency = form.Currency ?? "CDF";
        var fee = form.RegistrationFee?.ToString("N0", culture) ?? "—";
        var paid = form.AmountPaid > 0 ? $"{form.AmountPaid:N0}" : "—";
        var balance = form.BalanceDue?.ToString("N0", culture) ?? fee;
        var isUnpaid = form.AmountPaid <= 0;

        var table = CreateTable();
        var labelW = SideColWidth * 0.55;
        var amountW = SideColWidth - labelW - 8;
        table.Columns.Add(Col(labelW));
        table.Columns.Add(Col(amountW));
        var group = RowGroup();

        group.Rows.Add(FinRow("Frais d'inscription", $"{fee} {currency}", false));
        group.Rows.Add(FinRow("Montant payé", paid == "—" ? paid : $"{paid} {currency}", false));
        group.Rows.Add(FinRow("Reste à payer", $"{balance} {currency}", isUnpaid));

        table.RowGroups.Add(group);

        var section = new Section { Margin = new Thickness(CellPad) };
        section.Blocks.Add(table);
        section.Blocks.Add(new Paragraph(new Run("Mode de paiement :"))
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = FontSmall,
            Margin = new Thickness(0, 4, 0, 2),
            LineHeight = 10
        });
        section.Blocks.Add(CreateCheckboxGroup(string.Empty,
        [
            ("Espèces", false),
            ("Mobile Money", false),
            ("Chèque", false),
            ("Banque", false)
        ]));
        section.Blocks.Add(FinRowParagraph("N° Reçu", "—", false));
        return section;
    }

    private static Block BuildSignatures(
        EnrollmentFormDocumentDto form,
        IDocumentBrandingPathResolver brandingPathResolver)
    {
        var signatures = form.Branding.Signatures;
        var secretary = FindSignature(signatures, "secrétaire", "secretaire", "secretariat");
        var director = FindSignature(signatures, "direction", "directeur", "directrice", "visa");

        var slotWidth = ContentWidth / 4;
        var table = CreateTable();
        for (var i = 0; i < 4; i++)
        {
            table.Columns.Add(Col(slotWidth));
        }

        var row = new TableRow();
        row.Cells.Add(SignatureCell("Parents / Tuteur", null, null, brandingPathResolver));
        row.Cells.Add(SignatureCell("Secrétaire", secretary?.ImagePath, secretary?.SignatoryName, brandingPathResolver));
        row.Cells.Add(SignatureCell("Visa de la Direction", director?.ImagePath, director?.SignatoryName, brandingPathResolver));
        row.Cells.Add(StampCell(form.Branding.Stamps.FirstOrDefault(), brandingPathResolver));

        var group = RowGroup();
        group.Rows.Add(row);
        table.RowGroups.Add(group);
        table.Margin = new Thickness(0, SectionGap, 0, SectionGap);
        return table;
    }

    private static Block BuildAuditFooter(EnrollmentFormDocumentDto form)
    {
        var text =
            $"Utilisateur : {form.PrintedBy ?? "—"}    |    Date : {form.GeneratedAt:dd/MM/yyyy}    |    " +
            $"Heure : {form.GeneratedAt:HH:mm:ss}    |    Poste : {form.Workstation}    |    Version ERP : {form.ErpVersion}";

        return new Paragraph(new Run(text))
        {
            FontSize = FontSmall,
            Background = LightBlueBg,
            Padding = new Thickness(6, 3, 6, 3),
            TextAlignment = TextAlignment.Center,
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 2),
            LineHeight = 10
        };
    }

    private static Block BuildSchoolFooter(EnrollmentFormDocumentDto form)
    {
        var footer = form.Branding.Footer;
        if (footer is null)
        {
            return new Paragraph { LineHeight = 1, Margin = new Thickness(0) };
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(footer.Address)) parts.Add(footer.Address);
        if (!string.IsNullOrWhiteSpace(footer.Phone)) parts.Add($"Tél. {footer.Phone}");
        if (!string.IsNullOrWhiteSpace(footer.Email)) parts.Add(footer.Email);
        if (!string.IsNullOrWhiteSpace(footer.Website)) parts.Add(footer.Website);
        if (!string.IsNullOrWhiteSpace(footer.PoBox)) parts.Add($"BP {footer.PoBox}");

        if (parts.Count == 0)
        {
            return new Paragraph { LineHeight = 1, Margin = new Thickness(0) };
        }

        return new Paragraph(new Run(string.Join("   •   ", parts)))
        {
            FontSize = FontSmall,
            Foreground = TextMuted,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0),
            LineHeight = 10
        };
    }

    private static SchoolSignatureDto? FindSignature(
        IReadOnlyList<SchoolSignatureDto> signatures,
        params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            var match = signatures.FirstOrDefault(s =>
                s.Function.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static Block SectionBlock(string title, Block content)
    {
        var wrapper = BorderedSection(new Thickness(0, 0, 0, SectionGap));
        wrapper.Blocks.Add(SectionTitle(title));
        wrapper.Blocks.Add(content);
        return wrapper;
    }

    private static Block SidebarPanel(string title, Block content)
    {
        var wrapper = BorderedSection(new Thickness(0, 0, 0, SectionGap));
        wrapper.Blocks.Add(new Paragraph(new Run(title))
        {
            FontWeight = FontWeights.Bold,
            FontSize = FontSmall,
            Foreground = Brushes.White,
            Background = PrimaryBlue,
            Padding = new Thickness(4, 2, 4, 2),
            TextAlignment = TextAlignment.Center,
            LineHeight = 10
        });
        wrapper.Blocks.Add(content);
        return wrapper;
    }

    private static Section BorderedSection(Thickness margin) =>
        new()
        {
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(1),
            Margin = margin,
            Padding = new Thickness(0)
        };

    private static Paragraph SectionTitle(string title) =>
        new(new Run(title))
        {
            FontWeight = FontWeights.Bold,
            FontSize = FontSection,
            Foreground = Brushes.White,
            Background = PrimaryBlue,
            Padding = new Thickness(5, 3, 5, 3),
            Margin = new Thickness(0),
            LineHeight = 11
        };

    private static Paragraph SubBlockTitle(string title) =>
        new(new Run(title))
        {
            FontWeight = FontWeights.Bold,
            FontSize = FontSmall,
            Foreground = PrimaryBlue,
            Margin = new Thickness(CellPad, CellPad, CellPad, 1),
            LineHeight = 10
        };

    private static Table TwoColumnFieldTable()
    {
        var table = CreateTable();
        table.Columns.Add(Col(LabelColWidth));
        table.Columns.Add(Col(ValueColWidth));
        table.Columns.Add(Col(LabelColWidth));
        table.Columns.Add(Col(ValueColWidth));
        table.RowGroups.Add(RowGroup());
        return table;
    }

    private static void AddTwoColRow(
        Table table,
        (string Label, string Value) left,
        (string Label, string Value) right)
    {
        var row = new TableRow();
        if (!string.IsNullOrEmpty(left.Label))
        {
            row.Cells.Add(LabelCell(left.Label));
            row.Cells.Add(ValueCell(left.Value));
        }
        else
        {
            row.Cells.Add(EmptyCell());
            row.Cells.Add(EmptyCell());
        }

        if (!string.IsNullOrEmpty(right.Label))
        {
            row.Cells.Add(LabelCell(right.Label));
            row.Cells.Add(ValueCell(right.Value));
        }
        else
        {
            row.Cells.Add(EmptyCell());
            row.Cells.Add(EmptyCell());
        }

        table.RowGroups[0].Rows.Add(row);
    }

    private static TableRow FinRow(string label, string amount, bool highlightUnpaid)
    {
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run($"{label} :"))
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = FontSmall,
            Margin = new Thickness(0),
            LineHeight = 11
        })
        {
            Padding = new Thickness(0, 1, 2, 1)
        });

        row.Cells.Add(new TableCell(new Paragraph(new Run(amount))
        {
            FontWeight = FontWeights.Bold,
            FontSize = FontSmall,
            TextAlignment = TextAlignment.Right,
            Foreground = highlightUnpaid ? UnpaidRed : Brushes.Black,
            Margin = new Thickness(0),
            LineHeight = 11
        })
        {
            Padding = new Thickness(0, 1, 0, 1)
        });

        return row;
    }

    private static Paragraph FinRowParagraph(string label, string amount, bool highlightUnpaid)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 2, 0, 0),
            LineHeight = 11
        };

        paragraph.Inlines.Add(new Run($"{label} : ") { FontWeight = FontWeights.SemiBold, FontSize = FontSmall });
        paragraph.Inlines.Add(new Run(amount)
        {
            FontWeight = FontWeights.Bold,
            FontSize = FontSmall,
            Foreground = highlightUnpaid ? UnpaidRed : Brushes.Black
        });

        return paragraph;
    }

    private static TableCell LabelCell(string label) =>
        new(new Paragraph(new Run($"{label}"))
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = FontSmall,
            Margin = new Thickness(0),
            LineHeight = 11
        })
        {
            Padding = new Thickness(CellPad, 2, 2, 2),
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(0, 0, 0, 0.5)
        };

    private static TableCell ValueCell(string value) =>
        new(new Paragraph(new Run(value))
        {
            FontSize = FontBody,
            Margin = new Thickness(0),
            LineHeight = 11
        })
        {
            Padding = new Thickness(2, 2, CellPad, 2),
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(0, 0, 0, 0.5)
        };

    private static TableCell DocCell(string label, bool isChecked) =>
        new(new Paragraph(new Run($"{(isChecked ? "☑" : "☐")} {label}") { FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = FontSmall })
        {
            Margin = new Thickness(0),
            LineHeight = 11
        })
        {
            Padding = new Thickness(CellPad, 2, CellPad, 2)
        };

    private static TableCell SignatureCell(
        string caption,
        string? imagePath,
        string? signatoryName,
        IDocumentBrandingPathResolver brandingPathResolver)
    {
        var cell = new TableCell
        {
            Padding = new Thickness(CellPad),
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(1)
        };

        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            var path = brandingPathResolver.ResolveAbsolutePath(imagePath);
            var image = path is not null ? CreateImage(path, ContentWidth / 4 - 12, 34, Stretch.Uniform) : null;
            if (image is not null)
            {
                cell.Blocks.Add(new Paragraph(new InlineUIContainer(image))
                {
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    LineHeight = 1
                });
            }
        }
        else
        {
            cell.Blocks.Add(new Paragraph(new Run(" "))
            {
                Margin = new Thickness(0),
                LineHeight = 30
            });
        }

        cell.Blocks.Add(new Paragraph(new Run(caption))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = FontSmall,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 2, 0, 0),
            LineHeight = 10
        });

        if (!string.IsNullOrWhiteSpace(signatoryName))
        {
            cell.Blocks.Add(new Paragraph(new Run(signatoryName))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = FontSmall,
                Foreground = TextMuted,
                LineHeight = 9
            });
        }

        return cell;
    }

    private static TableCell StampCell(
        SchoolStampDto? stamp,
        IDocumentBrandingPathResolver brandingPathResolver)
    {
        var cell = new TableCell
        {
            Padding = new Thickness(CellPad),
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(1)
        };

        if (stamp is not null)
        {
            var stampPath = brandingPathResolver.ResolveAbsolutePath(stamp.ImagePath);
            var stampImage = stampPath is not null ? CreateImage(stampPath, 50, 50, Stretch.Uniform) : null;
            if (stampImage is not null)
            {
                cell.Blocks.Add(new Paragraph(new InlineUIContainer(stampImage))
                {
                    TextAlignment = TextAlignment.Center,
                    LineHeight = 1
                });
            }
            else
            {
                cell.Blocks.Add(new Paragraph(new Run(" "))
                {
                    Margin = new Thickness(0),
                    LineHeight = 30
                });
            }
        }
        else
        {
            cell.Blocks.Add(new Paragraph(new Run(" "))
            {
                Margin = new Thickness(0),
                LineHeight = 30
            });
        }

        cell.Blocks.Add(new Paragraph(new Run("Cachet"))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = FontSmall,
            FontWeight = FontWeights.SemiBold,
            LineHeight = 10
        });

        return cell;
    }

    private static Paragraph CreateCheckboxGroup(string label, IReadOnlyList<(string Text, bool Checked)> options)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            LineHeight = 10
        };

        if (!string.IsNullOrWhiteSpace(label))
        {
            paragraph.Inlines.Add(new Run(label + " ") { FontWeight = FontWeights.SemiBold, FontSize = FontSmall });
        }

        for (var i = 0; i < options.Count; i++)
        {
            var (text, isChecked) = options[i];
            if (i > 0)
            {
                paragraph.Inlines.Add(new Run("  "));
            }

            paragraph.Inlines.Add(new Run(isChecked ? "☑" : "☐") { FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = FontSmall });
            paragraph.Inlines.Add(new Run($" {text}") { FontSize = FontSmall });
        }

        return paragraph;
    }

    private static Table CreateTable() =>
        new() { CellSpacing = 0, Margin = new Thickness(0) };

    private static TableColumn Col(double width) =>
        new() { Width = new GridLength(Math.Max(width, 40)) };

    private static TableRowGroup RowGroup() => new();

    private static TableRow FullRow(Block content)
    {
        var row = new TableRow();
        row.Cells.Add(new TableCell(content) { ColumnSpan = 2, Padding = new Thickness(0) });
        return row;
    }

    private static TableRow TwoColRow(Block left, Block right)
    {
        var row = new TableRow();
        row.Cells.Add(new TableCell(left) { Padding = new Thickness(0) });
        row.Cells.Add(new TableCell(right) { Padding = new Thickness(0) });
        return row;
    }

    private static TableCell BorderedCell(Block content) =>
        new(content)
        {
            Padding = new Thickness(CellPad),
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(1)
        };

    private static TableCell EmptyCell() =>
        new(new Paragraph()) { Padding = new Thickness(CellPad, 2, CellPad, 2) };

    private static bool IsDocumentProvided(IReadOnlyList<string> documents, string label) =>
        documents.Any(document =>
            document.Equals(label, StringComparison.OrdinalIgnoreCase)
            || (label.Equals("Photos", StringComparison.OrdinalIgnoreCase)
                && document.Equals("Photo", StringComparison.OrdinalIgnoreCase))
            || (label.Equals("Pièce d'identité", StringComparison.OrdinalIgnoreCase)
                && document.Contains("identité", StringComparison.OrdinalIgnoreCase))
            || (label.Equals("Attestation de réussite", StringComparison.OrdinalIgnoreCase)
                && document.Contains("réussite", StringComparison.OrdinalIgnoreCase))
            || (label.Equals("Certificat de transfert", StringComparison.OrdinalIgnoreCase)
                && document.Contains("transfert", StringComparison.OrdinalIgnoreCase)));

    private static System.Windows.Controls.Image? CreateQrCodeImage(string content, double size)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(data);
            var png = qrCode.GetGraphic(4);
            using var stream = new MemoryStream(png);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return new System.Windows.Controls.Image
            {
                Source = bitmap,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }
        catch
        {
            return null;
        }
    }

    private static System.Windows.Controls.Image? CreateImage(
        string path,
        double width,
        double height,
        Stretch stretch)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            return new System.Windows.Controls.Image
            {
                Source = bitmap,
                Width = width,
                MaxWidth = width,
                Height = height,
                MaxHeight = height,
                Stretch = stretch,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }
        catch
        {
            return null;
        }
    }
}
