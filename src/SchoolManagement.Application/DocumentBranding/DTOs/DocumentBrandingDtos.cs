using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.DocumentBranding.DTOs;

public sealed record SchoolLogoDto(
    Guid Id,
    string Name,
    string ImagePath,
    bool IsPrimary,
    bool IsActive);

public sealed record SaveSchoolLogoRequest(
    string Name,
    bool IsPrimary,
    bool IsActive);

public sealed record SchoolDocumentHeaderDto(
    Guid Id,
    string Name,
    DocumentBrandingType DocumentType,
    string DocumentTypeLabel,
    IReadOnlyList<DocumentBrandingType> ApplicableDocumentTypes,
    string ApplicableDocumentTypesLabel,
    HeaderPrintMode PrintMode,
    string PrintModeLabel,
    string? ImagePath,
    int? WidthPx,
    int? HeightPx,
    int? ResolutionDpi,
    bool IsActive,
    decimal MarginLeftMm = 0,
    decimal MarginRightMm = 0,
    decimal? MaxHeightMm = null);

public sealed record SaveSchoolDocumentHeaderRequest(
    string Name,
    DocumentBrandingType DocumentType,
    HeaderPrintMode PrintMode,
    int? WidthPx,
    int? HeightPx,
    int? ResolutionDpi,
    bool IsActive,
    string? ApplicableDocumentTypes = null,
    decimal MarginLeftMm = 0,
    decimal MarginRightMm = 0,
    decimal? MaxHeightMm = null);

public sealed record SchoolSignatureDto(
    Guid Id,
    string SignatoryName,
    string Function,
    string ImagePath,
    bool IsActive,
    IReadOnlyList<DocumentBrandingType> ApplicableDocumentTypes,
    string ApplicableDocumentTypesLabel);

public sealed record SaveSchoolSignatureRequest(
    string SignatoryName,
    string Function,
    bool IsActive,
    DocumentBrandingType DocumentType = DocumentBrandingType.Autre,
    string? ApplicableDocumentTypes = null);

public sealed record SchoolStampDto(
    Guid Id,
    string Name,
    string ImagePath,
    bool IsActive);

public sealed record SaveSchoolStampRequest(
    string Name,
    bool IsActive);

public sealed record SchoolDocumentFooterDto(
    Guid? Id,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? PoBox,
    string? SchoolMotto,
    string? FreeText);

public sealed record SaveSchoolDocumentFooterRequest(
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? PoBox,
    string? SchoolMotto,
    string? FreeText);

public sealed record DocumentBrandingConfigurationDto(
    IReadOnlyList<SchoolLogoDto> Logos,
    IReadOnlyList<SchoolDocumentHeaderDto> Headers,
    IReadOnlyList<SchoolSignatureDto> Signatures,
    IReadOnlyList<SchoolStampDto> Stamps,
    SchoolDocumentFooterDto? Footer);

public sealed record DocumentPrintBrandingDto(
    HeaderPrintMode? PrintMode,
    string? HeaderImagePath,
    string? PrimaryLogoPath,
    SchoolDocumentFooterDto? Footer,
    IReadOnlyList<SchoolSignatureDto> Signatures,
    IReadOnlyList<SchoolStampDto> Stamps,
    decimal HeaderMarginLeftMm = 0,
    decimal HeaderMarginRightMm = 0,
    decimal? HeaderMaxHeightMm = null);

public sealed record DocumentBrandingLookupDto(
    IReadOnlyList<DocumentBrandingTypeOptionDto> DocumentTypes,
    IReadOnlyList<HeaderPrintModeOptionDto> PrintModes);

public sealed record DocumentBrandingTypeOptionDto(DocumentBrandingType Value, string Label);

public sealed record HeaderPrintModeOptionDto(HeaderPrintMode Value, string Label);

public sealed record UploadedBrandingImageDto(string ImagePath, string FileName, long FileSizeBytes);
