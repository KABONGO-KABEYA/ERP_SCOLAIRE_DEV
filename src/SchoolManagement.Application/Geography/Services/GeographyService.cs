namespace SchoolManagement.Application.Geography.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Domain.Entities.Geography;

public sealed class GeographyService : IGeographyService
{
    private readonly IRepository<Country> _countryRepository;
    private readonly IRepository<Province> _provinceRepository;
    private readonly IRepository<City> _cityRepository;
    private readonly IRepository<Commune> _communeRepository;

    public GeographyService(
        IRepository<Country> countryRepository,
        IRepository<Province> provinceRepository,
        IRepository<City> cityRepository,
        IRepository<Commune> communeRepository)
    {
        _countryRepository = countryRepository;
        _provinceRepository = provinceRepository;
        _cityRepository = cityRepository;
        _communeRepository = communeRepository;
    }

    public async Task<IReadOnlyList<GeographyItemDto>> GetCountriesAsync(CancellationToken cancellationToken = default)
    {
        var items = await _countryRepository.FindAsync(c => c.IsActive, cancellationToken);
        return items
            .OrderBy(c => c.Name)
            .Select(c => new GeographyItemDto(c.Id, c.Code, c.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<GeographyItemDto>> GetProvincesAsync(
        Guid countryId,
        CancellationToken cancellationToken = default)
    {
        var items = await _provinceRepository.FindAsync(p => p.CountryId == countryId && p.IsActive, cancellationToken);
        return items
            .OrderBy(p => p.Name)
            .Select(p => new GeographyItemDto(p.Id, p.Code, p.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<GeographyItemDto>> GetCitiesAsync(
        Guid provinceId,
        CancellationToken cancellationToken = default)
    {
        var items = await _cityRepository.FindAsync(c => c.ProvinceId == provinceId && c.IsActive, cancellationToken);
        return items
            .OrderBy(c => c.Name)
            .Select(c => new GeographyItemDto(c.Id, c.Code, c.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<GeographyItemDto>> GetCommunesAsync(
        Guid cityId,
        CancellationToken cancellationToken = default)
    {
        var items = await _communeRepository.FindAsync(c => c.CityId == cityId && c.IsActive, cancellationToken);
        return items
            .OrderBy(c => c.Name)
            .Select(c => new GeographyItemDto(c.Id, c.Code, c.Name))
            .ToList();
    }
}
