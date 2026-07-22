using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Withholdings.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Configuration Paramètres : destinations + clés de répartition (frais et retenues).</summary>
public partial class RevenueAllocationConfigViewModel : ViewModelBase
{
    private readonly IRevenueAllocationApiService _allocationApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly ISchoolFeeApiService _schoolFeeApi;
    private readonly IWithholdingApiService _withholdingApi;
    private readonly object _destinationsSync = new();
    private readonly object _keysSync = new();
    private int _destinationsLoadVersion;
    private int _keysLoadVersion;
    private int _catalogLoadVersion;

    public RevenueAllocationConfigViewModel(
        IRevenueAllocationApiService allocationApi,
        ISchoolApiService schoolApi,
        ISchoolFeeApiService schoolFeeApi,
        IWithholdingApiService withholdingApi)
    {
        _allocationApi = allocationApi;
        _schoolApi = schoolApi;
        _schoolFeeApi = schoolFeeApi;
        _withholdingApi = withholdingApi;
        SelectedSourceKind = SourceKindOptions[0];
    }

    public IReadOnlyList<AllocationSourceKindOption> SourceKindOptions { get; } =
    [
        new(RevenueAllocationSourceKind.FeeType, "Type de frais"),
        new(RevenueAllocationSourceKind.Withholding, "Retenue")
    ];

    public ObservableCollection<RevenueDestinationDto> Destinations { get; } = [];
    public ObservableCollection<RevenueDestinationDto> AvailableDestinationsForKey { get; } = [];
    public ObservableCollection<RevenueAllocationKeyDto> AllocationKeys { get; } = [];
    public ObservableCollection<AcademicYearDto> AcademicYears { get; } = [];
    public ObservableCollection<FeeTypeDto> FeeTypes { get; } = [];
    public ObservableCollection<WithholdingTypeDto> WithholdingTypes { get; } = [];
    public ObservableCollection<KeyDetailEditorRow> KeyDetailRows { get; } = [];

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private FeeStatusMessageKind _statusMessageKind = FeeStatusMessageKind.None;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private RevenueDestinationDto? _selectedDestination;
    [ObservableProperty] private string _destinationCode = string.Empty;
    [ObservableProperty] private string _destinationName = string.Empty;
    [ObservableProperty] private string _destinationDescription = string.Empty;
    [ObservableProperty] private bool _destinationIsActive = true;

    [ObservableProperty] private AcademicYearDto? _selectedKeyYear;
    [ObservableProperty] private AllocationSourceKindOption? _selectedSourceKind;
    [ObservableProperty] private FeeTypeDto? _selectedFeeType;
    [ObservableProperty] private WithholdingTypeDto? _selectedWithholdingType;
    [ObservableProperty] private RevenueAllocationKeyDto? _selectedKey;
    [ObservableProperty] private string _keyName = string.Empty;
    [ObservableProperty] private string _keyNotes = string.Empty;
    [ObservableProperty] private DateTime? _keyStartDate = DateTime.Today;
    [ObservableProperty] private DateTime? _keyEndDate;
    [ObservableProperty] private decimal _keyPercentageTotal;
    [ObservableProperty] private bool _isAddDestinationPickerOpen;

    [ObservableProperty] private bool _isKeysConfigurationExpanded = true;
    [ObservableProperty] private bool _isDestinationsPanelsExpanded = true;

    public string KeysConfigurationSectionHeaderText => "Configuration des clés de répartition";
    public string DestinationsSectionHeaderText => $"Destinations de répartition ({Destinations.Count})";
    public string PrincipalAccountHint =>
        "Sans clé ouverte, 100 % du montant (frais net ou retenue) est crédité au Compte principal (PRN).";

    public string KeysConfigurationToggleLabel =>
        IsKeysConfigurationExpanded
            ? "Masquer la configuration des clés"
            : "Afficher la configuration des clés";

