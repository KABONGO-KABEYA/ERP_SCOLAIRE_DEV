namespace SchoolManagement.Application.Geography.Interfaces;

using SchoolManagement.Application.Geography.DTOs;

public interface IGeographyService
{
    Task<IReadOnlyList<GeographyItemDto>> GetCountriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeographyItemDto>> GetProvincesAsync(
        Guid countryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeographyItemDto>> GetCitiesAsync(
        Guid provinceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeographyItemDto>> GetCommunesAsync(
        Guid cityId,
        CancellationToken cancellationToken = default);
}

public interface IAddressService
{
    Task<Guid?> UpsertAsync(
        AddressInputDto? input,
        Guid? existingAddressId,
        CancellationToken cancellationToken = default);

    Task<AddressDto?> GetAsync(Guid addressId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetCountryNamesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetProvinceNamesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetCityNamesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> GetCommuneNamesAsync(CancellationToken cancellationToken = default);
}
