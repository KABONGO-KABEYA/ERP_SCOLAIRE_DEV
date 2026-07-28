namespace SchoolManagement.Application.Reports;

using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.DocumentBranding;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Domain.Enums;

/// <summary>
/// PDF des rapports financiers : une page par onglet de l'application Desktop.
/// </summary>
public static class RealizedReceiptsPdfGenerator
{
    private static readonly Color PrimaryBlue = Color.FromHex("#1E3A8A");
    private static readonly Color HeaderBlue = Color.FromHex("#1D4ED8");
    private static readonly Color LightBlue = Color.FromHex("#EEF2FF");
    private static readonly Color BorderBlue = Color.FromHex("#C7D2FE");
    private static readonly Color TextMuted = Color.FromHex("#6B7280");
    private static readonly Color Zebra = Color.FromHex("#F8FAFC");

    private static readonly string[] TabTitles =
    [
        "Détail",
        "Journalier",
        "Par classe",
        "Par section",
        "Par type de frais",
        "Répartitions",
        "Retenues"
    ];

    static RealizedReceiptsPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] BuildPdfBytes(
        RealizedReceiptsResultDto result,
        AllocationCashFlowResultDto allocations,
        WithholdingReportResultDto withholdings,
        string schoolName,
        DocumentPrintBrandingDto branding,
        string? feeTypeName,
        Func<string?, byte[]?> loadImage)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var useLandscape = result.InstallmentColumns.Count >= 5;

        return Document.Create(document =>
        {
            // Page 1 — Détail
            document.Page(page => ConfigurePage(page, useLandscape, schoolName, branding, loadImage, result, feeTypeName, culture, 0, col =>
            {
                col.Item().Element(c => BuildSummaryBlock(c, result, culture));
                col.Item().Element(c => SectionTitle(c, "Élèves × échéances"));
                col.Item().Element(c => BuildDetailPivotTable(c, result, culture));
            }));

            // Page 2 — Journalier
            document.Page(page => ConfigurePage(page, useLandscape, schoolName, branding, loadImage, result, feeTypeName, culture, 1, col =>
            {
                if (result.DailyPivotRows.Count == 0)
                {
                    col.Item().Element(EmptyState);
                    return;
                }

                col.Item().Element(c => BuildDailyPivotLikeUi(c, result, culture));
            }));

            // Page 3 — Par classe
            document.Page(page => ConfigurePage(page, false, schoolName, branding, loadImage, result, feeTypeName, culture, 2, col =>
            {
                col.Item().Element(c => SectionTitle(c, "Total période par classe"));
                col.Item().Element(c => BuildByClassTable(c, result, culture));
                col.Item().PaddingTop(8).Element(c => SectionTitle(c, "Recette journalière par classe"));
                col.Item().Element(c => BuildDailyByClass(c, result, culture));
            }));

            // Page 4 — Par section
            document.Page(page => ConfigurePage(page, false, schoolName, branding, loadImage, result, feeTypeName, culture, 3, col =>
            {
                col.Item().Element(c => SectionTitle(c, "Total période par section"));
                col.Item().Element(c => BuildBySectionTable(c, result, culture));
                col.Item().PaddingTop(8).Element(c => SectionTitle(c, "Recette journalière par section"));
                col.Item().Element(c => BuildDailyBySection(c, result, culture));
            }));

            // Page 5 — Par type de frais
            document.Page(page => ConfigurePage(page, false, schoolName, branding, loadImage, result, feeTypeName, culture, 4, col =>
            {
                col.Item().Element(c => SectionTitle(c, "Total période par type de frais"));
                col.Item().Element(c => BuildByFeeTypeTable(c, result, culture));
                col.Item().PaddingTop(8).Element(c => SectionTitle(c, "Recette journalière par type de frais"));
                col.Item().Element(c => BuildDailyByFeeType(c, result, culture));
            }));

            // Page 6 — Répartitions
            document.Page(page => ConfigurePage(page, false, schoolName, branding, loadImage, result, feeTypeName, culture, 5, col =>
            {
                col.Item().Element(c => SectionTitle(c, "Répartition globale par compte bénéficiaire"));
                col.Item().Element(c => BuildAllocationGlobal(c, allocations, culture));
                col.Item().PaddingTop(8).Element(c => SectionTitle(c, "Répartition journalière"));
                col.Item().Element(c => BuildAllocationDaily(c, allocations, culture));
            }));

            // Page 7 — Retenues
            document.Page(page => ConfigurePage(page, false, schoolName, branding, loadImage, result, feeTypeName, culture, 6, col =>
            {
                col.Item().Element(c => BuildWithholdings(c, withholdings, culture));
            }));
        }).GeneratePdf();
    }

    private static void ConfigurePage(
        PageDescriptor page,
        bool landscape,
        string schoolName,
        DocumentPrintBrandingDto branding,
        Func<string?, byte[]?> loadImage,
        RealizedReceiptsResultDto result,
        string? feeTypeName,
        CultureInfo culture,
        int tabIndex,
        Action<ColumnDescriptor> content)
    {
        page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
        page.Margin(14);
        page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Black));

        // En-tête léger uniquement (évite DocumentLayoutException si l'image école est trop haute)
        page.Header().BorderBottom(1).BorderColor(BorderBlue).PaddingBottom(4).Row(row =>
        {
            row.RelativeItem().Text(schoolName.ToUpperInvariant()).SemiBold().FontSize(8).FontColor(PrimaryBlue);
            row.RelativeItem().AlignRight()
                .Text($"Onglet {tabIndex + 1}/{TabTitles.Length} — {TabTitles[tabIndex]}")
                .FontSize(8).FontColor(TextMuted);
        });

        page.Content().Column(col =>
        {
            col.Spacing(6);
            // Branding école dans le contenu (contraintes contrôlées)
            col.Item().Element(c => BuildSchoolHeader(c, schoolName, branding, loadImage));
            col.Item().Element(c => BuildTitleBlock(c, result, feeTypeName, culture, TabTitles[tabIndex], tabIndex + 1));
            content(col);
        });

        page.Footer().BorderTop(1).BorderColor(BorderBlue).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text($"Généré le {DateTime.Now:dd/MM/yyyy HH:mm}")
                .FontSize(7).FontColor(TextMuted);
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Total période : ").FontSize(7).FontColor(TextMuted);
                text.Span(result.GrandTotal.ToString("N2", culture)).SemiBold().FontSize(7);
                text.Span($"  ·  {result.PaymentCount} paiement(s)").FontSize(7).FontColor(TextMuted);
            });
        });
    }

    private static void BuildSchoolHeader(
        IContainer container,
        string schoolName,
        DocumentPrintBrandingDto branding,
        Func<string?, byte[]?> loadImage)
    {
        container.Column(col =>
        {
            if (DocumentPrintHeaderComposer.TryComposeFullWidthImage(col.Item(), branding, loadImage))
            {
                return;
            }

            col.Item().Border(1).BorderColor(BorderBlue).Background(LightBlue).Padding(6).Row(row =>
            {
                row.Spacing(8);
                var logo = loadImage(branding.PrimaryLogoPath) ?? loadImage(branding.HeaderImagePath);
                if (logo is not null)
                {
                    row.ConstantItem(40).Height(40).Image(logo).FitArea();
                }
                else
                {
                    row.ConstantItem(40).Height(40).Background(HeaderBlue).AlignCenter().AlignMiddle()
                        .Text(schoolName.Length > 0 ? schoolName[..1].ToUpperInvariant() : "E")
                        .Bold().FontSize(14).FontColor(Colors.White);
                }

                row.RelativeItem().AlignMiddle().Column(info =>
                {
                    info.Item().Text(schoolName.ToUpperInvariant()).Bold().FontSize(10).FontColor(PrimaryBlue);
                    if (!string.IsNullOrWhiteSpace(branding.Footer?.SchoolMotto))
                    {
                        info.Item().Text(branding.Footer!.SchoolMotto!).Italic().FontSize(6.5f).FontColor(TextMuted);
                    }

                    var address = branding.Footer?.Address;
                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        info.Item().Text(address!).FontSize(6.5f).FontColor(TextMuted);
                    }

                    var contact = string.Join("  ·  ", new[] { branding.Footer?.Phone, branding.Footer?.Email }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (!string.IsNullOrWhiteSpace(contact))
                    {
                        info.Item().Text(contact).FontSize(6.5f).FontColor(TextMuted);
                    }
                });
            });
        });
    }

    private static void BuildTitleBlock(
        IContainer container,
        RealizedReceiptsResultDto result,
        string? feeTypeName,
        CultureInfo culture,
        string tabTitle,
        int pageNumber)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("RAPPORT FINANCIER — RECETTES RÉALISÉES")
                .Bold().FontSize(11).FontColor(PrimaryBlue);
            col.Item().AlignCenter().Text(
                    $"Période du {result.FromDate.ToString("dd/MM/yyyy", culture)} au {result.ToDate.ToString("dd/MM/yyyy", culture)}")
                .FontSize(8).FontColor(TextMuted);
            if (!string.IsNullOrWhiteSpace(feeTypeName))
            {
                col.Item().AlignCenter().Text($"Type de frais : {feeTypeName}")
                    .FontSize(7.5f).FontColor(TextMuted);
            }

            col.Item().PaddingTop(4).Background(HeaderBlue).Padding(5)
                .AlignCenter().Text($"ONGLET : {tabTitle.ToUpperInvariant()}  ·  PAGE {pageNumber}/{TabTitles.Length}")
                .SemiBold().FontSize(9).FontColor(Colors.White);
        });
    }

    private static void BuildSummaryBlock(
        IContainer container,
        RealizedReceiptsResultDto result,
        CultureInfo culture)
    {
        container.Row(row =>
        {
            row.Spacing(6);
            row.RelativeItem().Element(c => SummaryCard(c, "Total période", result.GrandTotal.ToString("N2", culture)));
            row.RelativeItem().Element(c => SummaryCard(c, "Nombre de paiements", result.PaymentCount.ToString(culture)));
            var currencyText = result.ByCurrency.Count == 0
                ? "—"
                : string.Join("  |  ", result.ByCurrency.Select(x => $"{x.Currency} : {x.TotalAmount.ToString("N2", culture)}"));
            row.RelativeItem().Element(c => SummaryCard(c, "Par devise", currencyText));
        });
    }

    private static void SummaryCard(IContainer container, string label, string value)
    {
        container.Border(1).BorderColor(BorderBlue).Background(Colors.White).Padding(5).Column(col =>
        {
            col.Item().Text(label).FontSize(6.5f).FontColor(TextMuted);
            col.Item().PaddingTop(2).Text(value).SemiBold().FontSize(8.5f).FontColor(PrimaryBlue);
        });
    }

    private static void SectionTitle(IContainer container, string title) =>
        container.Background(LightBlue).Border(1).BorderColor(BorderBlue).Padding(4)
            .Text(title).SemiBold().FontSize(8.5f).FontColor(PrimaryBlue);

    private static void EmptyState(IContainer container) =>
        container.Padding(12).Border(1).BorderColor(BorderBlue).Background(Zebra)
            .AlignCenter().Text("Aucune donnée pour cet onglet sur la période sélectionnée.")
            .FontColor(TextMuted).FontSize(9);

    private static void BuildDetailPivotTable(
        IContainer container,
        RealizedReceiptsResultDto result,
        CultureInfo culture)
    {
        if (result.PivotRows.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.4f);
                columns.RelativeColumn(1.5f);
                foreach (var _ in result.InstallmentColumns)
                {
                    columns.RelativeColumn(1.1f);
                }

                columns.RelativeColumn(1.1f);
            });

            table.Header(header =>
            {
                HeaderCell(header, "Nom complet");
                HeaderCell(header, "Classe");
                foreach (var installment in result.InstallmentColumns)
                {
                    HeaderCell(header, installment.InstallmentName, alignRight: true);
                }

                HeaderCell(header, "Total", alignRight: true);
            });

            for (var index = 0; index < result.PivotRows.Count; index++)
            {
                var row = result.PivotRows[index];
                var bg = index % 2 == 1 ? Zebra : Colors.White;
                BodyCell(table, row.StudentName, bg);
                BodyCell(table, row.ClassName, bg);
                for (var i = 0; i < row.InstallmentAmounts.Count; i++)
                {
                    var amount = row.InstallmentAmounts[i];
                    BodyCell(table, amount > 0 ? amount.ToString("N2", culture) : "—", bg, alignRight: true);
                }

                BodyCell(table, row.RowTotal.ToString("N2", culture), bg, alignRight: true, bold: true);
            }

            table.Cell().ColumnSpan((uint)(2 + result.InstallmentColumns.Count))
                .Background(LightBlue).BorderBottom(1).BorderColor(BorderBlue).Padding(4)
                .Text("Total général").SemiBold().FontColor(PrimaryBlue);
            table.Cell().Background(LightBlue).BorderBottom(1).BorderColor(BorderBlue).Padding(4)
                .AlignRight().Text(result.GrandTotal.ToString("N2", culture)).SemiBold().FontColor(PrimaryBlue);
        });
    }

    private static void BuildDailyPivotLikeUi(
        IContainer container,
        RealizedReceiptsResultDto result,
        CultureInfo culture)
    {
        container.Column(col =>
        {
            foreach (var dateGroup in result.DailyPivotRows.GroupBy(r => r.Date).OrderBy(g => g.Key))
            {
                col.Item().PaddingTop(4).Background(LightBlue).Border(1).BorderColor(BorderBlue).Padding(4)
                    .Text($"Date : {dateGroup.Key.ToString("dddd dd MMMM yyyy", culture)}")
                    .SemiBold().FontSize(8).FontColor(PrimaryBlue);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.4f);
                        columns.RelativeColumn(1.5f);
                        foreach (var _ in result.InstallmentColumns)
                        {
                            columns.RelativeColumn(1.3f);
                        }

                        columns.RelativeColumn(1.1f);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header, "Nom complet");
                        HeaderCell(header, "Classe");
                        foreach (var installment in result.InstallmentColumns)
                        {
                            HeaderCell(header, installment.InstallmentName);
                        }

                        HeaderCell(header, "Total", alignRight: true);
                    });

                    var ordered = dateGroup.OrderBy(r => r.ClassName).ThenBy(r => r.StudentName).ToList();
                    for (var index = 0; index < ordered.Count; index++)
                    {
                        var row = ordered[index];
                        var bg = index % 2 == 1 ? Zebra : Colors.White;
                        BodyCell(table, row.StudentName, bg);
                        BodyCell(table, row.ClassName, bg);
                        for (var i = 0; i < row.InstallmentDetails.Count; i++)
                        {
                            var detail = row.InstallmentDetails[i];
                            BodyCell(table, string.IsNullOrWhiteSpace(detail) ? "—" : detail, bg);
                        }

                        BodyCell(table, row.RowTotal.ToString("N2", culture), bg, alignRight: true, bold: true);
                    }

                    var dayTotal = ordered.Sum(r => r.RowTotal);
                    table.Cell().ColumnSpan((uint)(2 + result.InstallmentColumns.Count))
                        .Background(LightBlue).Padding(3).Text("Sous-total jour").SemiBold().FontColor(PrimaryBlue);
                    table.Cell().Background(LightBlue).Padding(3).AlignRight()
                        .Text(dayTotal.ToString("N2", culture)).SemiBold().FontColor(PrimaryBlue);
                });
            }

            col.Item().PaddingTop(6).Background(LightBlue).Padding(5).Row(row =>
            {
                row.RelativeItem().Text("Total général").Bold().FontColor(PrimaryBlue);
                row.RelativeItem().AlignRight().Text(result.GrandTotal.ToString("N2", culture)).Bold().FontColor(PrimaryBlue);
            });
        });
    }

    private static void BuildByClassTable(IContainer container, RealizedReceiptsResultDto result, CultureInfo culture)
    {
        if (result.ByClass.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.1f);
                c.RelativeColumn(2.2f);
                c.RelativeColumn(1.6f);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(0.8f);
            });
            table.Header(h =>
            {
                HeaderCell(h, "Code");
                HeaderCell(h, "Classe");
                HeaderCell(h, "Section");
                HeaderCell(h, "Montant", alignRight: true);
                HeaderCell(h, "Nb", alignRight: true);
            });

            for (var i = 0; i < result.ByClass.Count; i++)
            {
                var item = result.ByClass[i];
                var bg = i % 2 == 1 ? Zebra : Colors.White;
                BodyCell(table, item.ClassCode, bg);
                BodyCell(table, item.ClassName, bg);
                BodyCell(table, item.SectionName, bg);
                BodyCell(table, item.TotalAmount.ToString("N2", culture), bg, alignRight: true);
                BodyCell(table, item.PaymentCount.ToString(culture), bg, alignRight: true);
            }

            var total = result.ByClass.Sum(x => x.TotalAmount);
            var count = result.ByClass.Sum(x => x.PaymentCount);
            TotalRow(table, 3, "Total", total.ToString("N2", culture), count.ToString(culture));
        });
    }

    private static void BuildDailyByClass(IContainer container, RealizedReceiptsResultDto result, CultureInfo culture)
    {
        if (result.DailyByClass.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Column(col =>
        {
            foreach (var group in result.DailyByClass.GroupBy(x => x.Date).OrderBy(g => g.Key))
            {
                var dayTotal = group.Sum(x => x.TotalAmount);
                col.Item().PaddingTop(4).Background(LightBlue).Padding(4).Row(row =>
                {
                    row.RelativeItem().Text(group.Key.ToString("dd/MM/yyyy", culture)).SemiBold().FontColor(PrimaryBlue);
                    row.RelativeItem().AlignRight().Text(dayTotal.ToString("N2", culture)).SemiBold().FontColor(PrimaryBlue);
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(0.8f);
                    });
                    table.Header(h =>
                    {
                        HeaderCell(h, "Classe");
                        HeaderCell(h, "Montant", alignRight: true);
                        HeaderCell(h, "Nb", alignRight: true);
                    });
                    var rows = group.OrderBy(x => x.ClassName).ToList();
                    for (var i = 0; i < rows.Count; i++)
                    {
                        var item = rows[i];
                        var bg = i % 2 == 1 ? Zebra : Colors.White;
                        BodyCell(table, item.ClassName, bg);
                        BodyCell(table, item.TotalAmount.ToString("N2", culture), bg, alignRight: true);
                        BodyCell(table, item.PaymentCount.ToString(culture), bg, alignRight: true);
                    }
                });
            }
        });
    }

    private static void BuildBySectionTable(IContainer container, RealizedReceiptsResultDto result, CultureInfo culture)
    {
        if (result.BySection.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.1f);
                c.RelativeColumn(3);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(0.8f);
            });
            table.Header(h =>
            {
                HeaderCell(h, "Code");
                HeaderCell(h, "Section");
                HeaderCell(h, "Montant", alignRight: true);
                HeaderCell(h, "Nb", alignRight: true);
            });

            for (var i = 0; i < result.BySection.Count; i++)
            {
                var item = result.BySection[i];
                var bg = i % 2 == 1 ? Zebra : Colors.White;
                BodyCell(table, item.SectionCode, bg);
                BodyCell(table, item.SectionName, bg);
                BodyCell(table, item.TotalAmount.ToString("N2", culture), bg, alignRight: true);
                BodyCell(table, item.PaymentCount.ToString(culture), bg, alignRight: true);
            }

            var total = result.BySection.Sum(x => x.TotalAmount);
            var count = result.BySection.Sum(x => x.PaymentCount);
            TotalRow(table, 2, "Total", total.ToString("N2", culture), count.ToString(culture));
        });
    }

    private static void BuildDailyBySection(IContainer container, RealizedReceiptsResultDto result, CultureInfo culture)
    {
        if (result.DailyBySection.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Column(col =>
        {
            foreach (var group in result.DailyBySection.GroupBy(x => x.Date).OrderBy(g => g.Key))
            {
                var dayTotal = group.Sum(x => x.TotalAmount);
                col.Item().PaddingTop(4).Background(LightBlue).Padding(4).Row(row =>
                {
                    row.RelativeItem().Text(group.Key.ToString("dd/MM/yyyy", culture)).SemiBold().FontColor(PrimaryBlue);
                    row.RelativeItem().AlignRight().Text(dayTotal.ToString("N2", culture)).SemiBold().FontColor(PrimaryBlue);
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(0.8f);
                    });
                    table.Header(h =>
                    {
                        HeaderCell(h, "Section");
                        HeaderCell(h, "Montant", alignRight: true);
                        HeaderCell(h, "Nb", alignRight: true);
                    });
                    var rows = group.OrderBy(x => x.SectionName).ToList();
                    for (var i = 0; i < rows.Count; i++)
                    {
                        var item = rows[i];
                        var bg = i % 2 == 1 ? Zebra : Colors.White;
                        BodyCell(table, item.SectionName, bg);
                        BodyCell(table, item.TotalAmount.ToString("N2", culture), bg, alignRight: true);
                        BodyCell(table, item.PaymentCount.ToString(culture), bg, alignRight: true);
                    }
                });
            }
        });
    }

    private static void BuildByFeeTypeTable(IContainer container, RealizedReceiptsResultDto result, CultureInfo culture)
    {
        if (result.ByFeeType.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.8f);
                c.RelativeColumn(1);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(0.8f);
            });
            table.Header(h =>
            {
                HeaderCell(h, "Type de frais");
                HeaderCell(h, "Devise");
                HeaderCell(h, "Montant", alignRight: true);
                HeaderCell(h, "Nb", alignRight: true);
            });

            for (var i = 0; i < result.ByFeeType.Count; i++)
            {
                var item = result.ByFeeType[i];
                var bg = i % 2 == 1 ? Zebra : Colors.White;
                BodyCell(table, item.FeeTypeName, bg);
                BodyCell(table, item.Currency, bg);
                BodyCell(table, item.TotalAmount.ToString("N2", culture), bg, alignRight: true);
                BodyCell(table, item.PaymentCount.ToString(culture), bg, alignRight: true);
            }

            var total = result.ByFeeType.Sum(x => x.TotalAmount);
            var count = result.ByFeeType.Sum(x => x.PaymentCount);
            TotalRow(table, 2, "Total", total.ToString("N2", culture), count.ToString(culture));
        });
    }

    private static void BuildDailyByFeeType(IContainer container, RealizedReceiptsResultDto result, CultureInfo culture)
    {
        if (result.DailyByFeeType.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Column(col =>
        {
            foreach (var group in result.DailyByFeeType.GroupBy(x => x.Date).OrderBy(g => g.Key))
            {
                var dayTotal = group.Sum(x => x.TotalAmount);
                col.Item().PaddingTop(4).Background(LightBlue).Padding(4).Row(row =>
                {
                    row.RelativeItem().Text(group.Key.ToString("dd/MM/yyyy", culture)).SemiBold().FontColor(PrimaryBlue);
                    row.RelativeItem().AlignRight().Text(dayTotal.ToString("N2", culture)).SemiBold().FontColor(PrimaryBlue);
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(0.8f);
                    });
                    table.Header(h =>
                    {
                        HeaderCell(h, "Type de frais");
                        HeaderCell(h, "Devise");
                        HeaderCell(h, "Montant", alignRight: true);
                        HeaderCell(h, "Nb", alignRight: true);
                    });
                    var rows = group.OrderBy(x => x.FeeTypeName).ToList();
                    for (var i = 0; i < rows.Count; i++)
                    {
                        var item = rows[i];
                        var bg = i % 2 == 1 ? Zebra : Colors.White;
                        BodyCell(table, item.FeeTypeName, bg);
                        BodyCell(table, item.Currency, bg);
                        BodyCell(table, item.TotalAmount.ToString("N2", culture), bg, alignRight: true);
                        BodyCell(table, item.PaymentCount.ToString(culture), bg, alignRight: true);
                    }
                });
            }
        });
    }

    private static void BuildAllocationGlobal(
        IContainer container,
        AllocationCashFlowResultDto allocations,
        CultureInfo culture)
    {
        if (allocations.GlobalRows.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.2f);
                c.RelativeColumn(0.7f);
                c.RelativeColumn(1.1f);
                c.RelativeColumn(1.1f);
                c.RelativeColumn(1.1f);
                c.RelativeColumn(1.1f);
            });
            table.Header(h =>
            {
                HeaderCell(h, "Compte bénéficiaire");
                HeaderCell(h, "Devise");
                HeaderCell(h, "Période J-1", alignRight: true);
                HeaderCell(h, "Encaissement", alignRight: true);
                HeaderCell(h, "Dépense P", alignRight: true);
                HeaderCell(h, "Période P", alignRight: true);
            });

            for (var i = 0; i < allocations.GlobalRows.Count; i++)
            {
                var item = allocations.GlobalRows[i];
                var bg = i % 2 == 1 ? Zebra : Colors.White;
                BodyCell(table, item.DestinationName, bg);
                BodyCell(table, item.CurrencyCode, bg);
                BodyCell(table, item.PeriodJ1.ToString("N2", culture), bg, alignRight: true);
                BodyCell(table, item.Encaissement.ToString("N2", culture), bg, alignRight: true);
                BodyCell(table, item.DepenseP.ToString("N2", culture), bg, alignRight: true);
                BodyCell(table, item.PeriodeP.ToString("N2", culture), bg, alignRight: true);
            }

            foreach (var t in allocations.TotalsByCurrency)
            {
                table.Cell().Background(LightBlue).Padding(3).Text(t.DestinationName).SemiBold().FontColor(PrimaryBlue);
                table.Cell().Background(LightBlue).Padding(3).Text(t.CurrencyCode).SemiBold().FontColor(PrimaryBlue);
                table.Cell().Background(LightBlue).Padding(3).AlignRight().Text(t.PeriodJ1.ToString("N2", culture)).SemiBold().FontColor(PrimaryBlue);
                table.Cell().Background(LightBlue).Padding(3).AlignRight().Text(t.Encaissement.ToString("N2", culture)).SemiBold().FontColor(PrimaryBlue);
                table.Cell().Background(LightBlue).Padding(3).AlignRight().Text(t.DepenseP.ToString("N2", culture)).SemiBold().FontColor(PrimaryBlue);
                table.Cell().Background(LightBlue).Padding(3).AlignRight().Text(t.PeriodeP.ToString("N2", culture)).SemiBold().FontColor(PrimaryBlue);
            }
        });
    }

    private static void BuildAllocationDaily(
        IContainer container,
        AllocationCashFlowResultDto allocations,
        CultureInfo culture)
    {
        if (allocations.DailyGroups.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Column(col =>
        {
            foreach (var group in allocations.DailyGroups.OrderBy(g => g.Date))
            {
                col.Item().PaddingTop(4).Background(LightBlue).Padding(4)
                    .Text(group.Date.ToString("dd/MM/yyyy", culture)).SemiBold().FontColor(PrimaryBlue);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(0.7f);
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(1.1f);
                    });
                    table.Header(h =>
                    {
                        HeaderCell(h, "Compte bénéficiaire");
                        HeaderCell(h, "Devise");
                        HeaderCell(h, "Période J-1", alignRight: true);
                        HeaderCell(h, "Encaissement", alignRight: true);
                        HeaderCell(h, "Dépense P", alignRight: true);
                        HeaderCell(h, "Période P", alignRight: true);
                    });

                    for (var i = 0; i < group.Rows.Count; i++)
                    {
                        var item = group.Rows[i];
                        var bg = i % 2 == 1 ? Zebra : Colors.White;
                        BodyCell(table, item.DestinationName, bg);
                        BodyCell(table, item.CurrencyCode, bg);
                        BodyCell(table, item.PeriodJ1.ToString("N2", culture), bg, alignRight: true);
                        BodyCell(table, item.Encaissement.ToString("N2", culture), bg, alignRight: true);
                        BodyCell(table, item.DepenseP.ToString("N2", culture), bg, alignRight: true);
                        BodyCell(table, item.PeriodeP.ToString("N2", culture), bg, alignRight: true);
                    }
                });
            }
        });
    }

    private static void BuildWithholdings(
        IContainer container,
        WithholdingReportResultDto withholdings,
        CultureInfo culture)
    {
        if (withholdings.Groups.Count == 0)
        {
            container.Element(EmptyState);
            return;
        }

        container.Column(col =>
        {
            foreach (var group in withholdings.Groups)
            {
                col.Item().PaddingTop(4).Background(LightBlue).Border(1).BorderColor(BorderBlue).Padding(4).Row(row =>
                {
                    row.RelativeItem().Text($"{group.WithholdingTypeName} ({group.WithholdingTypeCode})")
                        .SemiBold().FontColor(PrimaryBlue);
                    row.RelativeItem().AlignRight().Text(group.TypeTotal.ToString("N2", culture))
                        .SemiBold().FontColor(PrimaryBlue);
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.2f);
                    });
                    table.Header(h =>
                    {
                        HeaderCell(h, "Élève");
                        HeaderCell(h, "Date", alignRight: true);
                        HeaderCell(h, "Montant", alignRight: true);
                    });

                    for (var i = 0; i < group.Students.Count; i++)
                    {
                        var line = group.Students[i];
                        var bg = i % 2 == 1 ? Zebra : Colors.White;
                        BodyCell(table, line.StudentName, bg);
                        BodyCell(table, line.PaymentDate.ToString("dd/MM/yyyy", culture), bg, alignRight: true);
                        BodyCell(table, line.Amount.ToString("N2", culture), bg, alignRight: true);
                    }
                });
            }

            col.Item().PaddingTop(6).Background(LightBlue).Padding(5).Row(row =>
            {
                row.RelativeItem().Text($"Total  ·  {withholdings.PaymentCount} paiement(s)")
                    .Bold().FontColor(PrimaryBlue);
                row.RelativeItem().AlignRight().Text(withholdings.GrandTotal.ToString("N2", culture))
                    .Bold().FontColor(PrimaryBlue);
            });
        });
    }

    private static void TotalRow(
        TableDescriptor table,
        int labelSpan,
        string label,
        string amount,
        string count)
    {
        table.Cell().ColumnSpan((uint)labelSpan).Background(LightBlue).Padding(3)
            .Text(label).SemiBold().FontColor(PrimaryBlue);
        table.Cell().Background(LightBlue).Padding(3).AlignRight()
            .Text(amount).SemiBold().FontColor(PrimaryBlue);
        table.Cell().Background(LightBlue).Padding(3).AlignRight()
            .Text(count).SemiBold().FontColor(PrimaryBlue);
    }

    private static void HeaderCell(TableCellDescriptor header, string text, bool alignRight = false)
    {
        var cell = header.Cell().Background(HeaderBlue).Border(0.5f).BorderColor(HeaderBlue).Padding(3);
        var content = alignRight ? cell.AlignRight() : cell;
        content.Text(text).SemiBold().FontSize(7).FontColor(Colors.White);
    }

    private static void BodyCell(
        TableDescriptor table,
        string text,
        Color background,
        bool alignRight = false,
        bool bold = false)
    {
        var cell = table.Cell().Background(background).BorderBottom(0.5f).BorderColor(BorderBlue).Padding(3);
        var content = alignRight ? cell.AlignRight() : cell;
        if (bold)
        {
            content.Text(text).SemiBold().FontSize(7.5f);
        }
        else
        {
            content.Text(text).FontSize(7.5f);
        }
    }
}
