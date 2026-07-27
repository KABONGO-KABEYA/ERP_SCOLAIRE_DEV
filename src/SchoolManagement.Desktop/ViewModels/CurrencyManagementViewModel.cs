using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.CurrencyManagement.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;

namespace SchoolManagement.Desktop.ViewModels;

public enum CurrencyManagementMode
{
    Monnaies = 1,
    TauxChange = 2,
    DevisesEtablissement = 3,
    HistoriqueTaux = 4
}

/// <summary>Paramètres : monnaies, taux, devises établissement, historique.</summary>
public partial class CurrencyManagementViewModel : ViewModelBase
{
    private readonly ICurrencyApiService _api;

    public CurrencyManagementViewModel(ICurrencyApiService api)
    {
        _api = api;
    }

    public ObservableCollection<CurrencyDefinitionDto> Currencies { get; } = [];
    public ObservableCollection<SchoolCurrencyDto> SchoolCurrencies { get; } = [];
    public ObservableCollection<ExchangeRateTypeDto> RateTypes { get; } = [];
    public ObservableCollection<ExchangeRateDto> ExchangeRates { get; } = [];
    public ObservableCollection<ExchangeRateHistoryDto> HistoryRows { get; } = [];

    [ObservableProperty] private CurrencyManagementMode _mode = CurrencyManagementMode.Monnaies;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private FeeStatusMessageKind _statusMessageKind = FeeStatusMessageKind.None;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _activeOnly = true;

    [ObservableProperty] private CurrencyDefinitionDto? _selectedCurrency;
    [ObservableProperty] private string _currencyCode = string.Empty;
    [ObservableProperty] private string _currencyName = string.Empty;
    [ObservableProperty] private string _currencySymbol = string.Empty;
    [ObservableProperty] private string _currencyDecimals = "2";
    [ObservableProperty] private bool _currencyIsSystemLocal;
    [ObservableProperty] private bool _currencyIsActive = true;

    [ObservableProperty] private CurrencyDefinitionDto? _schoolCurrencyPick;
    [ObservableProperty] private bool _schoolCurrencyIsPrimary;
    [ObservableProperty] private bool _schoolCurrencyAllowPayment = true;
    [ObservableProperty] private SchoolCurrencyDto? _selectedSchoolCurrency;

    [ObservableProperty] private CurrencyDefinitionDto? _rateSource;
    [ObservableProperty] private CurrencyDefinitionDto? _rateTarget;
    [ObservableProperty] private ExchangeRateTypeDto? _rateType;
    [ObservableProperty] private DateTime _rateEffectiveDate = DateTime.Today;
    [ObservableProperty] private string _rateValue = "1";
    [ObservableProperty] private bool _rateIsActive = true;
    [ObservableProperty] private string _rateNotes = string.Empty;
    [ObservableProperty] private string _inverseRateHint = string.Empty;
    [ObservableProperty] private ExchangeRateDto? _selectedRate;

    public bool IsMonnaiesMode => Mode == CurrencyManagementMode.Monnaies;
    public bool IsTauxMode => Mode == CurrencyManagementMode.TauxChange;
    public bool IsSchoolCurrenciesMode => Mode == CurrencyManagementMode.DevisesEtablissement;
    public bool IsHistoryMode => Mode == CurrencyManagementMode.HistoriqueTaux;

    partial void OnRateValueChanged(string value) => UpdateInverseRateHint();
    partial void OnRateSourceChanged(CurrencyDefinitionDto? value) => UpdateInverseRateHint();
    partial void OnRateTargetChanged(CurrencyDefinitionDto? value) => UpdateInverseRateHint();

    private void UpdateInverseRateHint()
    {
        if (RateSource is null || RateTarget is null
            || !decimal.TryParse(RateValue.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var rate)
            || rate <= 0)
        {
            InverseRateHint = "L'inverse sera calculé automatiquement à la conversion (ex. 1 CDF = 1 ÷ taux).";
            return;
        }

        var inverse = Math.Round(1m / rate, 10, MidpointRounding.AwayFromZero);
        InverseRateHint =
            $"Inverse automatique : 1 {RateTarget.Code} = {inverse.ToString("0.##########", System.Globalization.CultureInfo.InvariantCulture)} {RateSource.Code}";
    }

