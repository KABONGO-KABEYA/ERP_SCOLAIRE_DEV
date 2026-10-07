using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SchoolManagement.Domain.Entities.Documents;

namespace SchoolManagement.Desktop.Printing;

public sealed record ParentNoticePrintOptions(
    ParentNoticePageLayout PageLayout,
    ParentNoticePageOrientation PageOrientation,
    bool IncludeSchoolHeader,
    string? HeaderImagePath = null,
    decimal HeaderMarginLeftMm = 0,
    decimal HeaderMarginRightMm = 0,
    decimal? HeaderMaxHeightMm = null);

public static class ParentNoticeDocumentBuilder
{
    private const double A4Width = 793.7;
    private const double A4Height = 1122.52;
    private const double A5Width = A4Height / 2d;
    private const double A5Height = A4Width;

    public static FlowDocument Build(
        string rtfBase64,
        IReadOnlyDictionary<string, string> values,
        ParentNoticePrintOptions? options = null)
    {
        options ??= new ParentNoticePrintOptions(
            ParentNoticePageLayout.FullPage,
            ParentNoticePageOrientation.Portrait,
            false);
        var document = Load(rtfBase64);
        ReplaceTokens(document, values);
        var (pageWidth, pageHeight) = GetNoticePageSize(options.PageLayout, options.PageOrientation);
        document.PageWidth = pageWidth;
        document.PageHeight = pageHeight;
        document.PagePadding = options.PageLayout == ParentNoticePageLayout.TwoPerPage
            ? new Thickness(30, 24, 30, 24)
            : new Thickness(65, 55, 65, 55);
        document.ColumnWidth = double.PositiveInfinity;

        if (options.IncludeSchoolHeader && !string.IsNullOrWhiteSpace(options.HeaderImagePath))
            InsertHeader(document, options);

        return document;
    }

    public static FlowDocument Combine(
        IEnumerable<FlowDocument> notices,
        ParentNoticePageLayout pageLayout = ParentNoticePageLayout.FullPage,
        ParentNoticePageOrientation pageOrientation = ParentNoticePageOrientation.Portrait)
    {
        var source = notices.ToList();
        if (pageLayout == ParentNoticePageLayout.TwoPerPage)
            return CombineTwoUpLandscape(source);

        var (pageWidth, pageHeight) = GetNoticePageSize(pageLayout, pageOrientation);
        var combined = new FlowDocument
        {
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            PagePadding = new Thickness(65, 55, 65, 55),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily("Times New Roman"),
            FontSize = 12
        };

        var index = 0;
        foreach (var notice in source)
        {
            var section = new Section
            {
                BreakPageBefore = index > 0,
                Margin = new Thickness(0)
            };

            while (notice.Blocks.FirstBlock is { } block)
            {
                notice.Blocks.Remove(block);
                section.Blocks.Add(block);
            }
            combined.Blocks.Add(section);
            index++;
        }

        return combined;
    }

    private static FlowDocument CombineTwoUpLandscape(IReadOnlyList<FlowDocument> notices)
    {
        var combined = new FlowDocument
        {
            PageWidth = A4Height,
            PageHeight = A4Width,
            PagePadding = new Thickness(24, 22, 24, 22),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily("Times New Roman"),
            FontSize = 12
        };

        for (var index = 0; index < notices.Count; index += 2)
        {
            var table = new Table
            {
                CellSpacing = 0,
                BreakPageBefore = index > 0,
                Margin = new Thickness(0)
            };
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var rowGroup = new TableRowGroup();
            var row = new TableRow();
            row.Cells.Add(CreateNoticeCell(notices[index], true));
            row.Cells.Add(index + 1 < notices.Count
                ? CreateNoticeCell(notices[index + 1], false)
                : new TableCell(new Paragraph()) { Padding = new Thickness(18, 0, 0, 0) });
            rowGroup.Rows.Add(row);
            table.RowGroups.Add(rowGroup);
            combined.Blocks.Add(table);
        }

        return combined;
    }

