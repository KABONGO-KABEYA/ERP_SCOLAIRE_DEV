using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Printing;

/// <summary>Relevé A5 (compatible A4) — pleine largeur imprimable.</summary>
public static class FeeTypeStatementDocumentBuilder
{
    // A5 portrait @ 96 DPI (défaut) : 148 × 210 mm ≈ 559 × 794 DIPs
    private const double DefaultPageWidth = 559;
    private const double DefaultPageHeight = 794;
    private const double PageMargin = 10;

    private static readonly Brush Navy = BrushFrom("#0B3D91");
    private static readonly Brush PrimaryBlue = BrushFrom("#1E5EFF");
    private static readonly Brush LightBlue = BrushFrom("#EAF2FF");
    private static readonly Brush SoftBlue = BrushFrom("#F5F8FF");
    private static readonly Brush BorderBlue = BrushFrom("#C9D8F5");
    private static readonly Brush TextMuted = BrushFrom("#64748B");
    private static readonly Brush TextDark = BrushFrom("#0F172A");
    private static readonly Brush GreenPaid = BrushFrom("#16A34A");
    private static readonly Brush RedDue = BrushFrom("#DC2626");
    private static readonly Brush Zebra = BrushFrom("#F8FBFF");
    private static readonly Brush HeaderCellBg = BrushFrom("#123A7A");
    private static readonly FontFamily UiFont = new("Segoe UI");
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static FlowDocument Build(
        FeeTypeStatementDto statement,
        IDocumentBrandingPathResolver brandingPathResolver,
        double? pageWidth = null,
        double? pageHeight = null)
    {
        var width = pageWidth is > 0 ? pageWidth.Value : DefaultPageWidth;
        var height = pageHeight is > 0 ? pageHeight.Value : DefaultPageHeight;
        var contentWidth = Math.Max(100, width - PageMargin * 2);

        var document = new FlowDocument
        {
            PageWidth = width,
            PageHeight = height,
            PagePadding = new Thickness(PageMargin),
            FontFamily = UiFont,
            FontSize = 7.5,
            ColumnWidth = double.PositiveInfinity,
            LineHeight = 10,
            TextAlignment = TextAlignment.Left
        };

        document.Blocks.Add(BuildHeader(statement, brandingPathResolver, contentWidth));
        document.Blocks.Add(BuildTwoTables(statement, contentWidth));
        document.Blocks.Add(BuildSummary(statement, contentWidth));
        document.Blocks.Add(BuildFooter(statement, contentWidth));
        return document;
    }

