namespace SchoolManagement.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

[ApiController]
[Authorize]
[Route("api/v1/geography")]
public class GeographyController : ControllerBase
{
    private readonly IGeographyService _geographyService;
    private readonly IAddressService _addressService;

    public GeographyController(IGeographyService geographyService, IAddressService addressService)
    {
        _geographyService = geographyService;
        _addressService = addressService;
    }

    [HttpGet("countries")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetCountries(CancellationToken cancellationToken)
    {
        var result = await _geographyService.GetCountriesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("provinces")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetProvinces([FromQuery] Guid countryId, CancellationToken cancellationToken)
    {
        var result = await _geographyService.GetProvincesAsync(countryId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("cities")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetCities([FromQuery] Guid provinceId, CancellationToken cancellationToken)
    {
        var result = await _geographyService.GetCitiesAsync(provinceId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("communes")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetCommunes([FromQuery] Guid cityId, CancellationToken cancellationToken)
    {
        var result = await _geographyService.GetCommunesAsync(cityId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("addresses/{id:guid}")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<AddressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAddress(Guid id, CancellationToken cancellationToken)
    {
        var address = await _addressService.GetAsync(id, cancellationToken);
        if (address is null)
        {
            return NotFound(ApiResponse<object>.Fail("Adresse introuvable."));
        }

        return Ok(ApiResponse<AddressDto>.Ok(address));
    }
}