    private static TableCell CreateNoticeCell(FlowDocument notice, bool isLeft)
    {
        var cell = new TableCell
        {
            Padding = isLeft ? new Thickness(0, 0, 18, 0) : new Thickness(18, 0, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            BorderThickness = isLeft ? new Thickness(0, 0, 1, 0) : new Thickness(0)
        };
        while (notice.Blocks.FirstBlock is { } block)
        {
            notice.Blocks.Remove(block);
            cell.Blocks.Add(block);
        }
        return cell;
    }

    private static (double Width, double Height) GetNoticePageSize(
        ParentNoticePageLayout layout,
        ParentNoticePageOrientation orientation)
    {
        if (layout == ParentNoticePageLayout.TwoPerPage)
            return (A5Width, A5Height);
        return orientation == ParentNoticePageOrientation.Landscape
            ? (A4Height, A4Width)
            : (A4Width, A4Height);
    }

    public static string CreateDefaultTemplate()
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Times New Roman"),
            FontSize = 12
        };
        document.Blocks.Add(new Paragraph(new Run("AVIS AUX PARENTS"))
        {
            FontFamily = new FontFamily("Arial"),
            Foreground = new SolidColorBrush(Color.FromRgb(15, 49, 99)),
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });
        document.Blocks.Add(new Paragraph(new Run("({{Avis.Objet}})"))
        {
            Foreground = new SolidColorBrush(Color.FromRgb(15, 49, 99)),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        });
        document.Blocks.Add(new Paragraph(new Run("Cher parent de l’élève {{Eleve.NomComplet}}, inscrit(e) en {{Eleve.Classe}},"))
        {
            TextAlignment = TextAlignment.Justify
        });
        document.Blocks.Add(new Paragraph(new Run(
            "Nous vous rappelons que les tranches {{Tranches.Selectionnees}}, d’un montant total prévu de " +
            "{{Tranches.TotalPrevu}}, sont concernées par le présent avis. Sur le montant annuel de " +
            "{{Frais.TotalAnnuel}}, la somme totale déjà payée est de {{Frais.TotalPaye}}."))
        {
            TextAlignment = TextAlignment.Justify
        });
        document.Blocks.Add(new Paragraph(new Run("Pour rappel :"))
        {
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 4)
        });
        document.Blocks.Add(new Paragraph(new Run(
            "• Total des tranches sélectionnées : {{Tranches.TotalPrevu}}\n" +
            "• Déjà payé sur ces tranches : {{Tranches.TotalPaye}}\n" +
            "• Montant restant sur ces tranches : {{Tranches.Reste}}\n" +
            "• Reste annuel à payer : {{Frais.ResteAnnuel}}"))
        {
            Margin = new Thickness(20, 0, 0, 8)
        });
        document.Blocks.Add(new Paragraph(new Run(
            "Nous vous prions de bien vouloir régulariser la situation dans le délai indiqué. " +
            "Nous restons à votre disposition pour toute information complémentaire."))
        {
            TextAlignment = TextAlignment.Justify
        });
        document.Blocks.Add(new Paragraph(new Run("Fait le {{Avis.Date}}\nLa Direction"))
        {
            TextAlignment = TextAlignment.Right,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 0)
        });
        return Save(document);
    }

    private static void InsertHeader(FlowDocument document, ParentNoticePrintOptions options)
    {
        if (!File.Exists(options.HeaderImagePath)) return;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(options.HeaderImagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        var mmToDip = 96d / 25.4d;
        var maxHeightMm = options.HeaderMaxHeightMm ??
            (options.PageLayout == ParentNoticePageLayout.TwoPerPage ? 28m : 55m);
        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxHeight = (double)maxHeightMm * mmToDip,
            Margin = new Thickness(
                (double)options.HeaderMarginLeftMm * mmToDip,
                0,
                (double)options.HeaderMarginRightMm * mmToDip,
                options.PageLayout == ParentNoticePageLayout.TwoPerPage ? 4 : 10)
        };
        var container = new BlockUIContainer(image) { Margin = new Thickness(0) };
        if (document.Blocks.FirstBlock is { } first)
            document.Blocks.InsertBefore(first, container);
        else
            document.Blocks.Add(container);
    }

    private static FlowDocument Load(string rtfBase64)
    {
        var document = new FlowDocument();
        using var stream = new MemoryStream(Convert.FromBase64String(rtfBase64));
        new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.Rtf);
        return document;
    }

    private static string Save(FlowDocument document)
    {
        using var stream = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.Rtf);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static void ReplaceTokens(DependencyObject root, IReadOnlyDictionary<string, string> values)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).Cast<object>().ToList())
        {
            if (child is Run run)
            {
                var original = run.Text;
                foreach (var pair in values)
                    run.Text = run.Text.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
                if (!string.Equals(original, run.Text, StringComparison.Ordinal))
                    run.Background = Brushes.Transparent;
            }
            if (child is DependencyObject nested) ReplaceTokens(nested, values);
        }
    }
}