    private static Block BuildHeader(
        FeeTypeStatementDto s,
        IDocumentBrandingPathResolver brandingPathResolver,
        double contentWidth)
    {
        var root = new Grid
        {
            Width = contentWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 6)
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Gauche : logo + école, titre + n° sous le logo
        var left = new StackPanel();
        var schoolRow = new Grid();
        schoolRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        schoolRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logoPath = brandingPathResolver.ResolveAbsolutePath(s.Branding.PrimaryLogoPath)
            ?? brandingPathResolver.ResolveAbsolutePath(s.Branding.HeaderImagePath);
        var logo = CreateImage(logoPath, 42, 42);
        if (logo is not null)
        {
            logo.Margin = new Thickness(0, 0, 8, 0);
            logo.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(logo, 0);
            schoolRow.Children.Add(logo);
        }

        var schoolInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        schoolInfo.Children.Add(new TextBlock
        {
            Text = s.SchoolName.ToUpperInvariant(),
            FontWeight = FontWeights.Bold,
            FontSize = 8.5,
            Foreground = Navy,
            TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(s.SchoolMotto))
        {
            schoolInfo.Children.Add(Txt(s.SchoolMotto!, 6.2, TextMuted, italic: true));
        }

        if (!string.IsNullOrWhiteSpace(s.SchoolAddress))
        {
            schoolInfo.Children.Add(Txt(s.SchoolAddress!, 6.2, TextMuted));
        }

        var contact = string.Join("  ·  ", new[] { s.SchoolPhone, s.SchoolEmail }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(contact))
        {
            schoolInfo.Children.Add(Txt(contact, 6.2, TextMuted));
        }

        Grid.SetColumn(schoolInfo, 1);
        schoolRow.Children.Add(schoolInfo);
        left.Children.Add(schoolRow);

        var titleBlock = new TextBlock
        {
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        titleBlock.Inlines.Add(new Run($"{BuildDocumentTitle(s.FeeTypeName)} ")
        {
            FontWeight = FontWeights.Bold,
            FontSize = 9.5,
            Foreground = Navy
        });
        titleBlock.Inlines.Add(new Run($"n°{s.StatementNumber}")
        {
            FontWeight = FontWeights.Bold,
            FontSize = 9.5,
            Foreground = PrimaryBlue
        });
        left.Children.Add(titleBlock);

        // Droite : infos élève (nom complet)
        var studentBox = new Border
        {
            Background = LightBlue,
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 6, 8, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Children =
                {
                    Txt($"Nom complet : {OrDash(s.StudentName)}", 8, TextDark, bold: true),
                    Txt($"Matricule : {OrDash(s.StudentRegistrationNumber)}", 7.5, TextMuted),
                    Txt($"Classe : {OrDash(s.ClassName)}", 7.5, TextMuted),
                    Txt($"Année scolaire : {OrDash(s.AcademicYearLabel)}", 7.5, TextMuted)
                }
            }
        };

        Grid.SetColumn(left, 0);
        Grid.SetColumn(studentBox, 2);
        root.Children.Add(left);
        root.Children.Add(studentBox);
        return new BlockUIContainer(root);
    }

    private static string BuildDocumentTitle(string feeTypeName) =>
        $"RELEVÉ DE {feeTypeName.Trim().ToUpperInvariant()}";

    private static string OrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static Block BuildTwoTables(FeeTypeStatementDto s, double contentWidth)
    {
        var currency = s.Currency.ToString();
        var gap = 8.0;
        var grid = FullWidthGrid(contentWidth, bottomMargin: 6);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(gap) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var historyRows = s.PaymentHistory.Select(l => new[]
        {
            l.Number.ToString("00"),
            l.InstallmentName,
            l.PaymentDate.ToLocalTime().ToString("dd/MM/yyyy", Fr),
            l.AmountPaid.ToString("N2", Fr),
            l.ReceiptNumber
        }).ToList();

        var situationRows = s.InstallmentSituations.Select(l => new[]
        {
            l.Number.ToString("00"),
            l.InstallmentName,
            l.AmountExpected.ToString("N2", Fr),
            l.AmountPaid.ToString("N2", Fr),
            l.Remaining.ToString("N2", Fr)
        }).ToList();
        var remainings = s.InstallmentSituations.Select(l => (decimal?)l.Remaining).ToList();

        // Lignes vides uniquement si l'historique dépasse la situation globale.
        while (situationRows.Count < historyRows.Count)
        {
            situationRows.Add(["—", "—", "—", "—", "—"]);
            remainings.Add(null);
        }

        if (historyRows.Count == 0)
        {
            historyRows.Add(["—", "Aucun paiement", "—", "—", "—"]);
        }

        if (situationRows.Count == 0)
        {
            situationRows.Add(["—", "Aucune tranche", "—", "—", "—"]);
            remainings.Add(null);
        }

        var halfWidth = (contentWidth - gap) / 2;

        var left = BuildCardTable(
            "HISTORIQUE DES PAIEMENTS",
            ["N°", "TRANCHE", "DATE PAIEMENT", $"MONTANT PAYÉ ({currency})", "N° REÇU"],
            [0.4, 1.5, 1.2, 1.3, 1.1],
            historyRows,
            halfWidth);

        var right = BuildCardTable(
            "SITUATION GLOBALE",
            ["N°", "TRANCHE", $"MONTANT PRÉVU ({currency})", $"DÉJÀ PAYÉ ({currency})", $"SOLDE RESTANT ({currency})"],
            [0.4, 1.5, 1.3, 1.2, 1.3],
            situationRows,
            halfWidth,
            balanceColumnIndex: 4,
            remainingValues: remainings);

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return new BlockUIContainer(grid);
    }

    private static Border BuildCardTable(
        string title,
        string[] headers,
        double[] weights,
        IReadOnlyList<string[]> rows,
        double width,
        int? balanceColumnIndex = null,
        IReadOnlyList<decimal?>? remainingValues = null)
    {
        var border = new Border
        {
            Width = width,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3)
        };

        var stack = new StackPanel { Width = width };
        stack.Children.Add(new Border
        {
            Background = Navy,
            Padding = new Thickness(6, 4, 6, 4),
            Child = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 7,
                Foreground = Brushes.White
            }
        });

