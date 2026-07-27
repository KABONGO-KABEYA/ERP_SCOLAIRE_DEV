using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Payments.DTOs;

namespace SchoolManagement.Application.Payments;

/// <summary>Générateur PDF A5 du relevé — pleine largeur imprimable.</summary>
public static class FeeTypeStatementPdfGenerator
{
    private static readonly Color Navy = Color.FromHex("#0B3D91");
    private static readonly Color PrimaryBlue = Color.FromHex("#1E5EFF");
    private static readonly Color LightBlue = Color.FromHex("#EAF2FF");
    private static readonly Color SoftBlue = Color.FromHex("#F5F8FF");
    private static readonly Color BorderBlue = Color.FromHex("#C9D8F5");
    private static readonly Color TextMuted = Color.FromHex("#64748B");
    private static readonly Color TextDark = Color.FromHex("#0F172A");
    private static readonly Color GreenPaid = Color.FromHex("#16A34A");
    private static readonly Color RedDue = Color.FromHex("#DC2626");
    private static readonly Color Zebra = Color.FromHex("#F8FBFF");

    static FeeTypeStatementPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] BuildPdfBytes(
        FeeTypeStatementDto statement,
        Func<string?, byte[]?> loadImage)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var currency = statement.Currency.ToString();

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(10);
                page.DefaultTextStyle(x => x.FontSize(7).FontColor(TextDark));

                page.Content().Column(column =>
                {
                    column.Spacing(5);
                    column.Item().Element(c => BuildHeader(c, statement, loadImage));
                    column.Item().Row(row =>
                    {
                        row.Spacing(6);
                        row.RelativeItem().Element(c => BuildHistoryTable(c, statement, culture, currency));
                        row.RelativeItem().Element(c => BuildSituationTable(c, statement, culture, currency));
                    });
                    column.Item().Element(c => BuildSummary(c, statement, culture, currency));
                    column.Item().Element(c => BuildFooter(c, statement));
                });
            });
        }).GeneratePdf();
    }

    private static void BuildHeader(
        IContainer container,
        FeeTypeStatementDto s,
        Func<string?, byte[]?> loadImage)
    {
        container.Row(row =>
        {
            row.Spacing(8);

            // Gauche : logo + école, titre + n° sous le logo
            row.RelativeItem().Column(left =>
            {
                left.Item().Row(schoolRow =>
                {
                    schoolRow.Spacing(6);

                    var logo = loadImage(s.Branding.PrimaryLogoPath) ?? loadImage(s.Branding.HeaderImagePath);
                    if (logo is not null)
                    {
                        schoolRow.ConstantItem(42).Height(42).Image(logo).FitArea();
                    }
                    else
                    {
                        schoolRow.ConstantItem(42).Height(42).Background(LightBlue).AlignCenter().AlignMiddle()
                            .Text(s.SchoolName.Length > 0 ? s.SchoolName[..1] : "E")
                            .Bold().FontSize(14).FontColor(Navy);
                    }

                    schoolRow.RelativeItem().AlignMiddle().Column(col =>
                    {
                        col.Item().Text(s.SchoolName.ToUpperInvariant())
                            .Bold().FontSize(8).FontColor(Navy);
                        if (!string.IsNullOrWhiteSpace(s.SchoolMotto))
                        {
                            col.Item().Text(s.SchoolMotto).Italic().FontSize(5.5f).FontColor(TextMuted);
                        }

                        if (!string.IsNullOrWhiteSpace(s.SchoolAddress))
                        {
                            col.Item().Text(s.SchoolAddress).FontSize(5.5f).FontColor(TextMuted);
                        }

                        var contact = string.Join("  ·  ", new[] { s.SchoolPhone, s.SchoolEmail }
                            .Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (!string.IsNullOrWhiteSpace(contact))
                        {
                            col.Item().Text(contact).FontSize(5.5f).FontColor(TextMuted);
                        }
                    });
                });

                left.Item().PaddingTop(4).Text(text =>
                {
                    text.Span($"{BuildDocumentTitle(s.FeeTypeName)} ").Bold().FontSize(9).FontColor(Navy);
                    text.Span($"n°{s.StatementNumber}").Bold().FontSize(9).FontColor(PrimaryBlue);
                });
            });

            // Droite : infos élève (nom complet)
            row.RelativeItem().Background(LightBlue).Border(1).BorderColor(BorderBlue).Padding(6).Column(col =>
            {
                col.Item().Text($"Nom complet : {OrDash(s.StudentName)}").Bold().FontSize(7.5f);
                col.Item().PaddingTop(2).Text($"Matricule : {OrDash(s.StudentRegistrationNumber)}").FontSize(7).FontColor(TextMuted);
                col.Item().PaddingTop(1).Text($"Classe : {OrDash(s.ClassName)}").FontSize(7).FontColor(TextMuted);
                col.Item().PaddingTop(1).Text($"Année scolaire : {OrDash(s.AcademicYearLabel)}").FontSize(7).FontColor(TextMuted);
            });
        });
    }

    private static string BuildDocumentTitle(string feeTypeName) =>
        $"RELEVÉ DE {feeTypeName.Trim().ToUpperInvariant()}";

    private static string OrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static void BuildHistoryTable(
        IContainer container,
        FeeTypeStatementDto s,
        CultureInfo culture,
        string currency)
    {
        container.Border(1).BorderColor(BorderBlue).Column(col =>
        {
            col.Item().Background(Navy).Padding(4)
                .Text("HISTORIQUE DES PAIEMENTS").Bold().FontSize(6.5f).FontColor(Colors.White);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(20);
                    c.RelativeColumn(1.6f);
                    c.RelativeColumn(1.3f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.2f);
                });

                table.Header(h =>
                {
                    HeaderCell(h, "N°");
                    HeaderCell(h, "TRANCHE");
                    HeaderCell(h, "DATE PAIEMENT");
                    HeaderCell(h, $"MONTANT PAYÉ ({currency})");
                    HeaderCell(h, "N° REÇU");
                });

                var rows = s.PaymentHistory.ToList();
                if (rows.Count == 0)
                {
                    BodyCell(table, "—", Colors.White, muted: true);
                    BodyCell(table, "Aucun paiement", Colors.White, muted: true);
                    BodyCell(table, "—", Colors.White, muted: true);
                    BodyCell(table, "—", Colors.White, muted: true);
                    BodyCell(table, "—", Colors.White, muted: true);
                }
                else
                {
                    for (var i = 0; i < rows.Count; i++)
                    {
                        var bg = i % 2 == 1 ? Zebra : Colors.White;
                        var line = rows[i];
                        BodyCell(table, line.Number.ToString("00"), bg);
                        BodyCell(table, line.InstallmentName, bg);
                        BodyCell(table, line.PaymentDate.ToLocalTime().ToString("dd/MM/yyyy", culture), bg);
                        BodyCell(table, line.AmountPaid.ToString("N2", culture), bg, alignRight: true);
                        BodyCell(table, line.ReceiptNumber, bg);
                    }
                }
            });
        });
    }

    private static void BuildSituationTable(
        IContainer container,
        FeeTypeStatementDto s,
        CultureInfo culture,
        string currency)
    {
        container.Border(1).BorderColor(BorderBlue).Column(col =>
        {
            col.Item().Background(Navy).Padding(4)
                .Text("SITUATION GLOBALE").Bold().FontSize(6.5f).FontColor(Colors.White);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(20);
                    c.RelativeColumn(1.6f);
                    c.RelativeColumn(1.4f);
                    c.RelativeColumn(1.3f);
                    c.RelativeColumn(1.4f);
                });

                table.Header(h =>
                {
                    HeaderCell(h, "N°");
                    HeaderCell(h, "TRANCHE");
                    HeaderCell(h, $"MONTANT PRÉVU ({currency})");
                    HeaderCell(h, $"DÉJÀ PAYÉ ({currency})");
                    HeaderCell(h, $"SOLDE RESTANT ({currency})");
                });

                var rows = s.InstallmentSituations.ToList();
                // Lignes vides uniquement si l'historique dépasse la situation globale.
                var totalRows = s.PaymentHistory.Count > rows.Count
                    ? s.PaymentHistory.Count
                    : rows.Count;

                if (totalRows == 0)
                {
                    BodyCell(table, "—", Colors.White, muted: true);
                    BodyCell(table, "Aucune tranche", Colors.White, muted: true);
                    BodyCell(table, "—", Colors.White, muted: true);
                    BodyCell(table, "—", Colors.White, muted: true);
                    BodyCell(table, "—", Colors.White, muted: true);
                }
                else
                {
                    for (var i = 0; i < totalRows; i++)
                    {
                        var bg = i % 2 == 1 ? Zebra : Colors.White;
                        if (i < rows.Count)
                        {
                            var line = rows[i];
                            BodyCell(table, line.Number.ToString("00"), bg);
                            BodyCell(table, line.InstallmentName, bg);
                            BodyCell(table, line.AmountExpected.ToString("N2", culture), bg, alignRight: true);
                            BodyCell(table, line.AmountPaid.ToString("N2", culture), bg, alignRight: true);
                            var color = line.Remaining <= 0 ? GreenPaid : RedDue;
                            table.Cell().Background(bg).BorderBottom(0.4f).BorderColor(BorderBlue).Padding(3)
                                .AlignRight()
                                .Text(line.Remaining.ToString("N2", culture)).Bold().FontColor(color).FontSize(7);
                        }
                        else
                        {
                            BodyCell(table, "—", bg, muted: true);
                            BodyCell(table, "—", bg, muted: true);
                            BodyCell(table, "—", bg, muted: true);
                            BodyCell(table, "—", bg, muted: true);
                            BodyCell(table, "—", bg, muted: true);
                        }
                    }
                }
            });
        });
    }

    private static void BuildSummary(
        IContainer container,
        FeeTypeStatementDto s,
        CultureInfo culture,
        string currency)
    {
        var remainingColor = s.TotalRemaining <= 0 ? GreenPaid : RedDue;
        var showFx = s.PaymentCurrencyAmount.HasValue
            && !string.IsNullOrWhiteSpace(s.PaymentCurrencyCode)
            && !string.Equals(s.FeeCurrencyCode, s.PaymentCurrencyCode, StringComparison.OrdinalIgnoreCase);

        container.Column(column =>
        {
            column.Item().Border(1).BorderColor(BorderBlue).Background(SoftBlue).Padding(6).Row(row =>
            {
                row.ConstantItem(78).AlignMiddle()
                    .Text("Récapitulatif :").Bold().FontSize(8).FontColor(Navy);

                row.RelativeItem().AlignMiddle().Row(r =>
                {
                    r.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.Span($"Prévu ({currency}) ").FontSize(6.5f).FontColor(TextMuted);
                        text.Span(s.TotalExpected.ToString("N2", culture)).Bold().FontSize(10).FontColor(PrimaryBlue);
                    });
                    r.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.Span($"Payé ({currency}) ").FontSize(6.5f).FontColor(TextMuted);
                        text.Span(s.TotalPaid.ToString("N2", culture)).Bold().FontSize(10).FontColor(GreenPaid);
                    });
                    r.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.Span($"Reste ({currency}) ").FontSize(6.5f).FontColor(TextMuted);
                        text.Span(s.TotalRemaining.ToString("N2", culture)).Bold().FontSize(10).FontColor(remainingColor);
                    });
                });
            });

            if (!showFx)
                return;

            column.Item().PaddingTop(4).Border(1).BorderColor(BorderBlue).Background(LightBlue).Padding(6).Column(col =>
            {
                var feeCode = s.FeeCurrencyCode ?? currency;
                var payCode = s.PaymentCurrencyCode!;
                var feeAmt = s.FeeCurrencyAmount ?? s.TotalPaid;
                col.Item().Text(text =>
                {
                    text.Span($"{feeAmt.ToString("N2", culture)} {feeCode}").Bold().FontSize(9).FontColor(Navy);
                    text.Span("  ·  Payé : ").FontSize(8).FontColor(TextMuted);
                    text.Span($"{s.PaymentCurrencyAmount!.Value.ToString("N2", culture)} {payCode}").Bold().FontSize(9).FontColor(GreenPaid);
                });
                if (s.AppliedExchangeRate.HasValue)
                {
                    col.Item().PaddingTop(2).Text(
                            $"Taux : 1 {feeCode} = {s.AppliedExchangeRate.Value.ToString("N6", culture)} {payCode}")
                        .FontSize(7.5f).FontColor(TextMuted);
                }
            });
        });
    }

    private static void BuildFooter(IContainer container, FeeTypeStatementDto s)
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        container.PaddingTop(4).Row(row =>
        {
            row.Spacing(20);

            row.RelativeItem().AlignBottom().Column(col =>
            {
                col.Item().AlignLeft()
                    .Text($"Caissier : {s.CashierName ?? "—"}")
                    .Bold().FontSize(7).FontColor(TextDark);
                col.Item().PaddingTop(10).BorderBottom(1).BorderColor(TextMuted).Height(18);
                col.Item().PaddingTop(3).AlignCenter().Text("Signature Caissier").FontSize(6.5f).FontColor(TextMuted);
            });

            row.RelativeItem().AlignBottom().Column(col =>
            {
                col.Item().AlignRight()
                    .Text(s.EditedAt.ToString("dd/MM/yyyy HH:mm", culture))
                    .FontSize(7).FontColor(TextMuted);
                col.Item().PaddingTop(10).BorderBottom(1).BorderColor(TextMuted).Height(18);
                col.Item().PaddingTop(3).AlignCenter().Text("Signature Parent / Tuteur").FontSize(6.5f).FontColor(TextMuted);
            });
        });
    }

    private static void HeaderCell(TableCellDescriptor h, string text) =>
        h.Cell().Background(Color.FromHex("#123A7A")).Padding(3)
            .Text(text).Bold().FontSize(6).FontColor(Colors.White);

    private static void BodyCell(
        TableDescriptor table,
        string text,
        Color background,
        bool alignRight = false,
        bool muted = false)
    {
        var cell = table.Cell().Background(background).BorderBottom(0.4f).BorderColor(BorderBlue).Padding(3);
        var content = alignRight ? cell.AlignRight() : cell;
        content.Text(text)
            .FontSize(7)
            .FontColor(muted ? TextMuted : TextDark);
    }
}
