using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SchoolManagement.Desktop.UI;
using QuestPDF.Fluent;

namespace SchoolManagement.Desktop.Printing;

/// <summary>Point d'entrée commun des aperçus, sans lancement de lecteur externe.</summary>
public static class DocumentPreview
{
    public static bool Show(IDocumentPaginatorSource document, string title)
    {
        var window = new DocumentPreviewWindow(title, document, null);
        window.ShowDialog();
        return window.Printed;
    }

    public static void ShowPdf(string path, string title)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("PDF introuvable.", path);
        new DocumentPreviewWindow(title, null, File.ReadAllBytes(path)).ShowDialog();
    }

    public static void ShowPdf(byte[] bytes, string title) =>
        new DocumentPreviewWindow(title, null, bytes).ShowDialog();

    public static byte[] ToPdf(IDocumentPaginatorSource source)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var paginator = source.DocumentPaginator;
        paginator.ComputePageCount();
        if (paginator.PageCount == 0) throw new InvalidOperationException("Aucune page à exporter.");
        // Snapshot des pages WPF : mêmes dimensions et même pagination que l'aperçu.
        var pages = new List<(byte[] Image, Size Size)>();
        for (var i = 0; i < paginator.PageCount; i++)
        {
            var page = paginator.GetPage(i);
            var size = page.Size;
            var drawing = new DrawingVisual();
            using (var dc = drawing.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(size));
                dc.DrawRectangle(new VisualBrush(page.Visual) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(size) }, null, new Rect(size));
            }
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * 2),
                (int)Math.Ceiling(size.Height * 2), 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(drawing);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            pages.Add((stream.ToArray(), size));
        }
        return QuestPDF.Fluent.Document.Create(container =>
        {
            foreach (var page in pages)
                container.Page(p =>
                {
                    p.Size((float)(page.Size.Width * 72 / 96), (float)(page.Size.Height * 72 / 96));
                    p.Margin(0);
                    p.Content().Image(page.Image).FitArea();
                });
        }).GeneratePdf();
    }

    public static FlowDocument Table(string title, string[] headings, IEnumerable<string[]> rows)
    {
        var doc = new FlowDocument { PageWidth = 1122.52, PageHeight = 793.7,
            PagePadding = new Thickness(32), ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily("Segoe UI"), FontSize = 11 };
        doc.Blocks.Add(new Paragraph(new Run(title)) { FontSize = 20, FontWeight = FontWeights.Bold });
        var table = new Table { CellSpacing = 0 };
        foreach (var _ in headings) table.Columns.Add(new TableColumn());
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        void AddRow(string[] values, bool header)
        {
            var row = new TableRow();
            foreach (var value in values)
                row.Cells.Add(new TableCell(new Paragraph(new Run(value ?? ""))) {
                    Padding = new Thickness(5), BorderThickness = new Thickness(0, 0, 0, 0.5),
                    BorderBrush = Brushes.LightGray, FontWeight = header ? FontWeights.Bold : FontWeights.Normal });
            group.Rows.Add(row);
        }
        AddRow(headings, true);
        foreach (var row in rows) AddRow(row, false);
        doc.Blocks.Add(table);
        return doc;
    }
}

internal sealed class DocumentPreviewWindow : Window
{
    private static readonly Brush PreviewBackground = CreatePreviewBackground();
    private readonly IDocumentPaginatorSource? _document;
    private readonly byte[]? _pdf;
    private readonly WebView2? _browser;
    private readonly TextBlock _status = new() { Margin = new Thickness(12), Text = "Aperçu avant impression" };
    private readonly Button _print = new() { Content = "Imprimer…", Margin = new Thickness(6), Padding = new Thickness(14, 6, 14, 6) };
    private string? _tempFile;
    private bool _closed;
    public bool Printed { get; private set; }