        var table = new Grid { Width = width };
        foreach (var w in weights)
        {
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
        }

        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = new Border
            {
                Background = HeaderCellBg,
                Padding = new Thickness(4, 3, 4, 3),
                Child = new TextBlock
                {
                    Text = headers[c],
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 6.5,
                    TextWrapping = TextWrapping.Wrap
                }
            };
            Grid.SetRow(cell, 0);
            Grid.SetColumn(cell, c);
            table.Children.Add(cell);
        }

        for (var r = 0; r < rows.Count; r++)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var bg = r % 2 == 1 ? Zebra : Brushes.White;
            var values = rows[r];
            for (var c = 0; c < values.Length; c++)
            {
                Brush fg = TextDark;
                var bold = false;
                if (balanceColumnIndex == c && remainingValues is not null && r < remainingValues.Count && remainingValues[r] is decimal rem)
                {
                    fg = rem <= 0 ? GreenPaid : RedDue;
                    bold = true;
                }
                else if (values[c] == "—")
                {
                    fg = TextMuted;
                }

                var cell = new Border
                {
                    Background = bg,
                    BorderBrush = BorderBlue,
                    BorderThickness = new Thickness(0, 0, 0, 0.5),
                    Padding = new Thickness(4, 3, 4, 3),
                    Child = new TextBlock
                    {
                        Text = values[c],
                        FontSize = 7.2,
                        Foreground = fg,
                        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                        TextWrapping = TextWrapping.NoWrap
                    }
                };
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                table.Children.Add(cell);
            }
        }

        stack.Children.Add(table);
        border.Child = stack;
        return border;
    }

    private static Block BuildSummary(FeeTypeStatementDto s, double contentWidth)
    {
        var currency = s.Currency.ToString();
        var remainingBrush = s.TotalRemaining <= 0 ? GreenPaid : RedDue;

        var border = new Border
        {
            Width = contentWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderBrush = BorderBlue,
            BorderThickness = new Thickness(1),
            Background = SoftBlue,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid { Width = contentWidth - 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = "Récapitulatif :",
            FontWeight = FontWeights.Bold,
            FontSize = 8.5,
            Foreground = Navy,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        var prevu = CompactAmount($"Prévu ({currency})", s.TotalExpected.ToString("N2", Fr), PrimaryBlue);
        var paye = CompactAmount($"Payé ({currency})", s.TotalPaid.ToString("N2", Fr), GreenPaid);
        var reste = CompactAmount($"Reste ({currency})", s.TotalRemaining.ToString("N2", Fr), remainingBrush);

        Grid.SetColumn(label, 0);
        Grid.SetColumn(prevu, 1);
        Grid.SetColumn(paye, 2);
        Grid.SetColumn(reste, 3);
        grid.Children.Add(label);
        grid.Children.Add(prevu);
        grid.Children.Add(paye);
        grid.Children.Add(reste);
        border.Child = grid;
        return new BlockUIContainer(border);
    }

    private static TextBlock CompactAmount(string label, string amount, Brush accent)
    {
        var tb = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        tb.Inlines.Add(new Run(label + " ") { FontSize = 7, Foreground = TextMuted });
        tb.Inlines.Add(new Run(amount) { FontSize = 11, FontWeight = FontWeights.Bold, Foreground = accent });
        return tb;
    }

    private static Block BuildFooter(FeeTypeStatementDto s, double contentWidth)
    {
        var grid = FullWidthGrid(contentWidth, bottomMargin: 0);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sig1 = SignatureBlock("Signature Caissier", cashierName: s.CashierName);
        var sig2 = SignatureBlock(
            "Signature Parent / Tuteur",
            dateTimeText: s.EditedAt.ToString("dd/MM/yyyy HH:mm", Fr));

        Grid.SetColumn(sig1, 0);
        Grid.SetColumn(sig2, 2);
        grid.Children.Add(sig1);
        grid.Children.Add(sig2);
        return new BlockUIContainer(grid);
    }

    private static StackPanel SignatureBlock(
        string label,
        string? cashierName = null,
        string? dateTimeText = null)
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        if (label.Contains("Caissier", StringComparison.Ordinal))
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Caissier : {OrDash(cashierName)}",
                FontSize = 7.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextDark,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(8, 0, 8, 8)
            });
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text = dateTimeText ?? string.Empty,
                FontSize = 7.5,
                Foreground = TextMuted,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 0, 8, 8)
            });
        }

        panel.Children.Add(new Border
        {
            BorderBrush = TextMuted,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Height = 20,
            Margin = new Thickness(8, 0, 8, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 7,
            Foreground = TextMuted,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });
        return panel;
    }

    private static Grid FullWidthGrid(double contentWidth, double bottomMargin) =>
        new()
        {
            Width = contentWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, bottomMargin)
        };

    private static StackPanel MetaRow(string label, string value, Brush? valueBrush = null, bool boldValue = false)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        var l = Txt(label, 6.2, TextMuted);
        var v = Txt(value, 6.8, valueBrush ?? TextDark, bold: boldValue);
        v.HorizontalAlignment = HorizontalAlignment.Right;
        v.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(l, 0);
        Grid.SetColumn(v, 1);
        row.Children.Add(l);
        row.Children.Add(v);
        return new StackPanel { Children = { row } };
    }

    private static TextBlock Txt(string text, double size, Brush fg, bool bold = false, bool italic = false) =>
        new()
        {
            Text = text,
            FontSize = size,
            Foreground = fg,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
            TextWrapping = TextWrapping.Wrap
        };

    private static Image? CreateImage(string? path, double maxWidth, double maxHeight)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.DecodePixelWidth = (int)maxWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            return new Image
            {
                Source = bitmap,
                Width = maxWidth,
                Height = maxHeight,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left
            };
        }
        catch
        {
            return null;
        }
    }

    private static SolidColorBrush BrushFrom(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }
}