    public string DestinationsPanelsToggleLabel =>
        IsDestinationsPanelsExpanded
            ? "Masquer les destinations"
            : "Afficher les destinations";

    public string KeyPercentageTotalDisplay =>
        string.Create(CultureInfo.CurrentCulture, $"{KeyPercentageTotal:N2} %");

    public string AllocationKeysCountLabel => $"{AllocationKeys.Count} clé(s)";

    public bool IsFeeTypeSource =>
        SelectedSourceKind?.Kind == RevenueAllocationSourceKind.FeeType;

    public bool IsWithholdingSource =>
        SelectedSourceKind?.Kind == RevenueAllocationSourceKind.Withholding;

    public string SourcePickerLabel => IsWithholdingSource ? "Type de retenue" : "Type de frais";

    public string KeyStatusLabel => SelectedKey is null
        ? (CanCreateKeyForSelection
            ? "Nouvelle répartition"
            : IsWithholdingSource
                ? "Sélectionnez un type de retenue"
                : "Sélectionnez un type de frais")
        : SelectedKey.HasAllocationHistory
            ? (SelectedKey.IsActive ? "Ouverte — déjà utilisée (historique conservé)" : $"Clôturée au {SelectedKey.EndDate:dd/MM/yyyy} — historique conservé")
            : SelectedKey.IsActive
                ? "Ouverte — jamais utilisée"
                : $"Clôturée au {SelectedKey.EndDate:dd/MM/yyyy}";

    /// <summary>Toujours modifiable : une seule clé par source / année.</summary>
    public bool CanEditKey => true;

    public bool IsKeyReadOnly => false;

    public bool CanCreateKeyForSelection =>
        SelectedKeyYear is not null
        && ((IsFeeTypeSource
                && SelectedFeeType is not null
                && !AllocationKeys.Any(k =>
                    k.AcademicYearId == SelectedKeyYear.Id
                    && k.FeeTypeId == SelectedFeeType.Id))
            || (IsWithholdingSource
                && SelectedWithholdingType is not null
                && !AllocationKeys.Any(k =>
                    k.AcademicYearId == SelectedKeyYear.Id
                    && k.WithholdingTypeId == SelectedWithholdingType.Id)));

    public bool CanDeleteSelectedKey => SelectedKey?.CanDelete == true;

    public bool HasAvailableDestinationsForKey => AvailableDestinationsForKey.Count > 0;

    public bool CanOpenAddDestinationPicker => HasAvailableDestinationsForKey;

    partial void OnIsKeysConfigurationExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(KeysConfigurationToggleLabel));

    partial void OnIsDestinationsPanelsExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(DestinationsPanelsToggleLabel));

    partial void OnKeyPercentageTotalChanged(decimal value) =>
        OnPropertyChanged(nameof(KeyPercentageTotalDisplay));

    [RelayCommand]
    private void ToggleKeysConfiguration() => IsKeysConfigurationExpanded = !IsKeysConfigurationExpanded;

    [RelayCommand]
    private void ToggleDestinationsPanels() => IsDestinationsPanelsExpanded = !IsDestinationsPanelsExpanded;

    partial void OnSelectedDestinationChanged(RevenueDestinationDto? value)
    {
        if (value is null)
        {
            DestinationCode = string.Empty;
            DestinationName = string.Empty;
            DestinationDescription = string.Empty;
            DestinationIsActive = true;
            return;
        }

        DestinationCode = value.Code;
        DestinationName = value.Name;
        DestinationDescription = value.Description ?? string.Empty;
        DestinationIsActive = value.IsActive;
    }

    partial void OnSelectedKeyChanged(RevenueAllocationKeyDto? value)
    {
        ClearKeyDetailRows();
        if (value is null)
        {
            KeyName = string.Empty;
            KeyNotes = string.Empty;
            KeyStartDate = DateTime.Today;
            KeyEndDate = null;
            KeyPercentageTotal = 0;
            RefreshAvailableDestinationsForKey();
            NotifyKeyUiState();
            return;
        }

        KeyName = value.Name;
        KeyNotes = value.Notes ?? string.Empty;
        KeyStartDate = value.StartDate.ToDateTime(TimeOnly.MinValue);
        KeyEndDate = value.EndDate?.ToDateTime(TimeOnly.MinValue);

        SelectedSourceKind = SourceKindOptions.FirstOrDefault(o => o.Kind == value.SourceKind)
            ?? SelectedSourceKind;
        if (value.SourceKind == RevenueAllocationSourceKind.Withholding)
        {
            SelectedWithholdingType = WithholdingTypes.FirstOrDefault(t => t.Id == value.WithholdingTypeId)
                ?? SelectedWithholdingType;
        }
        else
        {
            SelectedFeeType = FeeTypes.FirstOrDefault(f => f.Id == value.FeeTypeId) ?? SelectedFeeType;
        }

        foreach (var detail in value.Details.OrderBy(d => d.SortOrder))
        {
            AddKeyDetailRowInternal(new KeyDetailEditorRow
            {
                DestinationId = detail.DestinationId,
                DestinationCode = detail.DestinationCode,
                DestinationName = detail.DestinationName,
                Value = detail.Value,
                SortOrder = detail.SortOrder
            });
        }

        RecalculatePercentageTotal();
        RefreshAvailableDestinationsForKey();
        NotifyKeyUiState();
    }

    partial void OnSelectedKeyYearChanged(AcademicYearDto? value)
    {
        _ = LoadKeysThenSyncSelectionAsync();
    }

    partial void OnSelectedSourceKindChanged(AllocationSourceKindOption? value)
    {
        OnPropertyChanged(nameof(IsFeeTypeSource));
        OnPropertyChanged(nameof(IsWithholdingSource));
        OnPropertyChanged(nameof(SourcePickerLabel));
        SyncSelectionToExistingKey();
        NotifyKeyUiState();
    }

    partial void OnSelectedFeeTypeChanged(FeeTypeDto? value)
    {
        if (IsFeeTypeSource)
        {
            SyncSelectionToExistingKey();
        }
    }

    partial void OnSelectedWithholdingTypeChanged(WithholdingTypeDto? value)
    {
        if (IsWithholdingSource)
        {
            SyncSelectionToExistingKey();
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var catalogVersion = Interlocked.Increment(ref _catalogLoadVersion);
        IsBusy = true;
        SetStatus(null);
        try
        {
            AcademicYears.Clear();
            foreach (var year in await _schoolApi.GetAcademicYearsAsync())
            {
                AcademicYears.Add(year);
            }

            SelectedKeyYear ??= AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();

            var catalog = await _schoolFeeApi.GetCatalogAsync();
            if (catalogVersion != _catalogLoadVersion)
            {
                return;
            }

            FeeTypes.Clear();
            foreach (var feeType in catalog.FeeTypes
                         .Where(f => f.IsActive)
                         .GroupBy(f => f.Id)
                         .Select(g => g.First())
                         .OrderBy(f => f.Name))
            {
                FeeTypes.Add(feeType);
            }

            SelectedFeeType ??= FeeTypes.FirstOrDefault();

            WithholdingTypes.Clear();
            foreach (var type in (await _withholdingApi.GetTypesAsync(activeOnly: true))
                         .OrderBy(t => t.Name))
            {
                WithholdingTypes.Add(type);
            }

            SelectedWithholdingType ??= WithholdingTypes.FirstOrDefault();

            await LoadDestinationsAsync();
            await LoadKeysAsync();
            SyncSelectionToExistingKey();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        finally
        {
            if (catalogVersion == _catalogLoadVersion)
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private async Task LoadDestinationsAsync()
    {
        var version = Interlocked.Increment(ref _destinationsLoadVersion);
        var items = await _allocationApi.GetDestinationsAsync();
        lock (_destinationsSync)
        {
            if (version != _destinationsLoadVersion)
            {
                return;
            }

            Destinations.Clear();
            foreach (var item in items)
            {
                Destinations.Add(item);
            }
        }

        OnPropertyChanged(nameof(DestinationsSectionHeaderText));
        RefreshAvailableDestinationsForKey();
    }

    [RelayCommand]
    private void NewDestination()
    {
        SelectedDestination = null;
        DestinationCode = string.Empty;
        DestinationName = string.Empty;
        DestinationDescription = string.Empty;
        DestinationIsActive = true;
    }

    [RelayCommand]
    private async Task SaveDestinationAsync()
    {
        IsBusy = true;
        try
        {
            var request = new SaveRevenueDestinationRequest(
                DestinationCode.Trim(),
                DestinationName.Trim(),
                string.IsNullOrWhiteSpace(DestinationDescription) ? null : DestinationDescription.Trim(),
                DestinationIsActive);

            if (SelectedDestination is null)
            {
                await _allocationApi.CreateDestinationAsync(request);
                SetStatus("Destination créée.", FeeStatusMessageKind.Success);
            }
            else
            {
                await _allocationApi.UpdateDestinationAsync(SelectedDestination.Id, request);
                SetStatus("Destination mise à jour.", FeeStatusMessageKind.Success);
            }

            await LoadDestinationsAsync();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeactivateDestinationAsync()
    {
        if (SelectedDestination is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _allocationApi.DeactivateDestinationAsync(SelectedDestination.Id);
            SetStatus("Destination désactivée.", FeeStatusMessageKind.Success);
            await LoadDestinationsAsync();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadKeysAsync()
    {
        var version = Interlocked.Increment(ref _keysLoadVersion);
        var yearId = SelectedKeyYear?.Id;
        var items = await _allocationApi.GetKeysAsync(yearId);
        lock (_keysSync)
        {
            if (version != _keysLoadVersion)
            {
                return;
            }

            AllocationKeys.Clear();
            foreach (var key in items)
            {
                AllocationKeys.Add(key);
            }
        }

        OnPropertyChanged(nameof(AllocationKeysCountLabel));
        OnPropertyChanged(nameof(CanCreateKeyForSelection));
    }

    private async Task LoadKeysThenSyncSelectionAsync()
    {
        await LoadKeysAsync();
        SyncSelectionToExistingKey();
    }

    private void SyncSelectionToExistingKey()
    {
        OnPropertyChanged(nameof(CanCreateKeyForSelection));
        if (SelectedKeyYear is null)
        {
            return;
        }

        RevenueAllocationKeyDto? existing = null;
        if (IsFeeTypeSource && SelectedFeeType is not null)
        {
            existing = AllocationKeys.FirstOrDefault(k =>
                k.AcademicYearId == SelectedKeyYear.Id && k.FeeTypeId == SelectedFeeType.Id);
        }
        else if (IsWithholdingSource && SelectedWithholdingType is not null)
        {
            existing = AllocationKeys.FirstOrDefault(k =>
                k.AcademicYearId == SelectedKeyYear.Id && k.WithholdingTypeId == SelectedWithholdingType.Id);
        }

        if (existing is not null)
        {
            if (SelectedKey?.Id != existing.Id)
            {
                SelectedKey = existing;
            }

            return;
        }

        if (SelectedKey is not null && !MatchesCurrentSourceSelection(SelectedKey))
        {
            SelectedKey = null;
            var sourceName = IsWithholdingSource
                ? SelectedWithholdingType?.Name
                : SelectedFeeType?.Name;
            KeyName = string.IsNullOrWhiteSpace(sourceName) ? string.Empty : $"Répartition {sourceName}";
            KeyNotes = string.Empty;
            KeyStartDate = DateTime.Today;
            KeyEndDate = null;
            ClearKeyDetailRows();
            KeyPercentageTotal = 0;
            RefreshAvailableDestinationsForKey();
            NotifyKeyUiState();
        }
    }

    private bool MatchesCurrentSourceSelection(RevenueAllocationKeyDto key)
    {
        if (SelectedKeyYear is null || key.AcademicYearId != SelectedKeyYear.Id)
        {
            return false;
        }

        if (IsFeeTypeSource)
        {
            return SelectedFeeType is not null && key.FeeTypeId == SelectedFeeType.Id;
        }

        return SelectedWithholdingType is not null && key.WithholdingTypeId == SelectedWithholdingType.Id;
    }

    [RelayCommand]
    private void NewKey()
    {
        if (SelectedKeyYear is null)
        {
            SetStatus("Sélectionnez l'année scolaire.", FeeStatusMessageKind.Warning);
            return;
        }

        if (IsFeeTypeSource && SelectedFeeType is null)
        {
            SetStatus("Sélectionnez le type de frais.", FeeStatusMessageKind.Warning);
            return;
        }

        if (IsWithholdingSource && SelectedWithholdingType is null)
        {
            SetStatus("Sélectionnez le type de retenue.", FeeStatusMessageKind.Warning);
            return;
        }

        var existing = IsWithholdingSource
            ? AllocationKeys.FirstOrDefault(k =>
                k.AcademicYearId == SelectedKeyYear.Id && k.WithholdingTypeId == SelectedWithholdingType!.Id)
            : AllocationKeys.FirstOrDefault(k =>
                k.AcademicYearId == SelectedKeyYear.Id && k.FeeTypeId == SelectedFeeType!.Id);
        if (existing is not null)
        {
            SelectedKey = existing;
            SetStatus(
                IsWithholdingSource
                    ? "Une clé existe déjà pour cette retenue sur cette année. Vous ne pouvez que la modifier."
                    : "Une clé existe déjà pour ce type de frais sur cette année. Vous ne pouvez que la modifier.",
                FeeStatusMessageKind.Warning);
            return;
        }

        SelectedKey = null;
        var sourceName = IsWithholdingSource ? SelectedWithholdingType!.Name : SelectedFeeType!.Name;
        KeyName = $"Répartition {sourceName}";
        KeyNotes = string.Empty;
        KeyStartDate = DateTime.Today;
        KeyEndDate = null;
        ClearKeyDetailRows();
        KeyPercentageTotal = 0;
        RefreshAvailableDestinationsForKey();
        NotifyKeyUiState();
        SetStatus("Nouvelle répartition : définissez les pourcentages puis enregistrez.", FeeStatusMessageKind.Info);
    }

    [RelayCommand]
    private void ToggleAddDestinationPicker()
    {
        if (!CanOpenAddDestinationPicker)
        {
            IsAddDestinationPickerOpen = false;
            return;
        }

        if (!IsAddDestinationPickerOpen)
        {
            RefreshAvailableDestinationsForKey();
        }

        IsAddDestinationPickerOpen = !IsAddDestinationPickerOpen;
    }

    [RelayCommand]
    private void AddDestinationToKey(RevenueDestinationDto? destination)
    {
        if (!CanEditKey || destination is null)
        {
            return;
        }

        if (KeyDetailRows.Any(r => r.DestinationId == destination.Id))
        {
            SetStatus("Cette destination est déjà dans le tableau.", FeeStatusMessageKind.Warning);
            return;
        }

        AddKeyDetailRowInternal(new KeyDetailEditorRow
        {
            DestinationId = destination.Id,
            DestinationCode = destination.Code,
            DestinationName = destination.Name,
            Value = 0,
            SortOrder = KeyDetailRows.Count + 1
        });
        RecalculatePercentageTotal();
        RefreshAvailableDestinationsForKey();
        IsAddDestinationPickerOpen = false;
        SetStatus("Destination ajoutée localement. Définissez le pourcentage puis enregistrez.", FeeStatusMessageKind.Info);
    }

    [RelayCommand]
    private void RemoveKeyDetailRow(KeyDetailEditorRow? row)
    {
        if (row is null || !CanEditKey)
        {
            return;
        }

        row.PropertyChanged -= OnKeyDetailRowPropertyChanged;
        KeyDetailRows.Remove(row);
        for (var i = 0; i < KeyDetailRows.Count; i++)
        {
            KeyDetailRows[i].SortOrder = i + 1;
        }

        RecalculatePercentageTotal();
        RefreshAvailableDestinationsForKey();
    }

    [RelayCommand]
    private async Task SaveKeyAsync()
    {
        if (SelectedKeyYear is null)
        {
            SetStatus("Sélectionnez une année scolaire.", FeeStatusMessageKind.Warning);
            return;
        }

        if (IsFeeTypeSource && SelectedFeeType is null)
        {
            SetStatus("Sélectionnez le type de frais à répartir.", FeeStatusMessageKind.Warning);
            return;
        }

        if (IsWithholdingSource && SelectedWithholdingType is null)
        {
            SetStatus("Sélectionnez le type de retenue à répartir.", FeeStatusMessageKind.Warning);
            return;
        }

        if (KeyStartDate is null)
        {
            SetStatus("La date de début est obligatoire.", FeeStatusMessageKind.Warning);
            return;
        }

        if (!CanEditKey)
        {
            SetStatus("Cette répartition ne peut pas être modifiée.", FeeStatusMessageKind.Warning);
            return;
        }

        // Une seule clé par source / année : bascule en mise à jour si elle existe déjà.
        var existingForSelection = IsWithholdingSource
            ? AllocationKeys.FirstOrDefault(k =>
                k.AcademicYearId == SelectedKeyYear.Id && k.WithholdingTypeId == SelectedWithholdingType!.Id)
            : AllocationKeys.FirstOrDefault(k =>
                k.AcademicYearId == SelectedKeyYear.Id && k.FeeTypeId == SelectedFeeType!.Id);
        if (SelectedKey is null && existingForSelection is not null)
        {
            SelectedKey = existingForSelection;
        }

        IsBusy = true;
        try
        {
            var details = KeyDetailRows.Select((r, i) => new SaveRevenueAllocationKeyDetailRequest(
                r.DestinationId,
                r.Value,
                r.SortOrder > 0 ? r.SortOrder : i + 1)).ToList();

            var startDate = DateOnly.FromDateTime(KeyStartDate.Value);
            if (SelectedKey is null)
            {
                var created = await _allocationApi.CreateKeyAsync(new CreateRevenueAllocationKeyRequest(
                    SelectedKeyYear.Id,
                    IsFeeTypeSource ? SelectedFeeType!.Id : null,
                    IsWithholdingSource ? SelectedWithholdingType!.Id : null,
                    KeyName.Trim(),
                    string.IsNullOrWhiteSpace(KeyNotes) ? null : KeyNotes.Trim(),
                    startDate,
                    details));
                SetStatus(
                    IsWithholdingSource
                        ? "Répartition créée pour cette retenue (une seule autorisée par année)."
                        : "Répartition créée pour ce type de frais (une seule autorisée par année).",
                    FeeStatusMessageKind.Success);
                await LoadKeysAsync();
                SelectedKey = AllocationKeys.FirstOrDefault(k => k.Id == created.Id);
            }
            else
            {
                await _allocationApi.UpdateKeyAsync(SelectedKey.Id, new UpdateRevenueAllocationKeyRequest(
                    KeyName.Trim(),
                    string.IsNullOrWhiteSpace(KeyNotes) ? null : KeyNotes.Trim(),
                    startDate,
                    details));
                SetStatus(
                    SelectedKey.HasAllocationHistory
                        ? "Répartition mise à jour. L'historique des paiements passés est conservé."
                        : "Répartition mise à jour.",
                    FeeStatusMessageKind.Success);
                await LoadKeysAsync();
                SelectedKey = AllocationKeys.FirstOrDefault(k => k.Id == SelectedKey.Id);
            }
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteKeyAsync()
    {
        if (SelectedKey is null)
        {
            return;
        }

        if (!SelectedKey.CanDelete)
        {
            SetStatus(
                "Cette clé a déjà servi à des paiements. L'historique est conservé : suppression impossible.",
                FeeStatusMessageKind.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var id = SelectedKey.Id;
            await _allocationApi.DeleteKeyAsync(id);
            SetStatus("Répartition supprimée définitivement (jamais utilisée).", FeeStatusMessageKind.Success);
            SelectedKey = null;
            await LoadKeysAsync();
            SyncSelectionToExistingKey();
            if (SelectedKey is null
                && ((IsFeeTypeSource && SelectedFeeType is not null)
                    || (IsWithholdingSource && SelectedWithholdingType is not null)))
            {
                var sourceName = IsWithholdingSource
                    ? SelectedWithholdingType!.Name
                    : SelectedFeeType!.Name;
                KeyName = $"Répartition {sourceName}";
                KeyNotes = string.Empty;
                KeyStartDate = DateTime.Today;
                ClearKeyDetailRows();
                RecalculatePercentageTotal();
                RefreshAvailableDestinationsForKey();
            }

            NotifyKeyUiState();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CloseKeyAsync()
    {
        if (SelectedKey is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _allocationApi.CloseKeyAsync(SelectedKey.Id);
            SetStatus("Répartition clôturée. L'historique éventuel reste intact.", FeeStatusMessageKind.Success);
            await LoadKeysAsync();
            SelectedKey = AllocationKeys.FirstOrDefault(k => k.Id == SelectedKey.Id);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RecalculatePercentageTotal() =>
        KeyPercentageTotal = KeyDetailRows.Sum(r => r.Value);

    private void RefreshAvailableDestinationsForKey()
    {
        AvailableDestinationsForKey.Clear();
        var used = KeyDetailRows.Select(r => r.DestinationId).ToHashSet();
        foreach (var destination in Destinations.Where(d => d.IsActive && !used.Contains(d.Id)).OrderBy(d => d.Name))
        {
            AvailableDestinationsForKey.Add(destination);
        }

        OnPropertyChanged(nameof(HasAvailableDestinationsForKey));
        OnPropertyChanged(nameof(CanOpenAddDestinationPicker));
        if (!HasAvailableDestinationsForKey)
        {
            IsAddDestinationPickerOpen = false;
        }
    }

    private void AddKeyDetailRowInternal(KeyDetailEditorRow row)
    {
        row.PropertyChanged += OnKeyDetailRowPropertyChanged;
        KeyDetailRows.Add(row);
    }

    private void ClearKeyDetailRows()
    {
        foreach (var row in KeyDetailRows)
        {
            row.PropertyChanged -= OnKeyDetailRowPropertyChanged;
        }

        KeyDetailRows.Clear();
    }

    private void OnKeyDetailRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(KeyDetailEditorRow.Value))
        {
            RecalculatePercentageTotal();
        }
    }

    private void NotifyKeyUiState()
    {
        OnPropertyChanged(nameof(KeyStatusLabel));
        OnPropertyChanged(nameof(CanEditKey));
        OnPropertyChanged(nameof(IsKeyReadOnly));
        OnPropertyChanged(nameof(CanCreateKeyForSelection));
        OnPropertyChanged(nameof(CanDeleteSelectedKey));
        OnPropertyChanged(nameof(CanOpenAddDestinationPicker));
        OnPropertyChanged(nameof(IsFeeTypeSource));
        OnPropertyChanged(nameof(IsWithholdingSource));
        OnPropertyChanged(nameof(SourcePickerLabel));
    }

    private void SetStatus(string? message, FeeStatusMessageKind kind = FeeStatusMessageKind.Info)
    {
        StatusMessage = message;
        StatusMessageKind = string.IsNullOrWhiteSpace(message) ? FeeStatusMessageKind.None : kind;
    }
}

public sealed record AllocationSourceKindOption(RevenueAllocationSourceKind Kind, string Label);
