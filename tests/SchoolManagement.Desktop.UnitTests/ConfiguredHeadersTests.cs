using System.Linq.Expressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NSubstitute;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.DocumentBranding;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.DocumentBranding.Services;
using SchoolManagement.Desktop.Printing;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.Desktop.UnitTests;

public sealed class ConfiguredHeadersTests
{
    [Fact]
    public void Legacy_finance_expands_once_and_unchecked_type_stays_unchecked()
    {
        var migrated = DocumentBrandingTypeCodec.Deserialize("10,11", DocumentBrandingType.FicheInscription);
        Assert.Contains(DocumentBrandingType.FicheInscription, migrated);
        Assert.Contains(DocumentBrandingType.SituationPaiements, migrated);
        Assert.Contains(DocumentBrandingType.RecettesRealisees, migrated);
        var saved = DocumentBrandingTypeCodec.Serialize(migrated.Where(x => x != DocumentBrandingType.SituationPaiements));
        var reloaded = DocumentBrandingTypeCodec.Deserialize(saved, DocumentBrandingType.FicheInscription);
        Assert.DoesNotContain(DocumentBrandingType.SituationPaiements, reloaded);
        Assert.Contains(DocumentBrandingType.RecettesRealisees, reloaded);
    }

    [Theory]
    [InlineData(DocumentBrandingType.SituationPaiements, false)]
    [InlineData(DocumentBrandingType.FicheInscription, false)]
    [InlineData(DocumentBrandingType.Recu, true)]
    public async Task Only_receipts_keep_legacy_other_header_fallback(DocumentBrandingType type, bool expected)
    {
        var headers = Substitute.For<IRepository<SchoolDocumentHeader>>();
        headers.FindAsync(Arg.Any<Expression<Func<SchoolDocumentHeader, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new SchoolDocumentHeader { IsActive = true, DocumentType = DocumentBrandingType.Autre, PrintMode = HeaderPrintMode.FullImage, ImagePath = "other.png" } });
        var logos = Substitute.For<IRepository<SchoolLogo>>();
        logos.FindAsync(Arg.Any<Expression<Func<SchoolLogo, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new SchoolLogo { IsActive = true, IsPrimary = true, ImagePath = "logo.png" } });
        var resolver = new DocumentPrintBrandingResolver(logos, headers,
            Substitute.For<IRepository<SchoolSignature>>(), Substitute.For<IRepository<SchoolStamp>>(), Substitute.For<IRepository<SchoolDocumentFooter>>());
        var result = await resolver.ResolveAsync(Guid.NewGuid(), type);
        Assert.Equal(expected, result.HeaderImagePath is not null);
        Assert.Equal(expected, result.PrimaryLogoPath is not null);
    }

    [Fact]
    public void Selected_header_precedes_title_and_exports_all_pages() => OnSta(() =>
    {
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            var pixels = Enumerable.Repeat((byte)128, 40 * 10 * 4).ToArray();
            var bitmap = BitmapSource.Create(40, 10, 96, 96, PixelFormats.Bgra32, null, pixels, 160);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var output = System.IO.File.Create(file)) encoder.Save(output);
            var resolver = Substitute.For<IDocumentBrandingPathResolver>(); resolver.ResolveAbsolutePath("header.png").Returns(file);
            var doc = DocumentPreview.Table("Titre du document", ["Nom"], Enumerable.Range(0, 160).Select(i => new[] { "Élève " + i }));
            var originalFirst = doc.Blocks.FirstBlock;
            Assert.Same(doc, ConfiguredDocumentPreview.Apply(doc, null, [], resolver));
            Assert.Same(originalFirst, doc.Blocks.FirstBlock);
            var header = new SchoolDocumentHeaderDto(Guid.NewGuid(), "En-tête", DocumentBrandingType.ListeEleves, "Liste", [DocumentBrandingType.ListeEleves], "Liste", HeaderPrintMode.FullImage, "Image", "header.png", null, null, null, true, 8, 8, 20);
            ConfiguredDocumentPreview.Apply(doc, header, [], resolver);
            Assert.IsType<BlockUIContainer>(doc.Blocks.FirstBlock);
            Assert.Same(originalFirst, doc.Blocks.FirstBlock!.NextBlock);
            var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
            paginator.ComputePageCount(); Assert.True(paginator.PageCount > 1);
            var pdf = DocumentPreview.ToPdf(doc);
            Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 8));
        }
        finally { System.IO.File.Delete(file); }
    });

    private static void OnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
