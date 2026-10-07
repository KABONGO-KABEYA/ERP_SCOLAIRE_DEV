using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.DocumentBranding.Interfaces;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.DocumentBranding;

public sealed record ConfiguredDocumentHeader(byte[]? Image, HeaderPrintMode? Mode, decimal Left, decimal Right, decimal? Height)
{
    public void Compose(IContainer container)
    {
        if (Image is null) return;
        var area = container.PaddingLeft((float)Math.Clamp(Left, 0, 40) * 72 / 25.4f)
            .PaddingRight((float)Math.Clamp(Right, 0, 40) * 72 / 25.4f)
            .Height((float)Math.Clamp(Height ?? 20, 8, 60) * 72 / 25.4f);
        if (Mode == HeaderPrintMode.LogoOnly) area.Image(Image).FitArea();
        else area.Image(Image).FitUnproportionally();
    }
}

public sealed class ConfiguredDocumentHeaderService(
    IDocumentPrintBrandingResolver resolver, IDocumentBrandingStorageService storage)
{
    public async Task<ConfiguredDocumentHeader> LoadAsync(Guid schoolId, DocumentBrandingType type, CancellationToken cancellationToken)
    {
        var branding = await resolver.ResolveAsync(schoolId, type, cancellationToken);
        byte[]? bytes = null;
        if (branding.PrintMode is not null && !string.IsNullOrWhiteSpace(branding.HeaderImagePath))
        {
            if (!storage.FileExists(branding.HeaderImagePath))
                throw new InvalidOperationException("L’image de l’en-tête sélectionné est introuvable dans le dossier des documents.");
            bytes = await File.ReadAllBytesAsync(storage.ResolveAbsolutePath(branding.HeaderImagePath), cancellationToken);
        }
        return new(bytes, branding.PrintMode, branding.HeaderMarginLeftMm, branding.HeaderMarginRightMm, branding.HeaderMaxHeightMm);
    }
}
