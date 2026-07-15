using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class AddressEditorViewModel : ObservableObject
{
    private readonly IGeographyApiService _geographyApi;
    private bool _suppressCascadeHandlers;
    private int _cascadeVersion;

    public AddressEditorViewModel(IGeographyApiService geographyApi)
    {
        _geographyApi = geographyApi;
    }

    public ObservableCollection<GeographyItemDto> Countries { get; } = [];
    public ObservableCollection<GeographyItemDto> Provinces { get; } = [];
    public ObservableCollection<GeographyItemDto> Cities { get; } = [];
    public ObservableCollection<GeographyItemDto> Communes { get; } = [];

    [ObservableProperty] private GeographyItemDto? _selectedCountry;
    [ObservableProperty] private GeographyItemDto? _selectedProvince;
    [ObservableProperty] private GeographyItemDto? _selectedCity;
    [ObservableProperty] private GeographyItemDto? _selectedCommune;
    [ObservableProperty] private string _neighborhood = string.Empty;
    [ObservableProperty] private string _avenue = string.Empty;
    [ObservableProperty] private string _houseNumber = string.Empty;
    [ObservableProperty] private bool _isInitialized;
    [ObservableProperty] private bool _isLoadingGeography;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return;
        }

        Countries.Clear();
        foreach (var country in await _geographyApi.GetCountriesAsync(cancellationToken))
        {
            Countries.Add(country);
        }

        IsInitialized = true;
    }

    partial void OnSelectedCountryChanged(GeographyItemDto? value)
    {
        if (_suppressCascadeHandlers)
        {
            return;
        }

        _ = SetCountryAsync(value);
    }

    partial void OnSelectedProvinceChanged(GeographyItemDto? value)
    {
        if (_suppressCascadeHandlers)
        {
            return;
        }

        _ = SetProvinceAsync(value);
    }

    partial void OnSelectedCityChanged(GeographyItemDto? value)
    {
        if (_suppressCascadeHandlers)
        {
            return;
        }

        _ = SetCityAsync(value);
    }

    public async Task SetCountryAsync(GeographyItemDto? country, CancellationToken cancellationToken = default)
    {
        IsLoadingGeography = true;
        try
        {
            SetSelectedCountrySilently(country);
            await LoadProvincesAsync(country?.Id, cancellationToken);
        }
        finally
        {
            IsLoadingGeography = false;
        }
    }

    public async Task SetProvinceAsync(GeographyItemDto? province, CancellationToken cancellationToken = default)
    {
        IsLoadingGeography = true;
        try
        {
            SetSelectedProvinceSilently(province);
            await LoadCitiesAsync(province?.Id, cancellationToken);
        }
        finally
        {
            IsLoadingGeography = false;
        }
    }

    public async Task SetCityAsync(GeographyItemDto? city, CancellationToken cancellationToken = default)
    {
        IsLoadingGeography = true;
        try
        {
            SetSelectedCitySilently(city);
            await LoadCommunesAsync(city?.Id, cancellationToken);
        }
        finally
        {
            IsLoadingGeography = false;
        }
    }

    public Task EnsureProvincesLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCountry is null || Provinces.Count > 0)
        {
            return Task.CompletedTask;
        }

        return LoadProvincesAsync(SelectedCountry.Id, cancellationToken);
    }

    public Task EnsureCitiesLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProvince is null || Cities.Count > 0)
        {
            return Task.CompletedTask;
        }

        return LoadCitiesAsync(SelectedProvince.Id, cancellationToken);
    }

    public Task EnsureCommunesLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCity is null || Communes.Count > 0)
        {
            return Task.CompletedTask;
        }

        return LoadCommunesAsync(SelectedCity.Id, cancellationToken);
    }

    public async Task LoadFromInputAsync(AddressInputDto? input, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        _suppressCascadeHandlers = true;
        IsLoadingGeography = true;
        try
        {
            SelectedCountry = input?.CountryId is Guid countryId
                ? Countries.FirstOrDefault(c => c.Id == countryId)
                : null;

            if (SelectedCountry is not null)
            {
                await LoadProvincesAsync(SelectedCountry.Id, cancellationToken);
                SelectedProvince = input?.ProvinceId is Guid provinceId
                    ? Provinces.FirstOrDefault(p => p.Id == provinceId)
                    : null;
            }
            else
            {
                Provinces.Clear();
                Cities.Clear();
                Communes.Clear();
                SelectedProvince = null;
                SelectedCity = null;
                SelectedCommune = null;
            }

            if (SelectedProvince is not null)
            {
                await LoadCitiesAsync(SelectedProvince.Id, cancellationToken);
                SelectedCity = input?.CityId is Guid cityId
                    ? Cities.FirstOrDefault(c => c.Id == cityId)
                    : null;
            }
            else
            {
                Cities.Clear();
                Communes.Clear();
                SelectedCity = null;
                SelectedCommune = null;
            }

            if (SelectedCity is not null)
            {
                await LoadCommunesAsync(SelectedCity.Id, cancellationToken);
                SelectedCommune = input?.CommuneId is Guid communeId
                    ? Communes.FirstOrDefault(c => c.Id == communeId)
                    : null;
            }
            else
            {
                Communes.Clear();
                SelectedCommune = null;
            }

            Neighborhood = input?.Neighborhood ?? string.Empty;
            Avenue = input?.Avenue ?? string.Empty;
            HouseNumber = input?.HouseNumber ?? string.Empty;
        }
        finally
        {
            _suppressCascadeHandlers = false;
            IsLoadingGeography = false;
        }
    }

    public AddressInputDto ToInputDto() => new(
        SelectedCountry?.Id,
        SelectedProvince?.Id,
        SelectedCity?.Id,
        SelectedCommune?.Id,
        string.IsNullOrWhiteSpace(Neighborhood) ? null : Neighborhood.Trim(),
        string.IsNullOrWhiteSpace(Avenue) ? null : Avenue.Trim(),
        string.IsNullOrWhiteSpace(HouseNumber) ? null : HouseNumber.Trim());

    public bool HasContent() => AddressFormatting.HasContent(ToInputDto());

    public async Task LoadFromDtoAsync(AddressDto? dto, CancellationToken cancellationToken = default)
    {
        if (dto is null)
        {
            Reset();
            return;
        }

        await LoadFromInputAsync(new AddressInputDto(
            dto.CountryId,
            dto.ProvinceId,
            dto.CityId,
            dto.CommuneId,
            dto.Neighborhood,
            dto.Avenue,
            dto.HouseNumber), cancellationToken);
    }

    public void Reset()
    {
        _suppressCascadeHandlers = true;
        try
        {
            SelectedCountry = null;
            SelectedProvince = null;
            SelectedCity = null;
            SelectedCommune = null;
            Provinces.Clear();
            Cities.Clear();
            Communes.Clear();
            Neighborhood = string.Empty;
            Avenue = string.Empty;
            HouseNumber = string.Empty;
        }
        finally
        {
            _suppressCascadeHandlers = false;
        }
    }

    public void CopyFrom(AddressEditorViewModel source)
    {
        _suppressCascadeHandlers = true;
        try
        {
            SelectedCountry = source.SelectedCountry;
            Provinces.Clear();
            foreach (var item in source.Provinces)
            {
                Provinces.Add(item);
            }

            SelectedProvince = source.SelectedProvince;
            Cities.Clear();
            foreach (var item in source.Cities)
            {
                Cities.Add(item);
            }

            SelectedCity = source.SelectedCity;
            Communes.Clear();
            foreach (var item in source.Communes)
            {
                Communes.Add(item);
            }

            SelectedCommune = source.SelectedCommune;
            Neighborhood = source.Neighborhood;
            Avenue = source.Avenue;
            HouseNumber = source.HouseNumber;
        }
        finally
        {
            _suppressCascadeHandlers = false;
        }
    }

    private void SetSelectedCountrySilently(GeographyItemDto? country)
    {
        if (ReferenceEquals(SelectedCountry, country))
        {
            return;
        }

        _suppressCascadeHandlers = true;
        try
        {
            SelectedCountry = country;
        }
        finally
        {
            _suppressCascadeHandlers = false;
        }
    }

    private void SetSelectedProvinceSilently(GeographyItemDto? province)
    {
        if (ReferenceEquals(SelectedProvince, province))
        {
            return;
        }

        _suppressCascadeHandlers = true;
        try
        {
            SelectedProvince = province;
        }
        finally
        {
            _suppressCascadeHandlers = false;
        }
    }

    private void SetSelectedCitySilently(GeographyItemDto? city)
    {
        if (ReferenceEquals(SelectedCity, city))
        {
            return;
        }

        _suppressCascadeHandlers = true;
        try
        {
            SelectedCity = city;
        }
        finally
        {
            _suppressCascadeHandlers = false;
        }
    }

    private async Task LoadProvincesAsync(Guid? countryId, CancellationToken cancellationToken = default)
    {
        var version = ++_cascadeVersion;
        Provinces.Clear();
        Cities.Clear();
        Communes.Clear();
        SetSelectedProvinceSilently(null);
        SetSelectedCitySilently(null);
        SetSelectedCommuneSilently(null);

        if (!countryId.HasValue)
        {
            return;
        }

        foreach (var province in await _geographyApi.GetProvincesAsync(countryId.Value, cancellationToken))
        {
            if (version != _cascadeVersion)
            {
                return;
            }

            Provinces.Add(province);
        }
    }

    private async Task LoadCitiesAsync(Guid? provinceId, CancellationToken cancellationToken = default)
    {
        var version = ++_cascadeVersion;
        Cities.Clear();
        Communes.Clear();
        SetSelectedCitySilently(null);
        SetSelectedCommuneSilently(null);

        if (!provinceId.HasValue)
        {
            return;
        }

        foreach (var city in await _geographyApi.GetCitiesAsync(provinceId.Value, cancellationToken))
        {
            if (version != _cascadeVersion)
            {
                return;
            }

            Cities.Add(city);
        }
    }

    private async Task LoadCommunesAsync(Guid? cityId, CancellationToken cancellationToken = default)
    {
        var version = ++_cascadeVersion;
        Communes.Clear();
        SetSelectedCommuneSilently(null);

        if (!cityId.HasValue)
        {
            return;
        }

        foreach (var commune in await _geographyApi.GetCommunesAsync(cityId.Value, cancellationToken))
        {
            if (version != _cascadeVersion)
            {
                return;
            }

            Communes.Add(commune);
        }
    }

    private void SetSelectedCommuneSilently(GeographyItemDto? commune)
    {
        if (ReferenceEquals(SelectedCommune, commune))
        {
            return;
        }

        _suppressCascadeHandlers = true;
        try
        {
            SelectedCommune = commune;
        }
        finally
        {
            _suppressCascadeHandlers = false;
        }
    }
}
