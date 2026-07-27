using SchoolManagement.Application.DocumentBranding;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.DocumentBranding.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.DocumentBranding.Services;

/// <summary>
/// Composant générique utilisé par tous les futurs états imprimés.
/// </summary>
public sealed class DocumentPrintBrandingResolver : IDocumentPrintBrandingResolver
{
    private readonly IRepository<SchoolLogo> _logoRepository;
    private readonly IRepository<SchoolDocumentHeader> _headerRepository;
    private readonly IRepository<SchoolSignature> _signatureRepository;
    private readonly IRepository<SchoolStamp> _stampRepository;
    private readonly IRepository<SchoolDocumentFooter> _footerRepository;

    public DocumentPrintBrandingResolver(
        IRepository<SchoolLogo> logoRepository,
        IRepository<SchoolDocumentHeader> headerRepository,
        IRepository<SchoolSignature> signatureRepository,
        IRepository<SchoolStamp> stampRepository,
        IRepository<SchoolDocumentFooter> footerRepository)
    {
        _logoRepository = logoRepository;
        _headerRepository = headerRepository;
        _signatureRepository = signatureRepository;
        _stampRepository = stampRepository;
        _footerRepository = footerRepository;
    }

    public async Task<DocumentPrintBrandingDto> ResolveAsync(
        Guid schoolId,
        DocumentBrandingType documentType,
        CancellationToken cancellationToken = default)
    {
        var headers = await _headerRepository.FindAsync(
            x => x.SchoolId == schoolId && x.IsActive,
            cancellationToken);
        var header = headers.FirstOrDefault(x => DocumentBrandingTypeCodec.AppliesTo(x, documentType))
            ?? headers.FirstOrDefault(x => DocumentBrandingTypeCodec.AppliesTo(x, DocumentBrandingType.Autre));

        var logos = await _logoRepository.FindAsync(
            x => x.SchoolId == schoolId && x.IsActive,
            cancellationToken);
        var primaryLogo = logos.FirstOrDefault(x => x.IsPrimary) ?? logos.FirstOrDefault();

        string? headerImagePath = null;
        string? primaryLogoPath = primaryLogo?.ImagePath;
        HeaderPrintMode? printMode = null;
        decimal marginLeftMm = 0;
        decimal marginRightMm = 0;
        decimal? maxHeightMm = null;

        if (header is not null)
        {
            printMode = header.PrintMode;
            marginLeftMm = header.MarginLeftMm;
            marginRightMm = header.MarginRightMm;
            maxHeightMm = header.MaxHeightMm;
            if (header.PrintMode == HeaderPrintMode.FullImage)
            {
                headerImagePath = header.ImagePath;
            }
            else
            {
                headerImagePath = primaryLogoPath;
            }
        }
        else if (primaryLogo is not null)
        {
            printMode = HeaderPrintMode.LogoOnly;
            headerImagePath = primaryLogoPath;
        }

        var signatures = (await _signatureRepository.FindAsync(
                x => x.SchoolId == schoolId && x.IsActive,
                cancellationToken))
            .Where(x => DocumentBrandingTypeCodec.AppliesTo(x, documentType))
            .OrderBy(x => x.Function)
            .Select(x => MapSignatureDto(x))
            .ToList();

        var stamps = (await _stampRepository.FindAsync(
                x => x.SchoolId == schoolId && x.IsActive,
                cancellationToken))
            .OrderBy(x => x.Name)
            .Select(x => new SchoolStampDto(x.Id, x.Name, x.ImagePath, x.IsActive))
            .ToList();

        var footerEntity = (await _footerRepository.FindAsync(x => x.SchoolId == schoolId, cancellationToken)).FirstOrDefault();
        SchoolDocumentFooterDto? footer = footerEntity is null
            ? null
            : new(
                footerEntity.Id,
                footerEntity.Address,
                footerEntity.Phone,
                footerEntity.Email,
                footerEntity.Website,
                footerEntity.PoBox,
                footerEntity.SchoolMotto,
                footerEntity.FreeText);

        return new DocumentPrintBrandingDto(
            printMode,
            headerImagePath,
            primaryLogoPath,
            footer,
            signatures,
            stamps,
            marginLeftMm,
            marginRightMm,
            maxHeightMm);
    }

    private static SchoolSignatureDto MapSignatureDto(SchoolSignature entity)
    {
        var applicableTypes = DocumentBrandingTypeCodec.Deserialize(entity.ApplicableDocumentTypes, entity.DocumentType);
        return new(
            entity.Id,
            entity.SignatoryName,
            entity.Function,
            entity.ImagePath,
            entity.IsActive,
            applicableTypes,
            DocumentBrandingTypeCodec.FormatLabels(applicableTypes));
    }
}
