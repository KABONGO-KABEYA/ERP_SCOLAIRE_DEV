namespace SchoolManagement.Application.Geography.Services;

using ClosedXML.Excel;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Exceptions;

public sealed class GeographyAdminService : IGeographyAdminService
{
    private static readonly string[] TemplateHeaders =
    [
        "Pays_Code",
        "Pays_Nom",
        "Province_Code",
        "Province_Nom",
        "Ville_Code",
        "Ville_Nom",
        "Commune_Code",
        "Commune_Nom"
    ];

    private readonly IRepository<Country> _countryRepository;
    private readonly IRepository<Province> _provinceRepository;
    private readonly IRepository<City> _cityRepository;
    private readonly IRepository<Commune> _communeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GeographyAdminService(
        IRepository<Country> countryRepository,
        IRepository<Province> provinceRepository,
        IRepository<City> cityRepository,
        IRepository<Commune> communeRepository,
        IUnitOfWork unitOfWork)
    {
        _countryRepository = countryRepository;
        _provinceRepository = provinceRepository;
        _cityRepository = cityRepository;
        _communeRepository = communeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<GeographyItemDto>> GetAllCountriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _countryRepository.FindAsync(c => includeInactive || c.IsActive, cancellationToken);
        return items.OrderBy(c => c.Name).Select(c => new GeographyItemDto(c.Id, c.Code, c.Name)).ToList();
    }

    public async Task<IReadOnlyList<GeographyItemDto>> GetAllProvincesAsync(
        Guid countryId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _provinceRepository.FindAsync(
            p => p.CountryId == countryId && (includeInactive || p.IsActive),
            cancellationToken);
        return items.OrderBy(p => p.Name).Select(p => new GeographyItemDto(p.Id, p.Code, p.Name)).ToList();
    }

    public async Task<IReadOnlyList<GeographyItemDto>> GetAllCitiesAsync(
        Guid provinceId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _cityRepository.FindAsync(
            c => c.ProvinceId == provinceId && (includeInactive || c.IsActive),
            cancellationToken);
        return items.OrderBy(c => c.Name).Select(c => new GeographyItemDto(c.Id, c.Code, c.Name)).ToList();
    }

    public async Task<IReadOnlyList<GeographyItemDto>> GetAllCommunesAsync(
        Guid cityId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _communeRepository.FindAsync(
            c => c.CityId == cityId && (includeInactive || c.IsActive),
            cancellationToken);
        return items.OrderBy(c => c.Name).Select(c => new GeographyItemDto(c.Id, c.Code, c.Name)).ToList();
    }

