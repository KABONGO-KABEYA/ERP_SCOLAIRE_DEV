using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.DocumentBranding.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.DocumentBranding.Services;

public sealed class DocumentBrandingService : IDocumentBrandingService
{
    private readonly IRepository<SchoolLogo> _logoRepository;
    private readonly IRepository<SchoolDocumentHeader> _headerRepository;
    private readonly IRepository<SchoolSignature> _signatureRepository;
    private readonly IRepository<SchoolStamp> _stampRepository;
    private readonly IRepository<SchoolDocumentFooter> _footerRepository;
    private readonly IDocumentBrandingStorageService _storage;
    private readonly IUnitOfWork _unitOfWork;

    public DocumentBrandingService(
        IRepository<SchoolLogo> logoRepository,
        IRepository<SchoolDocumentHeader> headerRepository,
        IRepository<SchoolSignature> signatureRepository,
        IRepository<SchoolStamp> stampRepository,
        IRepository<SchoolDocumentFooter> footerRepository,
        IDocumentBrandingStorageService storage,
        IUnitOfWork unitOfWork)
    {
        _logoRepository = logoRepository;
        _headerRepository = headerRepository;
        _signatureRepository = signatureRepository;
        _stampRepository = stampRepository;
        _footerRepository = footerRepository;
        _storage = storage;
        _unitOfWork = unitOfWork;
    }

    public async Task<DocumentBrandingConfigurationDto> GetConfigurationAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var logos = await _logoRepository.FindAsync(x => x.SchoolId == schoolId, cancellationToken);
        var headers = await _headerRepository.FindAsync(x => x.SchoolId == schoolId, cancellationToken);
        var signatures = await _signatureRepository.FindAsync(x => x.SchoolId == schoolId, cancellationToken);
        var stamps = await _stampRepository.FindAsync(x => x.SchoolId == schoolId, cancellationToken);
        var footer = (await _footerRepository.FindAsync(x => x.SchoolId == schoolId, cancellationToken)).FirstOrDefault();

