using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;

namespace SchoolManagement.Desktop.ViewModels;

public partial class GeographyAdminViewModel : ViewModelBase
{
    private readonly IGeographyAdminApiService _api;

    public GeographyAdminViewModel(IGeographyAdminApiService api)
    {
        _api = api;
    }

    public ObservableCollection<GeographyItemDto> Countries { get; } = [];
    public ObservableCollection<GeographyItemDto> Provinces { get; } = [];
    public ObservableCollection<GeographyItemDto> CityProvinces { get; } = [];
    public ObservableCollection<GeographyItemDto> Cities { get; } = [];
    public ObservableCollection<GeographyItemDto> CommuneProvinces { get; } = [];
    public ObservableCollection<GeographyItemDto> CommuneCities { get; } = [];
    public ObservableCollection<GeographyItemDto> Communes { get; } = [];

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _includeInactive;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _validationMessage;
    [ObservableProperty] private string? _importSummary;

    [ObservableProperty] private GeographyItemDto? _selectedCountry;
    [ObservableProperty] private string _countryCode = string.Empty;
    [ObservableProperty] private string _countryName = string.Empty;
    private Guid? _editingCountryId;

    [ObservableProperty] private GeographyItemDto? _provinceCountryFilter;
    [ObservableProperty] private GeographyItemDto? _selectedProvince;
    [ObservableProperty] private string _provinceCode = string.Empty;
    [ObservableProperty] private string _provinceName = string.Empty;
    private Guid? _editingProvinceId;

    [ObservableProperty] private GeographyItemDto? _cityCountryFilter;
    [ObservableProperty] private GeographyItemDto? _cityProvinceFilter;
    [ObservableProperty] private GeographyItemDto? _selectedCity;
    [ObservableProperty] private string _cityCode = string.Empty;
    [ObservableProperty] private string _cityName = string.Empty;
    private Guid? _editingCityId;

    [ObservableProperty] private GeographyItemDto? _communeCountryFilter;
    [ObservableProperty] private GeographyItemDto? _communeProvinceFilter;
    [ObservableProperty] private GeographyItemDto? _communeCityFilter;
    [ObservableProperty] private GeographyItemDto? _selectedCommune;
    [ObservableProperty] private string _communeCode = string.Empty;
    [ObservableProperty] private string _communeName = string.Empty;
    private Guid? _editingCommuneId;

    partial void OnIncludeInactiveChanged(bool value) => _ = RefreshCurrentTabAsync();

    partial void OnSelectedCountryChanged(GeographyItemDto? value)
    {
        if (value is null)
        {
            ClearCountryForm();
            return;
        }

        _editingCountryId = value.Id;
        CountryCode = value.Code;
        CountryName = value.Name;
    }

    partial void OnProvinceCountryFilterChanged(GeographyItemDto? value) => _ = LoadProvincesAsync();

    partial void OnSelectedProvinceChanged(GeographyItemDto? value)
    {
        if (value is null)
        {
            ClearProvinceForm();
            return;
        }

        _editingProvinceId = value.Id;
        ProvinceCode = value.Code;
        ProvinceName = value.Name;
    }

    partial void OnCityCountryFilterChanged(GeographyItemDto? value) => _ = LoadCityProvincesAsync();

    partial void OnCityProvinceFilterChanged(GeographyItemDto? value) => _ = LoadCitiesAsync();

    partial void OnSelectedCityChanged(GeographyItemDto? value)
    {
        if (value is null)
        {
            ClearCityForm();
            return;
        }

        _editingCityId = value.Id;
        CityCode = value.Code;
        CityName = value.Name;
    }

    partial void OnCommuneCountryFilterChanged(GeographyItemDto? value) => _ = LoadCommuneProvincesAsync();

    partial void OnCommuneProvinceFilterChanged(GeographyItemDto? value) => _ = LoadCommuneCitiesAsync();

    partial void OnCommuneCityFilterChanged(GeographyItemDto? value) => _ = LoadCommunesAsync();

    partial void OnSelectedCommuneChanged(GeographyItemDto? value)
    {
        if (value is null)
        {
            ClearCommuneForm();
            return;
        }

        _editingCommuneId = value.Id;
        CommuneCode = value.Code;
        CommuneName = value.Name;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await LoadCountriesAsync();
        if (ProvinceCountryFilter is null && Countries.Count > 0)
        {
            ProvinceCountryFilter = Countries[0];
        }

        if (CityCountryFilter is null && Countries.Count > 0)
        {
            CityCountryFilter = Countries[0];
        }

        if (CommuneCountryFilter is null && Countries.Count > 0)
        {
            CommuneCountryFilter = Countries[0];
        }
    }