    public async Task<GeographyItemDto> SaveCountryAsync(
        UpsertGeographyItemRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCodeName(request.Code, request.Name);
        Country entity;
        if (id.HasValue)
        {
            entity = await GetCountryOrThrowAsync(id.Value, cancellationToken);
            entity.Code = request.Code.Trim().ToUpperInvariant();
            entity.Name = request.Name.Trim();
            entity.IsActive = request.IsActive;
            await _countryRepository.UpdateAsync(entity, cancellationToken);
        }
        else
        {
            entity = new Country
            {
                Code = request.Code.Trim().ToUpperInvariant(),
                Name = request.Name.Trim(),
                IsActive = request.IsActive
            };
            await _countryRepository.AddAsync(entity, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new GeographyItemDto(entity.Id, entity.Code, entity.Name);
    }

    public async Task<GeographyItemDto> SaveProvinceAsync(
        CreateProvinceRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCodeName(request.Code, request.Name);
        _ = await GetCountryOrThrowAsync(request.CountryId, cancellationToken);

        Province entity;
        if (id.HasValue)
        {
            entity = await GetProvinceOrThrowAsync(id.Value, cancellationToken);
            entity.CountryId = request.CountryId;
            entity.Code = request.Code.Trim().ToUpperInvariant();
            entity.Name = request.Name.Trim();
            entity.IsActive = request.IsActive;
            await _provinceRepository.UpdateAsync(entity, cancellationToken);
        }
        else
        {
            entity = new Province
            {
                CountryId = request.CountryId,
                Code = request.Code.Trim().ToUpperInvariant(),
                Name = request.Name.Trim(),
                IsActive = request.IsActive
            };
            await _provinceRepository.AddAsync(entity, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new GeographyItemDto(entity.Id, entity.Code, entity.Name);
    }

    public async Task<GeographyItemDto> SaveCityAsync(
        CreateCityRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCodeName(request.Code, request.Name);
        _ = await GetProvinceOrThrowAsync(request.ProvinceId, cancellationToken);

        City entity;
        if (id.HasValue)
        {
            entity = await GetCityOrThrowAsync(id.Value, cancellationToken);
            entity.ProvinceId = request.ProvinceId;
            entity.Code = request.Code.Trim().ToUpperInvariant();
            entity.Name = request.Name.Trim();
            entity.IsActive = request.IsActive;
            await _cityRepository.UpdateAsync(entity, cancellationToken);
        }
        else
        {
            entity = new City
            {
                ProvinceId = request.ProvinceId,
                Code = request.Code.Trim().ToUpperInvariant(),
                Name = request.Name.Trim(),
                IsActive = request.IsActive
            };
            await _cityRepository.AddAsync(entity, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new GeographyItemDto(entity.Id, entity.Code, entity.Name);
    }

    public async Task<GeographyItemDto> SaveCommuneAsync(
        CreateCommuneRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCodeName(request.Code, request.Name);
        _ = await GetCityOrThrowAsync(request.CityId, cancellationToken);

        Commune entity;
        if (id.HasValue)
        {
            entity = await GetCommuneOrThrowAsync(id.Value, cancellationToken);
            entity.CityId = request.CityId;
            entity.Code = request.Code.Trim().ToUpperInvariant();
            entity.Name = request.Name.Trim();
            entity.IsActive = request.IsActive;
            await _communeRepository.UpdateAsync(entity, cancellationToken);
        }
        else
        {
            entity = new Commune
            {
                CityId = request.CityId,
                Code = request.Code.Trim().ToUpperInvariant(),
                Name = request.Name.Trim(),
                IsActive = request.IsActive
            };
            await _communeRepository.AddAsync(entity, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new GeographyItemDto(entity.Id, entity.Code, entity.Name);
    }

    public async Task DeactivateCountryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetCountryOrThrowAsync(id, cancellationToken);
        entity.IsActive = false;
        await _countryRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateProvinceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetProvinceOrThrowAsync(id, cancellationToken);
        entity.IsActive = false;
        await _provinceRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateCityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetCityOrThrowAsync(id, cancellationToken);
        entity.IsActive = false;
        await _cityRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateCommuneAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetCommuneOrThrowAsync(id, cancellationToken);
        entity.IsActive = false;
        await _communeRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public byte[] BuildImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Geographie");
        for (var i = 0; i < TemplateHeaders.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = TemplateHeaders[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        sheet.Cell(2, 1).Value = "COD";
        sheet.Cell(2, 2).Value = "République Démocratique du Congo";
        sheet.Cell(2, 3).Value = "KIN";
        sheet.Cell(2, 4).Value = "Kinshasa";
        sheet.Cell(2, 5).Value = "KIN-VIL";
        sheet.Cell(2, 6).Value = "Kinshasa";
        sheet.Cell(2, 7).Value = "GOM";
        sheet.Cell(2, 8).Value = "Gombe";
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<GeographyImportResultDto> ImportFromExcelAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<GeographyImportRowError>();
        var countriesCreated = 0;
        var countriesUpdated = 0;
        var provincesCreated = 0;
        var provincesUpdated = 0;
        var citiesCreated = 0;
        var citiesUpdated = 0;
        var communesCreated = 0;
        var communesUpdated = 0;

        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.First();
        var countries = (await _countryRepository.FindAsync(_ => true, cancellationToken)).ToList();
        var provinces = (await _provinceRepository.FindAsync(_ => true, cancellationToken)).ToList();
        var cities = (await _cityRepository.FindAsync(_ => true, cancellationToken)).ToList();
        var communes = (await _communeRepository.FindAsync(_ => true, cancellationToken)).ToList();

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            var countryCode = GetCell(sheet, row, 1);
            var countryName = GetCell(sheet, row, 2);
            var provinceCode = GetCell(sheet, row, 3);
            var provinceName = GetCell(sheet, row, 4);
            var cityCode = GetCell(sheet, row, 5);
            var cityName = GetCell(sheet, row, 6);
            var communeCode = GetCell(sheet, row, 7);
            var communeName = GetCell(sheet, row, 8);

            if (IsEmpty(countryCode, countryName, provinceCode, provinceName, cityCode, cityName, communeCode, communeName))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(countryName))
            {
                errors.Add(new GeographyImportRowError(row, "Pays_Code et Pays_Nom sont obligatoires."));
                continue;
            }

            try
            {
                var (country, countryAction) = await UpsertCountryAsync(
                    countries,
                    countryCode,
                    countryName,
                    cancellationToken);
                if (countryAction == UpsertAction.Created)
                {
                    countriesCreated++;
                }
                else
                {
                    countriesUpdated++;
                }

                if (!string.IsNullOrWhiteSpace(provinceCode) && !string.IsNullOrWhiteSpace(provinceName))
                {
                    var (province, provinceAction) = await UpsertProvinceAsync(
                        provinces,
                        country.Id,
                        provinceCode,
                        provinceName,
                        cancellationToken);
                    if (provinceAction == UpsertAction.Created)
                    {
                        provincesCreated++;
                    }
                    else
                    {
                        provincesUpdated++;
                    }

                    if (!string.IsNullOrWhiteSpace(cityCode) && !string.IsNullOrWhiteSpace(cityName))
                    {
                        var (city, cityAction) = await UpsertCityAsync(
                            cities,
                            province.Id,
                            cityCode,
                            cityName,
                            cancellationToken);
                        if (cityAction == UpsertAction.Created)
                        {
                            citiesCreated++;
                        }
                        else
                        {
                            citiesUpdated++;
                        }

                        if (!string.IsNullOrWhiteSpace(communeCode) && !string.IsNullOrWhiteSpace(communeName))
                        {
                            var communeAction = await UpsertCommuneAsync(
                                communes,
                                city.Id,
                                communeCode,
                                communeName,
                                cancellationToken);
                            if (communeAction == UpsertAction.Created)
                            {
                                communesCreated++;
                            }
                            else
                            {
                                communesUpdated++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new GeographyImportRowError(row, ex.Message));
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new GeographyImportResultDto(
            countriesCreated,
            countriesUpdated,
            provincesCreated,
            provincesUpdated,
            citiesCreated,
            citiesUpdated,
            communesCreated,
            communesUpdated,
            errors);
    }

    private enum UpsertAction
    {
        Created,
        Updated
    }

    private async Task<(Country Country, UpsertAction Action)> UpsertCountryAsync(
        List<Country> countries,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var existing = countries.FirstOrDefault(c => c.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = name.Trim();
            existing.IsActive = true;
            await _countryRepository.UpdateAsync(existing, cancellationToken);
            return (existing, UpsertAction.Updated);
        }

        var country = new Country
        {
            Code = normalizedCode,
            Name = name.Trim(),
            IsActive = true
        };
        await _countryRepository.AddAsync(country, cancellationToken);
        countries.Add(country);
        return (country, UpsertAction.Created);
    }

    private async Task<(Province Province, UpsertAction Action)> UpsertProvinceAsync(
        List<Province> provinces,
        Guid countryId,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var existing = provinces.FirstOrDefault(p =>
            p.CountryId == countryId && p.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = name.Trim();
            existing.IsActive = true;
            await _provinceRepository.UpdateAsync(existing, cancellationToken);
            return (existing, UpsertAction.Updated);
        }

        var province = new Province
        {
            CountryId = countryId,
            Code = normalizedCode,
            Name = name.Trim(),
            IsActive = true
        };
        await _provinceRepository.AddAsync(province, cancellationToken);
        provinces.Add(province);
        return (province, UpsertAction.Created);
    }

    private async Task<(City City, UpsertAction Action)> UpsertCityAsync(
        List<City> cities,
        Guid provinceId,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var existing = cities.FirstOrDefault(c =>
            c.ProvinceId == provinceId && c.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = name.Trim();
            existing.IsActive = true;
            await _cityRepository.UpdateAsync(existing, cancellationToken);
            return (existing, UpsertAction.Updated);
        }

        var city = new City
        {
            ProvinceId = provinceId,
            Code = normalizedCode,
            Name = name.Trim(),
            IsActive = true
        };
        await _cityRepository.AddAsync(city, cancellationToken);
        cities.Add(city);
        return (city, UpsertAction.Created);
    }

    private async Task<UpsertAction> UpsertCommuneAsync(
        List<Commune> communes,
        Guid cityId,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var existing = communes.FirstOrDefault(c =>
            c.CityId == cityId && c.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = name.Trim();
            existing.IsActive = true;
            await _communeRepository.UpdateAsync(existing, cancellationToken);
            return UpsertAction.Updated;
        }

        var commune = new Commune
        {
            CityId = cityId,
            Code = normalizedCode,
            Name = name.Trim(),
            IsActive = true
        };
        await _communeRepository.AddAsync(commune, cancellationToken);
        communes.Add(commune);
        return UpsertAction.Created;
    }

    private static string GetCell(IXLWorksheet sheet, int row, int column) =>
        sheet.Cell(row, column).GetString().Trim();

    private static bool IsEmpty(params string?[] values) =>
        values.All(string.IsNullOrWhiteSpace);

    private static void ValidateCodeName(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Le code et le libellé sont obligatoires.");
        }
    }

    private async Task<Country> GetCountryOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        (await _countryRepository.FindAsync(c => c.Id == id, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Pays introuvable.");

    private async Task<Province> GetProvinceOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        (await _provinceRepository.FindAsync(p => p.Id == id, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Province introuvable.");

    private async Task<City> GetCityOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        (await _cityRepository.FindAsync(c => c.Id == id, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Ville introuvable.");

    private async Task<Commune> GetCommuneOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        (await _communeRepository.FindAsync(c => c.Id == id, cancellationToken)).FirstOrDefault()
        ?? throw new KeyNotFoundException("Commune introuvable.");
}