    public void SetMode(CurrencyManagementMode mode)
    {
        Mode = mode;
        OnPropertyChanged(nameof(IsMonnaiesMode));
        OnPropertyChanged(nameof(IsTauxMode));
        OnPropertyChanged(nameof(IsSchoolCurrenciesMode));
        OnPropertyChanged(nameof(IsHistoryMode));
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            switch (Mode)
            {
                case CurrencyManagementMode.Monnaies:
                    await ReloadCurrenciesAsync();
                    break;
                case CurrencyManagementMode.TauxChange:
                    await ReloadCurrenciesAsync();
                    await ReloadRateTypesAsync();
                    await ReloadRatesAsync();
                    UpdateInverseRateHint();
                    break;
                case CurrencyManagementMode.DevisesEtablissement:
                    await ReloadCurrenciesAsync(activeOnlyOverride: true);
                    await ReloadSchoolCurrenciesAsync();
                    break;
                case CurrencyManagementMode.HistoriqueTaux:
                    await ReloadHistoryAsync();
                    break;
            }

            SetOk("Données chargées.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewCurrency()
    {
        SelectedCurrency = null;
        CurrencyCode = string.Empty;
        CurrencyName = string.Empty;
        CurrencySymbol = string.Empty;
        CurrencyDecimals = "2";
        CurrencyIsSystemLocal = false;
        CurrencyIsActive = true;
    }

    partial void OnSelectedCurrencyChanged(CurrencyDefinitionDto? value)
    {
        if (value is null) return;
        CurrencyCode = value.Code;
        CurrencyName = value.Name;
        CurrencySymbol = value.Symbol;
        CurrencyDecimals = value.DecimalPlaces.ToString();
        CurrencyIsSystemLocal = value.IsSystemLocal;
        CurrencyIsActive = value.IsActive;
    }

    [RelayCommand]
    private async Task SaveCurrencyAsync()
    {
        try
        {
            IsBusy = true;
            if (!int.TryParse(CurrencyDecimals, out var decimals))
                decimals = 2;
            var request = new SaveCurrencyDefinitionRequest(
                CurrencyCode, CurrencyName, CurrencySymbol, decimals, CurrencyIsSystemLocal, CurrencyIsActive);
            if (SelectedCurrency is null)
                await _api.CreateCurrencyAsync(request);
            else
                await _api.UpdateCurrencyAsync(SelectedCurrency.Id, request);
            await ReloadCurrenciesAsync();
            SetOk("Devise enregistrée.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleCurrencyActiveAsync()
    {
        if (SelectedCurrency is null) return;
        try
        {
            IsBusy = true;
            await _api.SetCurrencyActiveAsync(SelectedCurrency.Id, !SelectedCurrency.IsActive);
            await ReloadCurrenciesAsync();
            SetOk(SelectedCurrency.IsActive ? "Devise désactivée." : "Devise activée.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSchoolCurrencyAsync()
    {
        if (SchoolCurrencyPick is null)
        {
            SetError("Sélectionnez une devise.");
            return;
        }

        try
        {
            IsBusy = true;
            await _api.UpsertSchoolCurrencyAsync(new SaveSchoolCurrencyRequest(
                SchoolCurrencyPick.Id, SchoolCurrencyIsPrimary, SchoolCurrencyAllowPayment));
            await ReloadSchoolCurrenciesAsync();
            SetOk("Devise d'établissement enregistrée.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveSchoolCurrencyAsync()
    {
        if (SelectedSchoolCurrency is null) return;
        if (MessageBox.Show("Retirer cette devise de l'établissement ?", "Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            IsBusy = true;
            await _api.RemoveSchoolCurrencyAsync(SelectedSchoolCurrency.Id);
            await ReloadSchoolCurrenciesAsync();
            SetOk("Devise retirée.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveExchangeRateAsync()
    {
        if (RateSource is null || RateTarget is null || RateType is null)
        {
            SetError("Source, destination et type de taux sont obligatoires.");
            return;
        }

        if (!decimal.TryParse(RateValue.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var rate) || rate <= 0)
        {
            SetError("Taux invalide.");
            return;
        }

        try
        {
            IsBusy = true;
            var request = new SaveExchangeRateRequest(
                RateSource.Id,
                RateTarget.Id,
                RateType.Id,
                DateOnly.FromDateTime(RateEffectiveDate),
                rate,
                RateIsActive,
                string.IsNullOrWhiteSpace(RateNotes) ? null : RateNotes);
            if (SelectedRate is null)
                await _api.CreateExchangeRateAsync(request);
            else
                await _api.UpdateExchangeRateAsync(SelectedRate.Id, request);
            await ReloadRatesAsync();
            SetOk("Taux enregistré.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewExchangeRate()
    {
        SelectedRate = null;
        RateEffectiveDate = DateTime.Today;
        RateValue = "1";
        RateIsActive = true;
        RateNotes = string.Empty;
    }

    partial void OnSelectedRateChanged(ExchangeRateDto? value)
    {
        if (value is null) return;
        RateSource = Currencies.FirstOrDefault(c => c.Id == value.SourceCurrencyId);
        RateTarget = Currencies.FirstOrDefault(c => c.Id == value.TargetCurrencyId);
        RateType = RateTypes.FirstOrDefault(t => t.Id == value.RateTypeId);
        RateEffectiveDate = value.EffectiveDate.ToDateTime(TimeOnly.MinValue);
        RateValue = value.Rate.ToString(System.Globalization.CultureInfo.InvariantCulture);
        RateIsActive = value.IsActive;
        RateNotes = value.Notes ?? string.Empty;
    }

    [RelayCommand]
    private async Task ActivateSelectedRateAsync()
    {
        if (SelectedRate is null) return;
        try
        {
            IsBusy = true;
            await _api.ActivateExchangeRateAsync(SelectedRate.Id);
            await ReloadRatesAsync();
            SetOk("Taux activé.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateSelectedRateAsync()
    {
        if (SelectedRate is null) return;
        if (MessageBox.Show("Désactiver ce taux ?", "Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            IsBusy = true;
            await _api.DeactivateExchangeRateAsync(SelectedRate.Id);
            await ReloadRatesAsync();
            SetOk("Taux désactivé.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadCurrenciesAsync(bool? activeOnlyOverride = null)
    {
        var activeOnly = activeOnlyOverride ?? (ActiveOnly ? true : null);
        var items = await _api.SearchCurrenciesAsync(
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            activeOnly);
        Currencies.Clear();
        foreach (var item in items)
            Currencies.Add(item);
    }

    private async Task ReloadSchoolCurrenciesAsync()
    {
        var items = await _api.GetSchoolCurrenciesAsync();
        SchoolCurrencies.Clear();
        foreach (var item in items)
            SchoolCurrencies.Add(item);
    }

    private async Task ReloadRateTypesAsync()
    {
        var items = await _api.GetRateTypesAsync(activeOnly: true);
        RateTypes.Clear();
        foreach (var item in items)
            RateTypes.Add(item);
    }

    private async Task ReloadRatesAsync()
    {
        var items = await _api.SearchExchangeRatesAsync(activeOnly: ActiveOnly ? true : null);
        ExchangeRates.Clear();
        foreach (var item in items)
            ExchangeRates.Add(item);
    }

    private async Task ReloadHistoryAsync()
    {
        var items = await _api.GetHistoryAsync(take: 300);
        HistoryRows.Clear();
        foreach (var item in items)
            HistoryRows.Add(item);
    }

    private void SetOk(string message)
    {
        StatusMessage = message;
        StatusMessageKind = FeeStatusMessageKind.Success;
    }

    private void SetError(string message)
    {
        StatusMessage = message;
        StatusMessageKind = FeeStatusMessageKind.Error;
    }
}
