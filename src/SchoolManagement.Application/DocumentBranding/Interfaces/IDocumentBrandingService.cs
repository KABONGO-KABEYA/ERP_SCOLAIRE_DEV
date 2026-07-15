using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.DocumentBranding.Interfaces;

public interface IDocumentBrandingService
{
    Task<DocumentBrandingConfigurationDto> GetConfigurationAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<DocumentBrandingLookupDto> GetLookupsAsync(CancellationToken cancellationToken = default);

    Task<SchoolLogoDto> CreateLogoAsync(Guid schoolId, SaveSchoolLogoRequest request, string imagePath, CancellationToken cancellationToken = default);

    Task<SchoolLogoDto> UpdateLogoAsync(Guid schoolId, Guid logoId, SaveSchoolLogoRequest request, string? imagePath, CancellationToken cancellationToken = default);

    Task DeleteLogoAsync(Guid schoolId, Guid logoId, CancellationToken cancellationToken = default);

    Task SetPrimaryLogoAsync(Guid schoolId, Guid logoId, CancellationToken cancellationToken = default);

    Task<SchoolDocumentHeaderDto> CreateHeaderAsync(Guid schoolId, SaveSchoolDocumentHeaderRequest request, string? imagePath, CancellationToken cancellationToken = default);

    Task<SchoolDocumentHeaderDto> UpdateHeaderAsync(Guid schoolId, Guid headerId, SaveSchoolDocumentHeaderRequest request, string? imagePath, CancellationToken cancellationToken = default);

    Task DeleteHeaderAsync(Guid schoolId, Guid headerId, CancellationToken cancellationToken = default);

    Task<SchoolSignatureDto> CreateSignatureAsync(Guid schoolId, SaveSchoolSignatureRequest request, string imagePath, CancellationToken cancellationToken = default);

    Task<SchoolSignatureDto> UpdateSignatureAsync(Guid schoolId, Guid signatureId, SaveSchoolSignatureRequest request, string? imagePath, CancellationToken cancellationToken = default);

    Task DeleteSignatureAsync(Guid schoolId, Guid signatureId, CancellationToken cancellationToken = default);

    Task<SchoolStampDto> CreateStampAsync(Guid schoolId, SaveSchoolStampRequest request, string imagePath, CancellationToken cancellationToken = default);

    Task<SchoolStampDto> UpdateStampAsync(Guid schoolId, Guid stampId, SaveSchoolStampRequest request, string? imagePath, CancellationToken cancellationToken = default);

    Task DeleteStampAsync(Guid schoolId, Guid stampId, CancellationToken cancellationToken = default);

    Task<SchoolDocumentFooterDto> SaveFooterAsync(Guid schoolId, SaveSchoolDocumentFooterRequest request, CancellationToken cancellationToken = default);
}

public interface IDocumentPrintBrandingResolver
{
    Task<DocumentPrintBrandingDto> ResolveAsync(
        Guid schoolId,
        DocumentBrandingType documentType,
        CancellationToken cancellationToken = default);
}