    [RelayCommand]
    private async Task RefreshCurrentTabAsync()
    {
        switch (SelectedTabIndex)
        {
            case 0:
                await LoadCountriesAsync();
                break;
            case 1:
                await LoadProvincesAsync();
                break;
            case 2:
                await LoadCitiesAsync();
                break;
            default:
                await LoadCommunesAsync();
                break;
        }
    }

    [RelayCommand]
    private void NewCountry()
    {
        SelectedCountry = null;
        ClearCountryForm();
    }

    [RelayCommand]
    private async Task SaveCountryAsync()
    {
        if (string.IsNullOrWhiteSpace(CountryCode) || string.IsNullOrWhiteSpace(CountryName))
        {
            ValidationMessage = "Code et nom du pays sont obligatoires.";
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            var saved = await _api.SaveCountryAsync(
                new UpsertGeographyItemRequest(CountryCode.Trim(), CountryName.Trim()),
                _editingCountryId);
            StatusMessage = _editingCountryId.HasValue ? "Pays mis à jour." : "Pays enregistré.";
            await LoadCountriesAsync();
            SelectedCountry = Countries.FirstOrDefault(c => c.Id == saved.Id);
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateCountryAsync()
    {
        if (_editingCountryId is null)
        {
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            await _api.DeactivateCountryAsync(_editingCountryId.Value);
            StatusMessage = "Pays désactivé.";
            NewCountry();
            await LoadCountriesAsync();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewProvince()
    {
        SelectedProvince = null;
        ClearProvinceForm();
    }

    [RelayCommand]
    private async Task SaveProvinceAsync()
    {
        if (ProvinceCountryFilter is null)
        {
            ValidationMessage = "Sélectionnez un pays.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ProvinceCode) || string.IsNullOrWhiteSpace(ProvinceName))
        {
            ValidationMessage = "Code et nom de la province sont obligatoires.";
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            var saved = await _api.SaveProvinceAsync(
                new CreateProvinceRequest(ProvinceCountryFilter.Id, ProvinceCode.Trim(), ProvinceName.Trim()),
                _editingProvinceId);
            StatusMessage = _editingProvinceId.HasValue ? "Province mise à jour." : "Province enregistrée.";
            await LoadProvincesAsync();
            SelectedProvince = Provinces.FirstOrDefault(p => p.Id == saved.Id);
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateProvinceAsync()
    {
        if (_editingProvinceId is null)
        {
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            await _api.DeactivateProvinceAsync(_editingProvinceId.Value);
            StatusMessage = "Province désactivée.";
            NewProvince();
            await LoadProvincesAsync();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewCity()
    {
        SelectedCity = null;
        ClearCityForm();
    }

    [RelayCommand]
    private async Task SaveCityAsync()
    {
        if (CityProvinceFilter is null)
        {
            ValidationMessage = "Sélectionnez une province.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CityCode) || string.IsNullOrWhiteSpace(CityName))
        {
            ValidationMessage = "Code et nom de la ville sont obligatoires.";
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            var saved = await _api.SaveCityAsync(
                new CreateCityRequest(CityProvinceFilter.Id, CityCode.Trim(), CityName.Trim()),
                _editingCityId);
            StatusMessage = _editingCityId.HasValue ? "Ville mise à jour." : "Ville enregistrée.";
            await LoadCitiesAsync();
            SelectedCity = Cities.FirstOrDefault(c => c.Id == saved.Id);
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateCityAsync()
    {
        if (_editingCityId is null)
        {
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            await _api.DeactivateCityAsync(_editingCityId.Value);
            StatusMessage = "Ville désactivée.";
            NewCity();
            await LoadCitiesAsync();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewCommune()
    {
        SelectedCommune = null;
        ClearCommuneForm();
    }

    [RelayCommand]
    private async Task SaveCommuneAsync()
    {
        if (CommuneCityFilter is null)
        {
            ValidationMessage = "Sélectionnez une ville.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CommuneCode) || string.IsNullOrWhiteSpace(CommuneName))
        {
            ValidationMessage = "Code et nom de la commune sont obligatoires.";
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            var saved = await _api.SaveCommuneAsync(
                new CreateCommuneRequest(CommuneCityFilter.Id, CommuneCode.Trim(), CommuneName.Trim()),
                _editingCommuneId);
            StatusMessage = _editingCommuneId.HasValue ? "Commune mise à jour." : "Commune enregistrée.";
            await LoadCommunesAsync();
            SelectedCommune = Communes.FirstOrDefault(c => c.Id == saved.Id);
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateCommuneAsync()
    {
        if (_editingCommuneId is null)
        {
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            await _api.DeactivateCommuneAsync(_editingCommuneId.Value);
            StatusMessage = "Commune désactivée.";
            NewCommune();
            await LoadCommunesAsync();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadTemplateAsync()
    {
        IsBusy = true;
        ValidationMessage = null;
        try
        {
            var bytes = await _api.DownloadImportTemplateAsync();
            var dialog = new SaveFileDialog
            {
                Filter = "Excel|*.xlsx",
                FileName = "Modele_Geographie.xlsx"
            };
            if (ErpFileDialog.ShowSave(dialog, ErpFileDialog.ResolveOwnerWindow()) != true)
            {
                return;
            }

            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            StatusMessage = "Modèle Excel enregistré.";
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportExcelAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel|*.xlsx"
        };
        if (ErpFileDialog.ShowOpen(dialog, ErpFileDialog.ResolveOwnerWindow()) != true)
        {
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        ImportSummary = null;
        try
        {
            var result = await _api.ImportExcelAsync(dialog.FileName);
            ImportSummary =
                $"Import terminé : {result.TotalProcessed} enregistrement(s) traité(s) " +
                $"(Pays +{result.CountriesCreated}/~{result.CountriesUpdated}, " +
                $"Provinces +{result.ProvincesCreated}/~{result.ProvincesUpdated}, " +
                $"Villes +{result.CitiesCreated}/~{result.CitiesUpdated}, " +
                $"Communes +{result.CommunesCreated}/~{result.CommunesUpdated}).";

            if (result.Errors.Count > 0)
            {
                var preview = string.Join(Environment.NewLine, result.Errors.Take(5).Select(e => $"Ligne {e.RowNumber} : {e.Message}"));
                ImportSummary += result.Errors.Count > 5
                    ? $"{Environment.NewLine}{preview}{Environment.NewLine}… et {result.Errors.Count - 5} autre(s) erreur(s)."
                    : $"{Environment.NewLine}{preview}";
            }

            StatusMessage = "Import Excel terminé.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCountriesAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _api.GetCountriesAsync(IncludeInactive);
            Countries.Clear();
            foreach (var item in items)
            {
                Countries.Add(item);
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadProvincesAsync()
    {
        Provinces.Clear();
        if (ProvinceCountryFilter is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var items = await _api.GetProvincesAsync(ProvinceCountryFilter.Id, IncludeInactive);
            foreach (var item in items)
            {
                Provinces.Add(item);
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCityProvincesAsync()
    {
        Cities.Clear();
        CityProvinces.Clear();
        CityProvinceFilter = null;
        if (CityCountryFilter is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var items = await _api.GetProvincesAsync(CityCountryFilter.Id, IncludeInactive);
            foreach (var item in items)
            {
                CityProvinces.Add(item);
            }

            if (CityProvinces.Count > 0)
            {
                CityProvinceFilter = CityProvinces[0];
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCitiesAsync()
    {
        Cities.Clear();
        if (CityProvinceFilter is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var items = await _api.GetCitiesAsync(CityProvinceFilter.Id, IncludeInactive);
            foreach (var item in items)
            {
                Cities.Add(item);
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCommuneProvincesAsync()
    {
        CommuneProvinces.Clear();
        CommuneProvinceFilter = null;
        CommuneCities.Clear();
        CommuneCityFilter = null;
        Communes.Clear();
        if (CommuneCountryFilter is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var items = await _api.GetProvincesAsync(CommuneCountryFilter.Id, IncludeInactive);
            foreach (var item in items)
            {
                CommuneProvinces.Add(item);
            }

            if (CommuneProvinces.Count > 0)
            {
                CommuneProvinceFilter = CommuneProvinces[0];
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCommuneCitiesAsync()
    {
        CommuneCities.Clear();
        CommuneCityFilter = null;
        Communes.Clear();
        if (CommuneProvinceFilter is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var items = await _api.GetCitiesAsync(CommuneProvinceFilter.Id, IncludeInactive);
            foreach (var item in items)
            {
                CommuneCities.Add(item);
            }

            if (CommuneCities.Count > 0)
            {
                CommuneCityFilter = CommuneCities[0];
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCommunesAsync()
    {
        Communes.Clear();
        if (CommuneCityFilter is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var items = await _api.GetCommunesAsync(CommuneCityFilter.Id, IncludeInactive);
            foreach (var item in items)
            {
                Communes.Add(item);
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearCountryForm()
    {
        _editingCountryId = null;
        CountryCode = string.Empty;
        CountryName = string.Empty;
    }

    private void ClearProvinceForm()
    {
        _editingProvinceId = null;
        ProvinceCode = string.Empty;
        ProvinceName = string.Empty;
    }

    private void ClearCityForm()
    {
        _editingCityId = null;
        CityCode = string.Empty;
        CityName = string.Empty;
    }

    private void ClearCommuneForm()
    {
        _editingCommuneId = null;
        CommuneCode = string.Empty;
        CommuneName = string.Empty;
    }
}