    public DocumentPreviewWindow(string title, IDocumentPaginatorSource? document, byte[]? pdf)
    {
        Title = $"Aperçu avant impression — {title}";
        Width = 1100; Height = 800; MinWidth = 650; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = ErpFileDialog.ResolveOwnerWindow();
        _document = document; _pdf = pdf;
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, (_, args) => { Print(title); args.Handled = true; }));
        AddHandler(CommandManager.PreviewExecutedEvent, new ExecutedRoutedEventHandler((_, args) =>
        {
            if (args.Command != ApplicationCommands.Print) return;
            Print(title); args.Handled = true;
        }));
        var root = new DockPanel { Background = Brushes.White };
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = Brushes.White
        };
        DockPanel.SetDock(toolbar, Dock.Top);
        toolbar.Children.Add(_print);
        var save = new Button { Content = "Enregistrer en PDF…", Margin = new Thickness(6), Padding = new Thickness(14, 6, 14, 6) };
        toolbar.Children.Add(save);
        root.Children.Add(toolbar);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        if (document is FlowDocument flow)
        {
            if (double.IsNaN(flow.PageWidth)) flow.PageWidth = 793.7;
            if (double.IsNaN(flow.PageHeight)) flow.PageHeight = 1122.52;
            root.Children.Add(CreatePagePreview(flow));
        }
        else if (document is not null)
            root.Children.Add(CreatePagePreview(document));
        else
        {
            _print.IsEnabled = false;
            _browser = new WebView2(); root.Children.Add(_browser);
            Loaded += LoadPdf;
        }
        Content = root;
        _print.Click += (_, _) => Print(title);
        save.Click += (_, _) => Save(title);
        Closed += (_, _) => { _closed = true; _browser?.Dispose(); if (_tempFile is not null) { try { File.Delete(_tempFile); } catch (IOException) { } } };
    }

    private static Brush CreatePreviewBackground()
    {
        var brush = new SolidColorBrush(Color.FromRgb(96, 96, 96));
        brush.Freeze();
        return brush;
    }

    private static ScrollViewer CreatePagePreview(IDocumentPaginatorSource source)
    {
        var paginator = source.DocumentPaginator;
        paginator.ComputePageCount();

        var pages = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = Brushes.Transparent
        };

        for (var index = 0; index < paginator.PageCount; index++)
        {
            var page = paginator.GetPage(index);
            var pageVisual = new System.Windows.Shapes.Rectangle
            {
                Width = page.Size.Width,
                Height = page.Size.Height,
                Fill = new VisualBrush(page.Visual)
                {
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(page.Size),
                    Stretch = Stretch.Fill
                }
            };

            pages.Children.Add(new Border
            {
                Width = page.Size.Width,
                Height = page.Size.Height,
                Margin = new Thickness(28, index == 0 ? 28 : 14, 28, 14),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(190, 190, 190)),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 3,
                    Opacity = 0.45,
                    Color = Colors.Black
                },
                Child = pageVisual
            });
        }

        return new ScrollViewer
        {
            Background = PreviewBackground,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Top,
            Content = pages
        };
    }

    private async void LoadPdf(object sender, RoutedEventArgs e)
    {
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ERP_Scolaire", "PdfPreview");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
            if (_closed) return;
            await _browser!.EnsureCoreWebView2Async(environment);
            if (_closed) return;
            _browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _browser.CoreWebView2.Settings.HiddenPdfToolbarItems = CoreWebView2PdfToolbarItems.Save | CoreWebView2PdfToolbarItems.SaveAs;
            _browser.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
            _tempFile = Path.Combine(Path.GetTempPath(), $"erp-preview-{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(_tempFile, _pdf!);
            if (_closed) { File.Delete(_tempFile); return; }
            _browser.NavigationCompleted += (_, args) => {
                _print.IsEnabled = args.IsSuccess;
                _status.Text = args.IsSuccess ? "Utilisez la barre du document pour le zoom et les pages." : "Impossible de charger l’aperçu. Vous pouvez enregistrer le PDF.";
            };
            _browser.Source = new Uri(_tempFile);
        }
        catch (Exception ex)
        {
            if (!_closed) _status.Text = $"Aperçu PDF indisponible (WebView2 requis). Vous pouvez enregistrer le PDF. {ex.Message}";
        }
    }

    private void Print(string title)
    {
        try
        {
            if (_document is null)
            {
                if (!_print.IsEnabled || _browser?.CoreWebView2 is null) return;
                _browser.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
                _status.Text = "Choisissez l’imprimante et confirmez dans la boîte d’impression.";
                return;
            }
            var paginator = _document.DocumentPaginator;
            paginator.ComputePageCount();
            if (paginator.PageCount == 0)
            {
                _status.Text = "Aucune page à imprimer.";
                return;
            }
            var dialog = new PrintDialog { UserPageRangeEnabled = true, MinPage = 1, MaxPage = (uint)paginator.PageCount };
            var first = paginator.GetPage(0).Size;
            dialog.PrintTicket.PageOrientation = first.Width > first.Height
                ? System.Printing.PageOrientation.Landscape : System.Printing.PageOrientation.Portrait;
            dialog.PrintTicket.PageMediaSize = new System.Printing.PageMediaSize(first.Width, first.Height);
            if (dialog.ShowDialog() != true) return;
            var from = dialog.PageRangeSelection == PageRangeSelection.UserPages ? dialog.PageRange.PageFrom - 1 : 0;
            var to = dialog.PageRangeSelection == PageRangeSelection.UserPages ? dialog.PageRange.PageTo - 1 : paginator.PageCount - 1;
            dialog.PrintDocument(new PreviewPrintPaginator(paginator, from, to,
                new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight)), title);
            Printed = true;
            _status.Text = "Document transmis à l’imprimante.";
        }
        catch (Exception ex) { _status.Text = $"Impression impossible : {ex.Message}"; }
    }

    private void Save(string title)
    {
        try
        {
            var name = string.Concat(title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var dialog = new Microsoft.Win32.SaveFileDialog { FileName = name + ".pdf", Filter = "Document PDF|*.pdf", DefaultExt = ".pdf" };
            if (dialog.ShowDialog(this) != true) return;
            File.WriteAllBytes(dialog.FileName, _pdf ?? DocumentPreview.ToPdf(_document!));
            _status.Text = $"PDF enregistré : {dialog.FileName}";
        }
        catch (Exception ex) { _status.Text = $"Enregistrement impossible : {ex.Message}"; }
    }
}

