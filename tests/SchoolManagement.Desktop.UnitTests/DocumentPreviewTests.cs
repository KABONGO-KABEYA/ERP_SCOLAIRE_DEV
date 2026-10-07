using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SchoolManagement.Desktop.Printing;
using Xunit;

namespace SchoolManagement.Desktop.UnitTests;

public sealed class DocumentPreviewTests
{
    [Fact]
    public void Export_produces_real_pdf_with_multiple_pages() => OnSta(() =>
    {
        var doc = new FixedDocument();
        for (var i = 0; i < 3; i++)
        {
            var page = new FixedPage { Width = 400, Height = 600 };
            page.Children.Add(new TextBlock { Text = $"Page {i + 1} — élève", FontSize = 20, Margin = new Thickness(30) });
            page.Measure(new Size(400, 600)); page.Arrange(new Rect(0, 0, 400, 600)); page.UpdateLayout();
            var content = new PageContent(); content.Child = page; doc.Pages.Add(content);
        }
        var pdf = DocumentPreview.ToPdf(doc);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 8));
        Assert.Contains("/Count 3", Encoding.ASCII.GetString(pdf));
    });

    [Fact]
    public void Selected_range_prints_only_selected_pages() => OnSta(() =>
    {
        var source = new TestPaginator();
        var paginator = new PreviewPrintPaginator(source, 1, 2, new Size(500, 700));
        Assert.Equal(2, paginator.PageCount);
        paginator.GetPage(0);
        Assert.Equal(1, source.LastPage);
        paginator.GetPage(1);
        Assert.Equal(2, source.LastPage);
        Assert.Same(DocumentPage.Missing, paginator.GetPage(2));
    });

    [Fact]
    public void Flow_document_can_be_exported_without_an_installed_pdf_printer() => OnSta(() =>
    {
        var doc = DocumentPreview.Table("Rapport de test", new[] { "Nom", "Montant" },
            Enumerable.Range(0, 80).Select(i => new[] { $"Élève {i}", "1 000 CDF" }));
        var bytes = DocumentPreview.ToPdf(doc);
        Assert.True(bytes.Length > 1000);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, 8));
    });

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class TestPaginator : DocumentPaginator
    {
        public int LastPage { get; private set; }
        public override bool IsPageCountValid => true;
        public override int PageCount => 4;
        public override Size PageSize { get; set; } = new(400, 600);
        public override IDocumentPaginatorSource Source => null!;
        public override DocumentPage GetPage(int number)
        {
            LastPage = number;
            return new DocumentPage(new System.Windows.Media.DrawingVisual(), PageSize, new Rect(PageSize), new Rect(PageSize));
        }
    }
}
