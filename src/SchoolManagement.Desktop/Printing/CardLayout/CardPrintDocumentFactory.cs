using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Printing.CardLayout;

/// <summary>Mode d'impression des cartes élèves.</summary>
public enum CardPrintLayoutKind
{
    /// <summary>Une face de carte = une page (job unique multi-pages).</summary>
    Individual = 1,

    /// <summary>Planche A4 : 2 colonnes × N rangées, puis planches verso.</summary>
    A4Sheet = 2
}

public sealed record CardPrintSideVisual(
    FrameworkElement Visual,
    double WidthMm,
    double HeightMm,
    string CardNumber,
    bool IsBack);

public sealed record CardPrintPair(
    CardPrintSideVisual Front,
    CardPrintSideVisual? Back);

/// <summary>Compose un FixedDocument pour impression en un seul job.</summary>
public static class CardPrintDocumentFactory
{
    public const double A4WidthMm = 210;
    public const double A4HeightMm = 297;
    public const int A4Columns = 2;
    public const int A4DefaultRows = 5;

    public static FixedDocument Build(
        IReadOnlyList<CardPrintPair> cards,
        CardPrintLayoutKind layout,
        int rowsPerPage = A4DefaultRows)
    {
        return layout == CardPrintLayoutKind.A4Sheet
            ? BuildA4Document(cards, Math.Clamp(rowsPerPage, 4, 5))
            : BuildIndividualDocument(cards);
    }

    private static FixedDocument BuildIndividualDocument(IReadOnlyList<CardPrintPair> cards)
    {
        var doc = new FixedDocument();
        foreach (var pair in cards)
        {
            AddPage(doc, CreateSingleCardPage(pair.Front));
            if (pair.Back is not null)
                AddPage(doc, CreateSingleCardPage(pair.Back));
        }

        return doc;
    }

    private static FixedDocument BuildA4Document(IReadOnlyList<CardPrintPair> cards, int rows)
    {
        var doc = new FixedDocument();
        var perPage = A4Columns * rows;
        for (var offset = 0; offset < cards.Count; offset += perPage)
        {
            var chunk = cards.Skip(offset).Take(perPage).ToList();
            AddPage(doc, CreateA4GridPage(
                chunk.Select(c => c.Front).ToList(),
                rows,
                mirrorColumns: false,
                title: "Recto"));

            if (chunk.Any(c => c.Back is not null))
            {
                var backs = chunk
                    .Select(c => c.Back ?? CreateBlankSide(c.Front))
                    .ToList();
                // Miroir horizontal pour impression verso (retournement bord long).
                AddPage(doc, CreateA4GridPage(backs, rows, mirrorColumns: true, title: "Verso"));
            }
        }

        return doc;
    }

    private static CardPrintSideVisual CreateBlankSide(CardPrintSideVisual front) =>
        new(
            new Border
            {
                Width = CardLayoutUnits.MmToDip(front.WidthMm),
                Height = CardLayoutUnits.MmToDip(front.HeightMm),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1)
            },
            front.WidthMm,
            front.HeightMm,
            front.CardNumber,
            IsBack: true);

    private static FixedPage CreateSingleCardPage(CardPrintSideVisual side)
    {
        var w = CardLayoutUnits.MmToDip(side.WidthMm);
        var h = CardLayoutUnits.MmToDip(side.HeightMm);
        var page = new FixedPage
        {
            Width = w,
            Height = h,
            Background = Brushes.White
        };

        var visual = PrepareVisual(side.Visual, w, h);
        FixedPage.SetLeft(visual, 0);
        FixedPage.SetTop(visual, 0);
        page.Children.Add(visual);
        return page;
    }

    private static FixedPage CreateA4GridPage(
        IReadOnlyList<CardPrintSideVisual> sides,
        int rows,
        bool mirrorColumns,
        string title)
    {
        var pageW = CardLayoutUnits.MmToDip(A4WidthMm);
        var pageH = CardLayoutUnits.MmToDip(A4HeightMm);
        var page = new FixedPage
        {
            Width = pageW,
            Height = pageH,
            Background = Brushes.White
        };

        var marginX = CardLayoutUnits.MmToDip(8);
        var marginY = CardLayoutUnits.MmToDip(10);
        var gapX = CardLayoutUnits.MmToDip(6);
        var gapY = CardLayoutUnits.MmToDip(4);

        var cardWMm = sides.Count > 0 ? sides[0].WidthMm : 85.6;
        var cardHMm = sides.Count > 0 ? sides[0].HeightMm : 53.98;
        var cardW = CardLayoutUnits.MmToDip(cardWMm);
        var cardH = CardLayoutUnits.MmToDip(cardHMm);

        var cellW = (pageW - 2 * marginX - (A4Columns - 1) * gapX) / A4Columns;
        var cellH = (pageH - 2 * marginY - (rows - 1) * gapY) / rows;
        var scale = Math.Min(1.0, Math.Min(cellW / cardW, cellH / cardH));
        var drawW = cardW * scale;
        var drawH = cardH * scale;

        for (var i = 0; i < sides.Count; i++)
        {
            var col = i % A4Columns;
            var row = i / A4Columns;
            if (mirrorColumns)
                col = A4Columns - 1 - col;

            var cellLeft = marginX + col * (cellW + gapX);
            var cellTop = marginY + row * (cellH + gapY);
            var left = cellLeft + (cellW - drawW) / 2;
            var top = cellTop + (cellH - drawH) / 2;

            var host = new Border
            {
                Width = drawW,
                Height = drawH,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0.6),
                Background = Brushes.White,
                Child = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    Child = PrepareVisual(
                        sides[i].Visual,
                        CardLayoutUnits.MmToDip(sides[i].WidthMm),
                        CardLayoutUnits.MmToDip(sides[i].HeightMm))
                }
            };

            FixedPage.SetLeft(host, left);
            FixedPage.SetTop(host, top);
            page.Children.Add(host);
        }

        // Repère discret recto/verso
        var label = new TextBlock
        {
            Text = title,
            FontSize = 8,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            Opacity = 0.7
        };
        FixedPage.SetLeft(label, marginX);
        FixedPage.SetTop(label, pageH - marginY + 2);
        page.Children.Add(label);

        return page;
    }

    private static FrameworkElement PrepareVisual(FrameworkElement visual, double width, double height)
    {
        visual.Width = width;
        visual.Height = height;
        visual.Measure(new Size(width, height));
        visual.Arrange(new Rect(0, 0, width, height));
        visual.UpdateLayout();
        return visual;
    }

    private static void AddPage(FixedDocument document, FixedPage page)
    {
        var content = new PageContent();
        ((System.Windows.Markup.IAddChild)content).AddChild(page);
        document.Pages.Add(content);
    }
}