internal sealed class PreviewPrintPaginator : DocumentPaginator
{
    private readonly DocumentPaginator _source;
    private readonly int _first;
    private readonly int _last;
    public PreviewPrintPaginator(DocumentPaginator source, int first, int last, Size size)
    {
        _source = source;
        _first = Math.Clamp(first, 0, source.PageCount - 1);
        _last = Math.Clamp(last, _first, source.PageCount - 1);
        PageSize = size;
    }
    public override bool IsPageCountValid => true;
    public override int PageCount => _last - _first + 1;
    public override Size PageSize { get; set; }
    public override IDocumentPaginatorSource Source => _source.Source;
    public override DocumentPage GetPage(int pageNumber)
    {
        if (pageNumber < 0 || pageNumber >= PageCount) return DocumentPage.Missing;
        var page = _source.GetPage(_first + pageNumber);
        var scale = Math.Min(1, Math.Min(PageSize.Width / page.Size.Width, PageSize.Height / page.Size.Height));
        var target = new Rect((PageSize.Width - page.Size.Width * scale) / 2,
            (PageSize.Height - page.Size.Height * scale) / 2, page.Size.Width * scale, page.Size.Height * scale);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(page.Visual) {
            ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(page.Size) }, null, target);
        return new DocumentPage(visual, PageSize, new Rect(PageSize), new Rect(PageSize));
    }
}
