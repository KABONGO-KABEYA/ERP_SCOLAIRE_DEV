namespace SchoolManagement.Application.Geography.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Domain.Entities.Geography;

public sealed class AddressService : IAddressService
{
    private readonly IRepository<PostalAddress> _addressRepository;
    private readonly IRepository<Country> _countryRepository;
    private readonly IRepository<Province> _provinceRepository;
    private readonly IRepository<City> _cityRepository;
    private readonly IRepository<Commune> _communeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddressService(
        IRepository<PostalAddress> addressRepository,
        IRepository<Country> countryRepository,
        IRepository<Province> provinceRepository,
        IRepository<City> cityRepository,
        IRepository<Commune> communeRepository,
        IUnitOfWork unitOfWork)
    {
        _addressRepository = addressRepository;
        _countryRepository = countryRepository;
        _provinceRepository = provinceRepository;
        _cityRepository = cityRepository;
        _communeRepository = communeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid?> UpsertAsync(
        AddressInputDto? input,
        Guid? existingAddressId,
        CancellationToken cancellationToken = default)
    {
        if (!AddressFormatting.HasContent(input))
        {
            return null;
        }

        PostalAddress? entity = null;
        var isExisting = false;
        if (existingAddressId.HasValue)
        {
            entity = (await _addressRepository.FindAsync(a => a.Id == existingAddressId.Value, cancellationToken))
                .FirstOrDefault();
            isExisting = entity is not null;
        }

        entity ??= new PostalAddress();
        entity.CountryId = input!.CountryId;
        entity.ProvinceId = input.ProvinceId;
        entity.CityId = input.CityId;
        entity.CommuneId = input.CommuneId;
        entity.Neighborhood = string.IsNullOrWhiteSpace(input.Neighborhood) ? null : input.Neighborhood.Trim();
        entity.Avenue = string.IsNullOrWhiteSpace(input.Avenue) ? null : input.Avenue.Trim();
        entity.HouseNumber = string.IsNullOrWhiteSpace(input.HouseNumber) ? null : input.HouseNumber.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        if (isExisting)
        {
            await _addressRepository.UpdateAsync(entity, cancellationToken);
        }
        else
        {
            entity.CreatedAt = DateTime.UtcNow;
            await _addressRepository.AddAsync(entity, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<AddressDto?> GetAsync(Guid addressId, CancellationToken cancellationToken = default)
    {
        var entity = (await _addressRepository.FindAsync(a => a.Id == addressId, cancellationToken)).FirstOrDefault();
        if (entity is null)
        {
            return null;
        }

        var countries = await GetCountryNamesAsync(cancellationToken);
        var provinces = await GetProvinceNamesAsync(cancellationToken);
        var cities = await GetCityNamesAsync(cancellationToken);
        var communes = await GetCommuneNamesAsync(cancellationToken);

        return new AddressDto(
            entity.Id,
            entity.CountryId,
            entity.CountryId.HasValue && countries.TryGetValue(entity.CountryId.Value, out var country) ? country : null,
            entity.ProvinceId,
            entity.ProvinceId.HasValue && provinces.TryGetValue(entity.ProvinceId.Value, out var province) ? province : null,
            entity.CityId,
            entity.CityId.HasValue && cities.TryGetValue(entity.CityId.Value, out var city) ? city : null,
            entity.CommuneId,
            entity.CommuneId.HasValue && communes.TryGetValue(entity.CommuneId.Value, out var commune) ? commune : null,
            entity.Neighborhood,
            entity.Avenue,
            entity.HouseNumber);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetCountryNamesAsync(CancellationToken cancellationToken = default) =>
        (await _countryRepository.FindAsync(_ => true, cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);

    public async Task<IReadOnlyDictionary<Guid, string>> GetProvinceNamesAsync(CancellationToken cancellationToken = default) =>
        (await _provinceRepository.FindAsync(_ => true, cancellationToken))
            .ToDictionary(p => p.Id, p => p.Name);

    public async Task<IReadOnlyDictionary<Guid, string>> GetCityNamesAsync(CancellationToken cancellationToken = default) =>
        (await _cityRepository.FindAsync(_ => true, cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);

    public async Task<IReadOnlyDictionary<Guid, string>> GetCommuneNamesAsync(CancellationToken cancellationToken = default) =>
        (await _communeRepository.FindAsync(_ => true, cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);
}