        return new DocumentBrandingConfigurationDto(
            logos.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Name).Select(MapLogo).ToList(),
            headers.OrderBy(x => x.DocumentType).ThenBy(x => x.Name).Select(MapHeader).ToList(),
            signatures.OrderBy(x => x.Function).ThenBy(x => x.SignatoryName).Select(MapSignature).ToList(),
            stamps.OrderBy(x => x.Name).Select(MapStamp).ToList(),
            footer is null ? null : MapFooter(footer));
    }

    public Task<DocumentBrandingLookupDto> GetLookupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DocumentBrandingLookupDto(
            DocumentBrandingLabels.AllDocumentTypes
                .Select(type => new DocumentBrandingTypeOptionDto(type, DocumentBrandingLabels.GetDocumentTypeLabel(type)))
                .ToList(),
            Enum.GetValues<HeaderPrintMode>()
                .Select(mode => new HeaderPrintModeOptionDto(mode, DocumentBrandingLabels.GetPrintModeLabel(mode)))
                .ToList()));

    public async Task<SchoolLogoDto> CreateLogoAsync(
        Guid schoolId,
        SaveSchoolLogoRequest request,
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        ValidateLogoRequest(request, imagePath);

        if (request.IsPrimary)
        {
            await ClearPrimaryLogoAsync(schoolId, cancellationToken);
        }

        var entity = new SchoolLogo
        {
            SchoolId = schoolId,
            Name = request.Name.Trim(),
            ImagePath = imagePath,
            IsPrimary = request.IsPrimary,
            IsActive = request.IsActive
        };

        await _logoRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapLogo(entity);
    }

    public async Task<SchoolLogoDto> UpdateLogoAsync(
        Guid schoolId,
        Guid logoId,
        SaveSchoolLogoRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetLogoAsync(schoolId, logoId, cancellationToken);
        ValidateLogoRequest(request, imagePath ?? entity.ImagePath);

        if (request.IsPrimary && !entity.IsPrimary)
        {
            await ClearPrimaryLogoAsync(schoolId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(imagePath) && !string.Equals(imagePath, entity.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            await _storage.DeleteAsync(entity.ImagePath, cancellationToken);
            entity.ImagePath = imagePath;
        }

        entity.Name = request.Name.Trim();
        entity.IsPrimary = request.IsPrimary;
        entity.IsActive = request.IsActive;
        await _logoRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapLogo(entity);
    }

    public async Task DeleteLogoAsync(Guid schoolId, Guid logoId, CancellationToken cancellationToken = default)
    {
        var entity = await GetLogoAsync(schoolId, logoId, cancellationToken);
        await _storage.DeleteAsync(entity.ImagePath, cancellationToken);
        await _logoRepository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPrimaryLogoAsync(Guid schoolId, Guid logoId, CancellationToken cancellationToken = default)
    {
        var entity = await GetLogoAsync(schoolId, logoId, cancellationToken);
        await ClearPrimaryLogoAsync(schoolId, cancellationToken);
        entity.IsPrimary = true;
        entity.IsActive = true;
        await _logoRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SchoolDocumentHeaderDto> CreateHeaderAsync(
        Guid schoolId,
        SaveSchoolDocumentHeaderRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default)
    {
        ValidateHeaderRequest(request, imagePath);

        var applicableTypes = DocumentBrandingTypeCodec.Deserialize(request.ApplicableDocumentTypes, request.DocumentType);
        var primaryType = applicableTypes[0];

        var entity = new SchoolDocumentHeader
        {
            SchoolId = schoolId,
            Name = request.Name.Trim(),
            DocumentType = primaryType,
            ApplicableDocumentTypes = DocumentBrandingTypeCodec.Serialize(applicableTypes),
            PrintMode = request.PrintMode,
            ImagePath = imagePath,
            WidthPx = request.WidthPx,
            HeightPx = request.HeightPx,
            ResolutionDpi = request.ResolutionDpi,
            MarginLeftMm = NormalizeMargin(request.MarginLeftMm),
            MarginRightMm = NormalizeMargin(request.MarginRightMm),
            MaxHeightMm = NormalizeMaxHeight(request.MaxHeightMm),
            IsActive = request.IsActive
        };

        await _headerRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapHeader(entity);
    }

    public async Task<SchoolDocumentHeaderDto> UpdateHeaderAsync(
        Guid schoolId,
        Guid headerId,
        SaveSchoolDocumentHeaderRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetHeaderAsync(schoolId, headerId, cancellationToken);
        ValidateHeaderRequest(request, imagePath ?? entity.ImagePath);

        if (!string.IsNullOrWhiteSpace(imagePath)
            && !string.Equals(imagePath, entity.ImagePath, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(entity.ImagePath))
        {
            await _storage.DeleteAsync(entity.ImagePath, cancellationToken);
        }

        entity.Name = request.Name.Trim();
        var applicableTypes = DocumentBrandingTypeCodec.Deserialize(request.ApplicableDocumentTypes, request.DocumentType);
        entity.DocumentType = applicableTypes[0];
        entity.ApplicableDocumentTypes = DocumentBrandingTypeCodec.Serialize(applicableTypes);
        entity.PrintMode = request.PrintMode;
        entity.ImagePath = imagePath ?? entity.ImagePath;
        entity.WidthPx = request.WidthPx;
        entity.HeightPx = request.HeightPx;
        entity.ResolutionDpi = request.ResolutionDpi;
        entity.MarginLeftMm = NormalizeMargin(request.MarginLeftMm);
        entity.MarginRightMm = NormalizeMargin(request.MarginRightMm);
        entity.MaxHeightMm = NormalizeMaxHeight(request.MaxHeightMm);
        entity.IsActive = request.IsActive;
        await _headerRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapHeader(entity);
    }

    public async Task DeleteHeaderAsync(Guid schoolId, Guid headerId, CancellationToken cancellationToken = default)
    {
        var entity = await GetHeaderAsync(schoolId, headerId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(entity.ImagePath))
        {
            await _storage.DeleteAsync(entity.ImagePath, cancellationToken);
        }

        await _headerRepository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SchoolSignatureDto> CreateSignatureAsync(
        Guid schoolId,
        SaveSchoolSignatureRequest request,
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        ValidateSignatureRequest(request, imagePath);

        var applicableTypes = DocumentBrandingTypeCodec.Deserialize(request.ApplicableDocumentTypes, request.DocumentType);

        var entity = new SchoolSignature
        {
            SchoolId = schoolId,
            SignatoryName = request.SignatoryName.Trim(),
            Function = request.Function.Trim(),
            DocumentType = applicableTypes[0],
            ApplicableDocumentTypes = DocumentBrandingTypeCodec.Serialize(applicableTypes),
            ImagePath = imagePath,
            IsActive = request.IsActive
        };

        await _signatureRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapSignature(entity);
    }

    public async Task<SchoolSignatureDto> UpdateSignatureAsync(
        Guid schoolId,
        Guid signatureId,
        SaveSchoolSignatureRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetSignatureAsync(schoolId, signatureId, cancellationToken);
        ValidateSignatureRequest(request, imagePath ?? entity.ImagePath);

        if (!string.IsNullOrWhiteSpace(imagePath) && !string.Equals(imagePath, entity.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            await _storage.DeleteAsync(entity.ImagePath, cancellationToken);
            entity.ImagePath = imagePath;
        }

        entity.SignatoryName = request.SignatoryName.Trim();
        entity.Function = request.Function.Trim();
        var applicableTypes = DocumentBrandingTypeCodec.Deserialize(request.ApplicableDocumentTypes, request.DocumentType);
        entity.DocumentType = applicableTypes[0];
        entity.ApplicableDocumentTypes = DocumentBrandingTypeCodec.Serialize(applicableTypes);
        entity.IsActive = request.IsActive;
        await _signatureRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapSignature(entity);
    }

    public async Task DeleteSignatureAsync(Guid schoolId, Guid signatureId, CancellationToken cancellationToken = default)
    {
        var entity = await GetSignatureAsync(schoolId, signatureId, cancellationToken);
        await _storage.DeleteAsync(entity.ImagePath, cancellationToken);
        await _signatureRepository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SchoolStampDto> CreateStampAsync(
        Guid schoolId,
        SaveSchoolStampRequest request,
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        ValidateStampRequest(request, imagePath);

        var entity = new SchoolStamp
        {
            SchoolId = schoolId,
            Name = request.Name.Trim(),
            ImagePath = imagePath,
            IsActive = request.IsActive
        };

        await _stampRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapStamp(entity);
    }

    public async Task<SchoolStampDto> UpdateStampAsync(
        Guid schoolId,
        Guid stampId,
        SaveSchoolStampRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetStampAsync(schoolId, stampId, cancellationToken);
        ValidateStampRequest(request, imagePath ?? entity.ImagePath);

        if (!string.IsNullOrWhiteSpace(imagePath) && !string.Equals(imagePath, entity.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            await _storage.DeleteAsync(entity.ImagePath, cancellationToken);
            entity.ImagePath = imagePath;
        }

        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
        await _stampRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapStamp(entity);
    }

    public async Task DeleteStampAsync(Guid schoolId, Guid stampId, CancellationToken cancellationToken = default)
    {
        var entity = await GetStampAsync(schoolId, stampId, cancellationToken);
        await _storage.DeleteAsync(entity.ImagePath, cancellationToken);
        await _stampRepository.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SchoolDocumentFooterDto> SaveFooterAsync(
        Guid schoolId,
        SaveSchoolDocumentFooterRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = (await _footerRepository.FindAsync(x => x.SchoolId == schoolId, cancellationToken)).FirstOrDefault();
        if (entity is null)
        {
            entity = new SchoolDocumentFooter { SchoolId = schoolId };
            ApplyFooter(entity, request);
            await _footerRepository.AddAsync(entity, cancellationToken);
        }
        else
        {
            ApplyFooter(entity, request);
            await _footerRepository.UpdateAsync(entity, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapFooter(entity);
    }

    private async Task ClearPrimaryLogoAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var logos = await _logoRepository.FindAsync(x => x.SchoolId == schoolId && x.IsPrimary, cancellationToken);
        foreach (var logo in logos)
        {
            logo.IsPrimary = false;
            await _logoRepository.UpdateAsync(logo, cancellationToken);
        }
    }

    private async Task<SchoolLogo> GetLogoAsync(Guid schoolId, Guid logoId, CancellationToken cancellationToken) =>
        (await _logoRepository.FindAsync(x => x.Id == logoId && x.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Logo introuvable.");

    private async Task<SchoolDocumentHeader> GetHeaderAsync(Guid schoolId, Guid headerId, CancellationToken cancellationToken) =>
        (await _headerRepository.FindAsync(x => x.Id == headerId && x.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("En-tête introuvable.");

    private async Task<SchoolSignature> GetSignatureAsync(Guid schoolId, Guid signatureId, CancellationToken cancellationToken) =>
        (await _signatureRepository.FindAsync(x => x.Id == signatureId && x.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Signature introuvable.");

    private async Task<SchoolStamp> GetStampAsync(Guid schoolId, Guid stampId, CancellationToken cancellationToken) =>
        (await _stampRepository.FindAsync(x => x.Id == stampId && x.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Cachet introuvable.");

    private static void ValidateLogoRequest(SaveSchoolLogoRequest request, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Le nom du logo est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new DomainException("L'image du logo est obligatoire.");
        }
    }

    private static void ValidateHeaderRequest(SaveSchoolDocumentHeaderRequest request, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Le nom de l'en-tête est obligatoire.");
        }

        if (request.PrintMode == HeaderPrintMode.FullImage && string.IsNullOrWhiteSpace(imagePath))
        {
            throw new DomainException("L'image complète est obligatoire pour ce mode.");
        }

        if (request.MarginLeftMm < 0 || request.MarginLeftMm > 40
            || request.MarginRightMm < 0 || request.MarginRightMm > 40)
        {
            throw new DomainException("Les marges de l'en-tête doivent être entre 0 et 40 mm.");
        }

        if (request.MaxHeightMm is < 8 or > 60)
        {
            throw new DomainException("La hauteur max de l'en-tête doit être entre 8 et 60 mm.");
        }
    }

    private static decimal NormalizeMargin(decimal value) =>
        Math.Clamp(value, 0m, 40m);

    private static decimal? NormalizeMaxHeight(decimal? value) =>
        value is null ? null : Math.Clamp(value.Value, 8m, 60m);

    private static void ValidateSignatureRequest(SaveSchoolSignatureRequest request, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(request.SignatoryName) || string.IsNullOrWhiteSpace(request.Function))
        {
            throw new DomainException("Le nom et la fonction du signataire sont obligatoires.");
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new DomainException("L'image de la signature est obligatoire.");
        }
    }

    private static void ValidateStampRequest(SaveSchoolStampRequest request, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Le nom du cachet est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new DomainException("L'image du cachet est obligatoire.");
        }
    }

    private static void ApplyFooter(SchoolDocumentFooter entity, SaveSchoolDocumentFooterRequest request)
    {
        entity.Address = NullIfWhiteSpace(request.Address);
        entity.Phone = NullIfWhiteSpace(request.Phone);
        entity.Email = NullIfWhiteSpace(request.Email);
        entity.Website = NullIfWhiteSpace(request.Website);
        entity.PoBox = NullIfWhiteSpace(request.PoBox);
        entity.SchoolMotto = NullIfWhiteSpace(request.SchoolMotto);
        entity.FreeText = NullIfWhiteSpace(request.FreeText);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SchoolLogoDto MapLogo(SchoolLogo entity) =>
        new(entity.Id, entity.Name, entity.ImagePath, entity.IsPrimary, entity.IsActive);

    private static SchoolDocumentHeaderDto MapHeader(SchoolDocumentHeader entity)
    {
        var applicableTypes = DocumentBrandingTypeCodec.Deserialize(entity.ApplicableDocumentTypes, entity.DocumentType);
        return new(
            entity.Id,
            entity.Name,
            entity.DocumentType,
            DocumentBrandingLabels.GetDocumentTypeLabel(entity.DocumentType),
            applicableTypes,
            DocumentBrandingTypeCodec.FormatLabels(applicableTypes),
            entity.PrintMode,
            DocumentBrandingLabels.GetPrintModeLabel(entity.PrintMode),
            entity.ImagePath,
            entity.WidthPx,
            entity.HeightPx,
            entity.ResolutionDpi,
            entity.IsActive,
            entity.MarginLeftMm,
            entity.MarginRightMm,
            entity.MaxHeightMm);
    }

    private static SchoolSignatureDto MapSignature(SchoolSignature entity)
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

    private static SchoolStampDto MapStamp(SchoolStamp entity) =>
        new(entity.Id, entity.Name, entity.ImagePath, entity.IsActive);

    private static SchoolDocumentFooterDto MapFooter(SchoolDocumentFooter entity) =>
        new(
            entity.Id,
            entity.Address,
            entity.Phone,
            entity.Email,
            entity.Website,
            entity.PoBox,
            entity.SchoolMotto,
            entity.FreeText);
}
