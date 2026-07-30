using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.DocumentBranding.Interfaces;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.DocumentBranding}")]
public sealed class DocumentBrandingController : ControllerBase
{
    private readonly IDocumentBrandingService _brandingService;
    private readonly IDocumentPrintBrandingResolver _printResolver;
    private readonly IDocumentBrandingStorageService _storage;
    private readonly ICurrentUserService _currentUser;

    public DocumentBrandingController(
        IDocumentBrandingService brandingService,
        IDocumentPrintBrandingResolver printResolver,
        IDocumentBrandingStorageService storage,
        ICurrentUserService currentUser)
    {
        _brandingService = brandingService;
        _printResolver = printResolver;
        _storage = storage;
        _currentUser = currentUser;
    }

    [HttpGet("configuration")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetConfiguration(CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var configuration = await _brandingService.GetConfigurationAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<DocumentBrandingConfigurationDto>.Ok(configuration));
    }

    [HttpGet("lookups")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetLookups(CancellationToken cancellationToken)
    {
        var lookups = await _brandingService.GetLookupsAsync(cancellationToken);
        return Ok(ApiResponse<DocumentBrandingLookupDto>.Ok(lookups));
    }

    [HttpGet("print/{documentType}")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> ResolvePrintBranding(DocumentBrandingType documentType, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var branding = await _printResolver.ResolveAsync(schoolId, documentType, cancellationToken);
        return Ok(ApiResponse<DocumentPrintBrandingDto>.Ok(branding));
    }

    [HttpPost("logos")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CreateLogo(
        [FromForm] SaveSchoolLogoRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var imagePath = await SaveUploadedImageAsync(schoolId, image, _storage.SaveLogoAsync, cancellationToken);
        var logo = await _brandingService.CreateLogoAsync(schoolId, request, imagePath, cancellationToken);
        return Ok(ApiResponse<SchoolLogoDto>.Ok(logo, "Logo enregistré."));
    }

    [HttpPut("logos/{logoId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> UpdateLogo(
        Guid logoId,
        [FromForm] SaveSchoolLogoRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        string? imagePath = null;
        if (image is not null)
        {
            imagePath = await SaveUploadedImageAsync(schoolId, image, _storage.SaveLogoAsync, cancellationToken);
        }

        var logo = await _brandingService.UpdateLogoAsync(schoolId, logoId, request, imagePath, cancellationToken);
        return Ok(ApiResponse<SchoolLogoDto>.Ok(logo, "Logo mis à jour."));
    }

    [HttpDelete("logos/{logoId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> DeleteLogo(Guid logoId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _brandingService.DeleteLogoAsync(schoolId, logoId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null!, "Logo supprimé."));
    }

    [HttpPost("logos/{logoId:guid}/set-primary")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> SetPrimaryLogo(Guid logoId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _brandingService.SetPrimaryLogoAsync(schoolId, logoId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null!, "Logo principal défini."));
    }

    [HttpGet("logos/primary/file")]
    [Authorize]
    public async Task<IActionResult> GetPrimaryLogoFile(CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var configuration = await _brandingService.GetConfigurationAsync(schoolId, cancellationToken);
        var logo = configuration.Logos.FirstOrDefault(l => l.IsPrimary && l.IsActive)
            ?? configuration.Logos.FirstOrDefault(l => l.IsActive);
        if (logo is null || string.IsNullOrWhiteSpace(logo.ImagePath) || !_storage.FileExists(logo.ImagePath))
        {
            return NotFound();
        }

        var absolutePath = _storage.ResolveAbsolutePath(logo.ImagePath);
        var contentType = Path.GetExtension(absolutePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };
        var bytes = await System.IO.File.ReadAllBytesAsync(absolutePath, cancellationToken);
        return File(bytes, contentType);
    }

    [HttpPost("headers")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CreateHeader(
        [FromForm] SaveSchoolDocumentHeaderRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        string? imagePath = null;
        if (image is not null)
        {
            imagePath = await SaveUploadedImageAsync(schoolId, image, _storage.SaveHeaderAsync, cancellationToken);
        }

        var header = await _brandingService.CreateHeaderAsync(schoolId, request, imagePath, cancellationToken);
        return Ok(ApiResponse<SchoolDocumentHeaderDto>.Ok(header, "En-tête enregistré."));
    }

    [HttpPut("headers/{headerId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> UpdateHeader(
        Guid headerId,
        [FromForm] SaveSchoolDocumentHeaderRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        string? imagePath = null;
        if (image is not null)
        {
            imagePath = await SaveUploadedImageAsync(schoolId, image, _storage.SaveHeaderAsync, cancellationToken);
        }

        var header = await _brandingService.UpdateHeaderAsync(schoolId, headerId, request, imagePath, cancellationToken);
        return Ok(ApiResponse<SchoolDocumentHeaderDto>.Ok(header, "En-tête mis à jour."));
    }

    [HttpDelete("headers/{headerId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> DeleteHeader(Guid headerId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _brandingService.DeleteHeaderAsync(schoolId, headerId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null!, "En-tête supprimé."));
    }

    [HttpPost("signatures")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CreateSignature(
        [FromForm] SaveSchoolSignatureRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var imagePath = await SaveUploadedImageAsync(schoolId, image, _storage.SaveSignatureAsync, cancellationToken);
        var signature = await _brandingService.CreateSignatureAsync(schoolId, request, imagePath, cancellationToken);
        return Ok(ApiResponse<SchoolSignatureDto>.Ok(signature, "Signature enregistrée."));
    }

    [HttpPut("signatures/{signatureId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> UpdateSignature(
        Guid signatureId,
        [FromForm] SaveSchoolSignatureRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        string? imagePath = null;
        if (image is not null)
        {
            imagePath = await SaveUploadedImageAsync(schoolId, image, _storage.SaveSignatureAsync, cancellationToken);
        }

        var signature = await _brandingService.UpdateSignatureAsync(schoolId, signatureId, request, imagePath, cancellationToken);
        return Ok(ApiResponse<SchoolSignatureDto>.Ok(signature, "Signature mise à jour."));
    }

    [HttpDelete("signatures/{signatureId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> DeleteSignature(Guid signatureId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _brandingService.DeleteSignatureAsync(schoolId, signatureId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null!, "Signature supprimée."));
    }

    [HttpPost("stamps")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CreateStamp(
        [FromForm] SaveSchoolStampRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var imagePath = await SaveUploadedImageAsync(schoolId, image, _storage.SaveStampAsync, cancellationToken);
        var stamp = await _brandingService.CreateStampAsync(schoolId, request, imagePath, cancellationToken);
        return Ok(ApiResponse<SchoolStampDto>.Ok(stamp, "Cachet enregistré."));
    }

    [HttpPut("stamps/{stampId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> UpdateStamp(
        Guid stampId,
        [FromForm] SaveSchoolStampRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        string? imagePath = null;
        if (image is not null)
        {
            imagePath = await SaveUploadedImageAsync(schoolId, image, _storage.SaveStampAsync, cancellationToken);
        }

        var stamp = await _brandingService.UpdateStampAsync(schoolId, stampId, request, imagePath, cancellationToken);
        return Ok(ApiResponse<SchoolStampDto>.Ok(stamp, "Cachet mis à jour."));
    }

    [HttpDelete("stamps/{stampId:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> DeleteStamp(Guid stampId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _brandingService.DeleteStampAsync(schoolId, stampId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null!, "Cachet supprimé."));
    }

    [HttpPut("footer")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> SaveFooter(
        [FromBody] SaveSchoolDocumentFooterRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var footer = await _brandingService.SaveFooterAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<SchoolDocumentFooterDto>.Ok(footer, "Pied de page enregistré."));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();

    private static async Task<string> SaveUploadedImageAsync(
        Guid schoolId,
        IFormFile? image,
        Func<Guid, string, Stream, CancellationToken, Task<string>> saveAsync,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            throw new ArgumentException("Le fichier image est obligatoire.");
        }

        await using var stream = image.OpenReadStream();
        return await saveAsync(schoolId, image.FileName, stream, cancellationToken);
    }
}
