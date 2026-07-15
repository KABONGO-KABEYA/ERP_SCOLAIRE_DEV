namespace SchoolManagement.Application.Geography.Interfaces;

using SchoolManagement.Application.Geography.DTOs;

public interface IGeographyAdminService
{
    Task<IReadOnlyList<GeographyItemDto>> GetAllCountriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeographyItemDto>> GetAllProvincesAsync(
        Guid countryId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeographyItemDto>> GetAllCitiesAsync(
        Guid provinceId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeographyItemDto>> GetAllCommunesAsync(
        Guid cityId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<GeographyItemDto> SaveCountryAsync(
        UpsertGeographyItemRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default);

    Task<GeographyItemDto> SaveProvinceAsync(
        CreateProvinceRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default);

    Task<GeographyItemDto> SaveCityAsync(
        CreateCityRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default);

    Task<GeographyItemDto> SaveCommuneAsync(
        CreateCommuneRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default);

    Task DeactivateCountryAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeactivateProvinceAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeactivateCityAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeactivateCommuneAsync(Guid id, CancellationToken cancellationToken = default);

    byte[] BuildImportTemplate();

    Task<GeographyImportResultDto> ImportFromExcelAsync(
        Stream content,
        CancellationToken cancellationToken = default);
}
