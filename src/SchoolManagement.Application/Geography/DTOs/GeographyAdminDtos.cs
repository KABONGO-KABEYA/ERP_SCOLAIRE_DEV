namespace SchoolManagement.Application.Geography.DTOs;

public enum GeographyAdminLevel
{
    Country = 1,
    Province = 2,
    City = 3,
    Commune = 4
}

public sealed record UpsertGeographyItemRequest(
    string Code,
    string Name,
    bool IsActive = true);

public sealed record CreateProvinceRequest(
    Guid CountryId,
    string Code,
    string Name,
    bool IsActive = true);

public sealed record CreateCityRequest(
    Guid ProvinceId,
    string Code,
    string Name,
    bool IsActive = true);

public sealed record CreateCommuneRequest(
    Guid CityId,
    string Code,
    string Name,
    bool IsActive = true);

public sealed record GeographyImportRowError(
    int RowNumber,
    string Message);

public sealed record GeographyImportResultDto(
    int CountriesCreated,
    int CountriesUpdated,
    int ProvincesCreated,
    int ProvincesUpdated,
    int CitiesCreated,
    int CitiesUpdated,
    int CommunesCreated,
    int CommunesUpdated,
    IReadOnlyList<GeographyImportRowError> Errors)
{
    public int TotalProcessed =>
        CountriesCreated + CountriesUpdated
        + ProvincesCreated + ProvincesUpdated
        + CitiesCreated + CitiesUpdated
        + CommunesCreated + CommunesUpdated;
}
