using System.Collections.Specialized;
using NSubstitute;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.Desktop.UnitTests;

public sealed class DocumentBrandingReloadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_keeps_header_selection_image_mode_and_document_choices(bool create)
    {
        var api = Substitute.For<IDocumentBrandingApiService>();
        var resolver = Substitute.For<IDocumentBrandingPathResolver>();
        resolver.ResolveAbsolutePath("Entetes/saved.png").Returns("C:/images/saved.png");
        var header = new SchoolDocumentHeaderDto(Guid.NewGuid(), "École", DocumentBrandingType.FicheInscription,
            "Inscription", new[] { DocumentBrandingType.FicheInscription, DocumentBrandingType.RapportFinancier },
            "Inscription, Finance", HeaderPrintMode.FullImage, "Image complète", "Entetes/saved.png",
            null, null, null, true, 8, 8, 50);
        api.GetLookupsAsync().Returns(new DocumentBrandingLookupDto(
            new[] { new DocumentBrandingTypeOptionDto(DocumentBrandingType.FicheInscription, "Inscription"),
                new DocumentBrandingTypeOptionDto(DocumentBrandingType.RapportFinancier, "Finance") },
            new[] { new HeaderPrintModeOptionDto(HeaderPrintMode.FullImage, "Image complète") }));
        api.GetConfigurationAsync().Returns(new DocumentBrandingConfigurationDto([], [header], [], [], null));
        api.CreateHeaderAsync(Arg.Any<SaveSchoolDocumentHeaderRequest>(), Arg.Any<string?>()).Returns(header);
        api.UpdateHeaderAsync(header.Id, Arg.Any<SaveSchoolDocumentHeaderRequest>(), Arg.Any<string?>()).Returns(header);
        var vm = new DocumentBrandingViewModel(api, resolver);
        await vm.LoadCommand.ExecuteAsync(null);
        if (create)
        {
            vm.NewHeaderCommand.Execute(null);
            vm.HeaderName = header.Name;
            vm.HeaderPendingImagePath = "C:/images/source.png";
            foreach (var option in vm.HeaderDocumentTypeOptions) option.IsSelected = true;
        }
        else vm.SelectedHeader = vm.Headers.Single();
        var resets = 0;
        vm.PrintModes.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) resets++; };
        await vm.SaveHeaderCommand.ExecuteAsync(null);
        Assert.Equal(0, resets);
        Assert.Same(vm.Headers.Single(), vm.SelectedHeader);
        Assert.Equal(header.Id, vm.SelectedHeader!.Id);
        Assert.Equal(HeaderPrintMode.FullImage, vm.HeaderPrintMode);
        Assert.Equal("C:/images/saved.png", vm.HeaderPreviewPath);
        Assert.All(vm.HeaderDocumentTypeOptions, option => Assert.True(option.IsSelected));
        Assert.Null(vm.HeaderPendingImagePath);
        Assert.Null(vm.ValidationMessage);
        Assert.Equal("En-tête enregistré.", vm.StatusMessage);
        await vm.SaveHeaderCommand.ExecuteAsync(null);
        await api.Received(create ? 1 : 2).UpdateHeaderAsync(header.Id, Arg.Any<SaveSchoolDocumentHeaderRequest>(), Arg.Any<string?>());
    }
}
