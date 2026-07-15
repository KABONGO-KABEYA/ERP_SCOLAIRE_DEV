namespace SchoolManagement.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

[ApiController]
[Authorize]
[Route("api/v1/geography/admin")]
public class GeographyAdminController : ControllerBase
{
    private readonly IGeographyAdminService _geographyAdminService;

    public GeographyAdminController(IGeographyAdminService geographyAdminService)
    {
        _geographyAdminService = geographyAdminService;
    }

    [HttpGet("countries")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> GetCountries([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var result = await _geographyAdminService.GetAllCountriesAsync(includeInactive, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("provinces")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> GetProvinces([FromQuery] Guid countryId, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var result = await _geographyAdminService.GetAllProvincesAsync(countryId, includeInactive, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("cities")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> GetCities([FromQuery] Guid provinceId, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var result = await _geographyAdminService.GetAllCitiesAsync(provinceId, includeInactive, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("communes")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> GetCommunes([FromQuery] Guid cityId, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var result = await _geographyAdminService.GetAllCommunesAsync(cityId, includeInactive, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("countries")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> CreateCountry([FromBody] UpsertGeographyItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _geographyAdminService.SaveCountryAsync(request, cancellationToken: cancellationToken);
        return Ok(ApiResponse<GeographyItemDto>.Ok(result, "Pays enregistré."));
    }

    [HttpPut("countries/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> UpdateCountry(Guid id, [FromBody] UpsertGeographyItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _geographyAdminService.SaveCountryAsync(request, id, cancellationToken);
        return Ok(ApiResponse<GeographyItemDto>.Ok(result, "Pays mis à jour."));
    }

    [HttpDelete("countries/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> DeactivateCountry(Guid id, CancellationToken cancellationToken)
    {
        await _geographyAdminService.DeactivateCountryAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Pays désactivé."));
    }

    [HttpPost("provinces")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> CreateProvince([FromBody] CreateProvinceRequest request, CancellationToken cancellationToken)
    {
        var result = await _geographyAdminService.SaveProvinceAsync(request, cancellationToken: cancellationToken);
        return Ok(ApiResponse<GeographyItemDto>.Ok(result, "Province enregistrée."));
    }

    [HttpPut("provinces/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> UpdateProvince(Guid id, [FromBody] CreateProvinceRequest request, CancellationToken cancellationToken)
    {
        var result = await _geographyAdminService.SaveProvinceAsync(request, id, cancellationToken);
        return Ok(ApiResponse<GeographyItemDto>.Ok(result, "Province mise à jour."));
    }

    [HttpDelete("provinces/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> DeactivateProvince(Guid id, CancellationToken cancellationToken)
    {
        await _geographyAdminService.DeactivateProvinceAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Province désactivée."));
    }

    [HttpPost("cities")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> CreateCity([FromBody] CreateCityRequest request, CancellationToken cancellationToken)
    {
        var result = await _geographyAdminService.SaveCityAsync(request, cancellationToken: cancellationToken);
        return Ok(ApiResponse<GeographyItemDto>.Ok(result, "Ville enregistrée."));
    }

    [HttpPut("cities/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> UpdateCity(Guid id, [FromBody] CreateCityRequest request, CancellationToken cancellationToken)
    {
        var result = await _geographyAdminService.SaveCityAsync(request, id, cancellationToken);
        return Ok(ApiResponse<GeographyItemDto>.Ok(result, "Ville mise à jour."));
    }

    [HttpDelete("cities/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> DeactivateCity(Guid id, CancellationToken cancellationToken)
    {
        await _geographyAdminService.DeactivateCityAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Ville désactivée."));
    }

    [HttpPost("communes")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> CreateCommune([FromBody] CreateCommuneRequest request, CancellationToken cancellationToken)
    {
        var result = await _geographyAdminService.SaveCommuneAsync(request, cancellationToken: cancellationToken);
        return Ok(ApiResponse<GeographyItemDto>.Ok(result, "Commune enregistrée."));
    }

    [HttpPut("communes/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> UpdateCommune(Guid id, [FromBody] CreateCommuneRequest request, CancellationToken cancellationToken)
    {
        var result = await _geographyAdminService.SaveCommuneAsync(request, id, cancellationToken);
        return Ok(ApiResponse<GeographyItemDto>.Ok(result, "Commune mise à jour."));
    }

    [HttpDelete("communes/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    public async Task<IActionResult> DeactivateCommune(Guid id, CancellationToken cancellationToken)
    {
        await _geographyAdminService.DeactivateCommuneAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Commune désactivée."));
    }

    [HttpGet("import/template")]
    [Authorize(Policy = Permissions.AdminFull)]
    public IActionResult DownloadImportTemplate()
    {
        var bytes = _geographyAdminService.BuildImportTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Modele_Geographie.xlsx");
    }

    [HttpPost("import")]
    [Authorize(Policy = Permissions.AdminFull)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> ImportExcel(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Fichier Excel vide."));
        }

        await using var stream = file.OpenReadStream();
        var result = await _geographyAdminService.ImportFromExcelAsync(stream, cancellationToken);
        return Ok(ApiResponse<GeographyImportResultDto>.Ok(result, "Import terminé."));
    }
}
