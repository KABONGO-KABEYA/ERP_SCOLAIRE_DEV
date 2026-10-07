using System.Windows.Documents;
using SchoolManagement.Desktop.Printing;
using SchoolManagement.Domain.Entities.Documents;
using Xunit;

namespace SchoolManagement.Desktop.UnitTests;

public sealed class ParentNoticeDocumentBuilderTests
{
    [Fact]
    public void Replaces_student_and_financial_fields_without_losing_document() => OnSta(() =>
    {
        var template = ParentNoticeDocumentBuilder.CreateDefaultTemplate();
        var document = ParentNoticeDocumentBuilder.Build(template, new Dictionary<string, string>
        {
            ["{{Eleve.NomComplet}}"] = "KABONGO Alice",
            ["{{Frais.Type}}"] = "Minerval",
            ["{{Frais.TotalAnnuel}}"] = "300 000 CDF",
            ["{{Frais.TotalPaye}}"] = "200 000 CDF",
            ["{{Frais.ResteAnnuel}}"] = "100 000 CDF",
            ["{{Frais.TableauTranches}}"] = "Première tranche : 100 000 CDF"
        });

        var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
        Assert.Contains("KABONGO Alice", text);
        Assert.Contains("100 000 CDF", text);
        Assert.DoesNotContain("{{Eleve.NomComplet}}", text);
    });

    [Fact]
    public void Combines_one_notice_per_recipient_with_page_breaks() => OnSta(() =>
    {
        var template = ParentNoticeDocumentBuilder.CreateDefaultTemplate();
        var first = ParentNoticeDocumentBuilder.Build(template, new Dictionary<string, string>
        {
            ["{{Eleve.NomComplet}}"] = "Élève 1"
        });
        var second = ParentNoticeDocumentBuilder.Build(template, new Dictionary<string, string>
        {
            ["{{Eleve.NomComplet}}"] = "Élève 2"
        });

        var combined = ParentNoticeDocumentBuilder.Combine([first, second]);
        var sections = combined.Blocks.OfType<Section>().ToArray();
        Assert.Equal(2, sections.Length);
        Assert.False(sections[0].BreakPageBefore);
        Assert.True(sections[1].BreakPageBefore);
    });

    [Fact]
    public void Combines_two_notices_per_a4_page() => OnSta(() =>
    {
        var template = ParentNoticeDocumentBuilder.CreateDefaultTemplate();
        var options = new ParentNoticePrintOptions(
            ParentNoticePageLayout.TwoPerPage,
            ParentNoticePageOrientation.Portrait,
            false);
        var documents = Enumerable.Range(1, 3)
            .Select(index => ParentNoticeDocumentBuilder.Build(template, new Dictionary<string, string>
            {
                ["{{Eleve.NomComplet}}"] = $"Élève {index}"
            }, options))
            .ToArray();

        var combined = ParentNoticeDocumentBuilder.Combine(documents, ParentNoticePageLayout.TwoPerPage);
        var sheets = combined.Blocks.OfType<Table>().ToArray();

        Assert.True(combined.PageWidth > combined.PageHeight);
        Assert.Equal(2, sheets.Length);
        Assert.False(sheets[0].BreakPageBefore);
        Assert.True(sheets[1].BreakPageBefore);
        Assert.Equal(2, sheets[0].RowGroups[0].Rows[0].Cells.Count);
        Assert.Equal(2, sheets[1].RowGroups[0].Rows[0].Cells.Count);
    });

    [Fact]
    public void Builds_full_page_in_selected_a4_orientation() => OnSta(() =>
    {
        var template = ParentNoticeDocumentBuilder.CreateDefaultTemplate();
        var portrait = ParentNoticeDocumentBuilder.Build(template, new Dictionary<string, string>(),
            new ParentNoticePrintOptions(
                ParentNoticePageLayout.FullPage,
                ParentNoticePageOrientation.Portrait,
                false));
        var landscape = ParentNoticeDocumentBuilder.Build(template, new Dictionary<string, string>(),
            new ParentNoticePrintOptions(
                ParentNoticePageLayout.FullPage,
                ParentNoticePageOrientation.Landscape,
                false));

        Assert.True(portrait.PageHeight > portrait.PageWidth);
        Assert.True(landscape.PageWidth > landscape.PageHeight);
    });

    [Fact]
    public void Default_template_exposes_selected_installment_total() => OnSta(() =>
    {
        var template = ParentNoticeDocumentBuilder.CreateDefaultTemplate();
        var document = ParentNoticeDocumentBuilder.Build(template, new Dictionary<string, string>
        {
            ["{{Tranches.TotalPrevu}}"] = "120 000 CDF"
        });

        var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
        Assert.Contains("120 000 CDF", text);
        Assert.DoesNotContain("{{Tranches.TotalPrevu}}", text);
    });

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
