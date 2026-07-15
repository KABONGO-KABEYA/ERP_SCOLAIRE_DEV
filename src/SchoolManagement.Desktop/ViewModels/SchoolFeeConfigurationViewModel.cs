using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.SchoolFees;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class SchoolFeeConfigurationViewModel : ViewModelBase
{
    private readonly ISchoolFeeApiService _schoolFeeApi;
    private readonly ISchoolApiService _schoolApiService;
    private readonly IEnrollmentWizardApiService _wizardApiService;
    private readonly List<PedagogicalClassFilterItem> _allPedagogicalClasses = [];
    private readonly Dictionary<Guid, ClassScheduleSignatureInfo> _scheduleSignatures = new();
    private readonly List<FeeInstallmentDto> _feeTypeInstallmentPool = [];
    private bool _isRefreshingClassSelection;
    private bool _isEnforcingDueDates;

    private sealed record ClassScheduleSignatureInfo(string Signature, bool IsConfigured);

    public SchoolFeeConfigurationViewModel(
        ISchoolFeeApiService schoolFeeApi,
        ISchoolApiService schoolApiService,
        IEnrollmentWizardApiService wizardApiService)
    {
        _schoolFeeApi = schoolFeeApi;
        _schoolApiService = schoolApiService;
        _wizardApiService = wizardApiService;
        Currencies = [Currency.CDF, Currency.USD];
    }

    public ObservableCollection<AcademicYearDto> AcademicYears { get; } = [];
    public ObservableCollection<SectionDto> Sections { get; } = [];
    public ObservableCollection<ClassSelectionItemViewModel> FilteredClasses { get; } = [];
    public ObservableCollection<ClassSelectionItemViewModel> VisibleClasses { get; } = [];
    public ObservableCollection<FeeTypeDto> FeeTypes { get; } = [];
    public ObservableCollection<FeeTypeDto> CatalogFeeTypes { get; } = [];
    public ObservableCollection<FeeInstallmentDto> Installments { get; } = [];
    public ObservableCollection<FeeInstallmentDto> CatalogInstallments { get; } = [];
    public ObservableCollection<FeePricingCategoryDto> PricingCategories { get; } = [];
    public ObservableCollection<FeePricingCategoryDto> CatalogPricingCategories { get; } = [];
    public ObservableCollection<ScheduleLineViewModel> ScheduleLines { get; } = [];
    public ObservableCollection<FeeTypeInstallmentItemViewModel> FeeTypeInstallmentItems { get; } = [];
    public ObservableCollection<FeeInstallmentDto> AvailableInstallmentsForAssignment { get; } = [];
    public ObservableCollection<FeeInstallmentDto> AvailableInstallmentsForConfiguration { get; } = [];

    public IReadOnlyList<Currency> Currencies { get; }

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private FeeStatusMessageKind _statusMessageKind = FeeStatusMessageKind.None;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _classSearchText = string.Empty;
    [ObservableProperty] private AcademicYearDto? _selectedAcademicYear;
    [ObservableProperty] private SectionDto? _selectedSection;
    [ObservableProperty] private FeeTypeDto? _selectedFeeType;
    [ObservableProperty] private FeePricingCategoryDto? _selectedPricingCategory;
    [ObservableProperty] private decimal _annualTotal;
    [ObservableProperty] private Currency _displayCurrency = Currency.CDF;
    [ObservableProperty] private bool _isScheduleEditable = true;
    [ObservableProperty] private string? _readOnlyNotice;

    public bool IsScheduleReadOnly => !IsScheduleEditable;

    [ObservableProperty] private string _feeTypeCode = string.Empty;
    [ObservableProperty] private string _feeTypeName = string.Empty;
    [ObservableProperty] private Currency _feeTypeCurrency = Currency.CDF;
    [ObservableProperty] private bool _feeTypeIsMandatory = true;
    [ObservableProperty] private bool _feeTypeIsActive = true;
    [ObservableProperty] private bool _isEditingFeeType;
    [ObservableProperty] private FeeTypeDto? _selectedCatalogFeeType;

    [ObservableProperty] private string _pricingCategoryCode = string.Empty;
    [ObservableProperty] private string _pricingCategoryName = string.Empty;
    [ObservableProperty] private string _pricingCategoryDescription = string.Empty;
    [ObservableProperty] private bool _pricingCategoryIsActive = true;
    [ObservableProperty] private bool _isEditingPricingCategory;
    [ObservableProperty] private FeePricingCategoryDto? _selectedCatalogPricingCategory;

    [ObservableProperty] private string _installmentName = string.Empty;
    [ObservableProperty] private int _installmentSortOrder;
    [ObservableProperty] private bool _installmentIsActive = true;
    [ObservableProperty] private FeeInstallmentDto? _selectedInstallment;
    [ObservableProperty] private FeeInstallmentDto? _installmentToAssign;
    [ObservableProperty] private FeeInstallmentDto? _installmentToAssignForConfiguration;
    [ObservableProperty] private ScheduleLineViewModel? _selectedScheduleLine;
    [ObservableProperty] private bool _isAddInstallmentPickerOpen;

    public bool HasAvailableInstallmentsForConfiguration => AvailableInstallmentsForConfiguration.Count > 0;

    public bool CanOpenAddInstallmentPicker =>
        IsScheduleEditable
        && SelectedClassCount > 0
        && HasAvailableInstallmentsForConfiguration;

    [ObservableProperty] private bool _isTariffConfigurationExpanded = true;
    [ObservableProperty] private bool _isFeeTypePanelsExpanded = true;
    [ObservableProperty] private bool _isPricingCategoryPanelsExpanded = true;
    [ObservableProperty] private bool _isInstallmentPanelsExpanded = true;

    public string TariffConfigurationSectionHeaderText => "Configuration des tarifs";
    public string FeeTypeSectionHeaderText => $"Types de frais ({CatalogFeeTypes.Count})";
    public string PricingCategorySectionHeaderText => $"Catégories tarifaires ({CatalogPricingCategories.Count})";
    public string InstallmentSectionHeaderText => $"Tranches ({CatalogInstallments.Count})";

    public string TariffConfigurationToggleLabel => IsTariffConfigurationExpanded ? "Masquer la configuration des tarifs" : "Afficher la configuration des tarifs";
    public string FeeTypePanelsToggleLabel => IsFeeTypePanelsExpanded ? "Masquer les types de frais" : "Afficher les types de frais";
    public string PricingCategoryPanelsToggleLabel => IsPricingCategoryPanelsExpanded ? "Masquer les catégories tarifaires" : "Afficher les catégories tarifaires";
    public string InstallmentPanelsToggleLabel => IsInstallmentPanelsExpanded ? "Masquer les tranches" : "Afficher les tranches";

    partial void OnIsTariffConfigurationExpandedChanged(bool value) => OnPropertyChanged(nameof(TariffConfigurationToggleLabel));
    partial void OnIsFeeTypePanelsExpandedChanged(bool value) => OnPropertyChanged(nameof(FeeTypePanelsToggleLabel));
    partial void OnIsPricingCategoryPanelsExpandedChanged(bool value) => OnPropertyChanged(nameof(PricingCategoryPanelsToggleLabel));
    partial void OnIsInstallmentPanelsExpandedChanged(bool value) => OnPropertyChanged(nameof(InstallmentPanelsToggleLabel));

    [RelayCommand]
    private void ToggleTariffConfiguration() => IsTariffConfigurationExpanded = !IsTariffConfigurationExpanded;

    [RelayCommand]
    private void ToggleFeeTypePanels() => IsFeeTypePanelsExpanded = !IsFeeTypePanelsExpanded;

    [RelayCommand]
    private void TogglePricingCategoryPanels() => IsPricingCategoryPanelsExpanded = !IsPricingCategoryPanelsExpanded;

    [RelayCommand]
    private void ToggleInstallmentPanels() => IsInstallmentPanelsExpanded = !IsInstallmentPanelsExpanded;

    public int SelectedClassCount => FilteredClasses.Count(c => c.IsSelected);

    public int InstallmentCount => ScheduleLines.Count;

    public string AnnualTotalDisplay => AnnualTotal.ToString("N2");

    public string AnnualTotalWithCurrencyDisplay => $"{AnnualTotalDisplay} {CurrencyDisplay}";

    public string CurrencyDisplay => DisplayCurrency.ToString();

    public string SelectedClassesSummary => SelectedClassCount switch
    {
        0 => "Aucune classe sélectionnée",
        1 => FilteredClasses.First(c => c.IsSelected).DisplayName,
        _ => $"{SelectedClassCount} classes sélectionnées"
    };

    partial void OnSelectedAcademicYearChanged(AcademicYearDto? value)
    {
        UpdateScheduleEditability();
        _ = RefreshScheduleSignaturesAndLoadAsync();
    }

    partial void OnSelectedSectionChanged(SectionDto? value)
    {
        RefreshFilteredClasses();
        UpdateClassSelectionCompatibility();
    }

    partial void OnSelectedFeeTypeChanged(FeeTypeDto? value)
    {
        if (value is not null)
        {
            DisplayCurrency = value.Currency;
            OnPropertyChanged(nameof(CurrencyDisplay));
        }

        SelectedScheduleLine = null;
        _ = RefreshScheduleSignaturesAndLoadAsync();
    }

    partial void OnSelectedPricingCategoryChanged(FeePricingCategoryDto? value)
    {
        SelectedScheduleLine = null;
        _ = RefreshScheduleSignaturesAndLoadAsync();
    }

    partial void OnIsScheduleEditableChanged(bool value)
    {
        OnPropertyChanged(nameof(CanOpenAddInstallmentPicker));
        if (!value)
        {
            IsAddInstallmentPickerOpen = false;
        }
    }

    partial void OnClassSearchTextChanged(string value) => RefreshVisibleClasses();

    partial void OnAnnualTotalChanged(decimal value)
    {
        OnPropertyChanged(nameof(AnnualTotalDisplay));
        OnPropertyChanged(nameof(AnnualTotalWithCurrencyDisplay));
    }

    partial void OnDisplayCurrencyChanged(Currency value)
    {
        OnPropertyChanged(nameof(CurrencyDisplay));
        OnPropertyChanged(nameof(AnnualTotalWithCurrencyDisplay));
    }

    partial void OnSelectedCatalogFeeTypeChanged(FeeTypeDto? value)
    {
        if (value is null)
        {
            ClearFeeTypeForm();
            return;
        }

        IsEditingFeeType = true;
        FeeTypeCode = value.Code;
        FeeTypeName = value.Name;
        FeeTypeCurrency = value.Currency;
        FeeTypeIsMandatory = value.IsMandatory;
        FeeTypeIsActive = value.IsActive;
        _ = LoadFeeTypeInstallmentsAsync();
    }

    partial void OnSelectedCatalogPricingCategoryChanged(FeePricingCategoryDto? value)
    {
        if (value is null)
        {
            ClearPricingCategoryForm();
            return;
        }

        IsEditingPricingCategory = true;
        PricingCategoryCode = value.Code;
        PricingCategoryName = value.Name;
        PricingCategoryDescription = value.Description ?? string.Empty;
        PricingCategoryIsActive = value.IsActive;
    }

    partial void OnSelectedInstallmentChanged(FeeInstallmentDto? value)
    {
        if (value is null)
        {
            ClearInstallmentForm();
            return;
        }

        InstallmentName = value.Name;
        InstallmentSortOrder = value.SortOrder;
        InstallmentIsActive = value.IsActive;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await ReloadCatalogAsync(reloadSchedule: false);

            AcademicYears.Clear();
            var years = await _schoolApiService.GetAcademicYearsAsync();
            foreach (var year in years
                         .GroupBy(y => y.Id)
                         .Select(g => g.First())
                         .OrderByDescending(y => y.StartDate))
            {
                AcademicYears.Add(year);
            }

            SelectedAcademicYear ??= AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();

            _allPedagogicalClasses.Clear();
            var structure = await _wizardApiService.GetStructureOptionsAsync();
            Sections.Clear();
            foreach (var section in structure.Sections.OrderBy(s => s.Name))
            {
                Sections.Add(section);
            }

            var seenClassIds = new HashSet<Guid>();
            var pedagogicalClasses = await _schoolApiService.GetPedagogicalClassesAsync(enabledOnly: true);
            foreach (var cls in pedagogicalClasses
                         .Where(c => c.IsEnabled)
                         .GroupBy(c => c.Id)
                         .Select(g => g.First())
                         .OrderBy(c => c.DisplayName))
            {
                if (!seenClassIds.Add(cls.Id))
                {
                    continue;
                }

                var sectionId = ResolveSectionIdForProgram(cls.Program, Sections);
                if (sectionId == Guid.Empty)
                {
                    continue;
                }

                _allPedagogicalClasses.Add(new PedagogicalClassFilterItem(
                    cls.Id,
                    cls.DisplayName,
                    sectionId,
                    cls.StudyOption,
                    cls.Program));
            }

            RefreshFilteredClasses(selectFirst: true);
            SelectedPricingCategory ??= PricingCategories.FirstOrDefault();
            SelectedFeeType ??= FeeTypes.FirstOrDefault();
            UpdateScheduleEditability();
            await RefreshScheduleSignaturesAndLoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadScheduleAsync()
    {
        ScheduleLines.Clear();
        AnnualTotal = 0;
        OnPropertyChanged(nameof(InstallmentCount));

        if (SelectedAcademicYear is null || SelectedFeeType is null || SelectedPricingCategory is null)
        {
            return;
        }

        var selectedClassIds = FilteredClasses.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        if (selectedClassIds.Count == 0)
        {
            await LoadScheduleStructureAsync();
            StatusMessage = "Sélectionnez au moins une classe.";
            return;
        }

        try
        {
            var schedule = await _schoolFeeApi.GetScheduleAsync(
                SelectedAcademicYear.Id,
                selectedClassIds[0],
                SelectedPricingCategory.Id,
                SelectedFeeType.Id);

            DisplayCurrency = schedule.Currency;
            BuildScheduleLines(schedule.Lines);
            AnnualTotal = schedule.AnnualTotal;
            OnPropertyChanged(nameof(InstallmentCount));
            RefreshAvailableInstallmentsForConfiguration();
            if (IsScheduleEditable && string.IsNullOrWhiteSpace(StatusMessage))
            {
                StatusMessage = null;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (!IsScheduleEditable)
        {
            StatusMessage = ReadOnlyNotice;
            return;
        }

        if (SelectedAcademicYear is null || SelectedFeeType is null || SelectedPricingCategory is null)
        {
            StatusMessage = "Sélectionnez l'année, la catégorie et le type de frais.";
            return;
        }

        var selectedClassIds = FilteredClasses.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        if (selectedClassIds.Count == 0)
        {
            StatusMessage = "Sélectionnez au moins une classe.";
            return;
        }

        IsBusy = true;
        try
        {
            await EnsureFeeTypeInstallmentsIncludeScheduleAsync();

            var lines = ScheduleLines.Select(l => new SaveClassFeeScheduleLineRequest(
                l.FeeInstallmentId,
                l.SortOrder,
                l.Amount,
                l.DueDate)).ToList();

            if (selectedClassIds.Count == 1)
            {
                var schedule = await _schoolFeeApi.SaveScheduleAsync(new SaveClassFeeScheduleRequest(
                    SelectedAcademicYear.Id,
                    selectedClassIds[0],
                    SelectedPricingCategory.Id,
                    SelectedFeeType.Id,
                    lines));

                AnnualTotal = schedule.AnnualTotal;
                SetStatus($"Tarifs enregistrés pour {schedule.PedagogicalClassName} — {schedule.FeeTypeName} ({schedule.AcademicYearLabel}).", FeeStatusMessageKind.Success);
            }
            else
            {
                var result = await _schoolFeeApi.SaveScheduleBulkAsync(new SaveClassFeeScheduleBulkRequest(
                    SelectedAcademicYear.Id,
                    selectedClassIds,
                    SelectedPricingCategory.Id,
                    SelectedFeeType.Id,
                    lines));

                AnnualTotal = ScheduleLines.Sum(l => l.Amount);
                SetStatus($"Tarifs enregistrés pour {result.SavedClassCount} classe(s) — {SelectedFeeType.Name} ({SelectedAcademicYear.Label}).", FeeStatusMessageKind.Success);
            }

            await RefreshScheduleSignaturesAsync();
            EnforceCompatibleClassSelection();
            UpdateClassSelectionCompatibility();
            await LoadScheduleAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ResetSchedule()
    {
        if (!IsScheduleEditable)
        {
            SetStatus(ReadOnlyNotice, FeeStatusMessageKind.Warning);
            return;
        }

        foreach (var line in ScheduleLines)
        {
            line.Amount = 0;
            line.DueDate = null;
        }

        RecalculateTotal();
        SetStatus("Montants réinitialisés localement. Cliquez sur Enregistrer pour appliquer.", FeeStatusMessageKind.Info);
    }

    [RelayCommand]
    private async Task CopyFromPreviousYearAsync()
    {
        if (!IsScheduleEditable)
        {
            StatusMessage = ReadOnlyNotice;
            return;
        }

        if (SelectedAcademicYear is null || SelectedFeeType is null || SelectedPricingCategory is null)
        {
            StatusMessage = "Sélectionnez l'année, la catégorie et le type de frais.";
            return;
        }

        var selectedClassIds = FilteredClasses.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        if (selectedClassIds.Count == 0)
        {
            StatusMessage = "Sélectionnez au moins une classe.";
            return;
        }

        IsBusy = true;
        try
        {
            if (selectedClassIds.Count == 1)
            {
                var result = await _schoolFeeApi.CopyScheduleFromPreviousAsync(new CopyClassFeeScheduleRequest(
                    SelectedAcademicYear.Id,
                    selectedClassIds[0],
                    SelectedPricingCategory.Id,
                    SelectedFeeType.Id));

                StatusMessage = $"{result.CopiedCount} montant(s) reporté(s) depuis {result.SourceYearLabel}.";
            }
            else
            {
                var result = await _schoolFeeApi.CopyScheduleFromPreviousBulkAsync(new CopyClassFeeScheduleBulkRequest(
                    SelectedAcademicYear.Id,
                    selectedClassIds,
                    SelectedPricingCategory.Id,
                    SelectedFeeType.Id));

                StatusMessage = $"{result.CopiedCount} montant(s) reporté(s) pour {result.ClassCount} classe(s) depuis {result.SourceYearLabel}.";
            }

            await RefreshScheduleSignaturesAndLoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAllClasses()
    {
        if (FilteredClasses.Count == 0)
        {
            return;
        }

        var anchorItem = FilteredClasses.FirstOrDefault(c => c.IsSelected) ?? FilteredClasses[0];
        var anchorSignature = GetClassSignature(anchorItem.Id);

        _isRefreshingClassSelection = true;
        var selectedCount = 0;
        foreach (var item in FilteredClasses)
        {
            var isCompatible = string.Equals(GetClassSignature(item.Id), anchorSignature, StringComparison.Ordinal);
            item.IsSelected = isCompatible;
            if (isCompatible)
            {
                selectedCount++;
            }
        }

        _isRefreshingClassSelection = false;
        UpdateClassSelectionCompatibility();
        NotifyClassSelectionChanged();

        if (selectedCount < FilteredClasses.Count)
        {
            SetStatus(
                "Seules les classes partageant la même configuration tarifaire ont été sélectionnées.",
                FeeStatusMessageKind.Info);
        }
        else
        {
            StatusMessage = null;
            StatusMessageKind = FeeStatusMessageKind.None;
        }

        _ = LoadScheduleAsync();
    }

    [RelayCommand]
    private void ClearClassSelection()
    {
        _isRefreshingClassSelection = true;
        foreach (var item in FilteredClasses)
        {
            item.IsSelected = false;
        }

        _isRefreshingClassSelection = false;
        NotifyClassSelectionChanged();
        ScheduleLines.Clear();
        AnnualTotal = 0;
        OnPropertyChanged(nameof(InstallmentCount));
    }

    [RelayCommand]
    private void NewFeeType()
    {
        SelectedCatalogFeeType = null;
        ClearFeeTypeForm();
        IsEditingFeeType = false;
        FeeTypeIsActive = true;
        FeeTypeIsMandatory = true;
        FeeTypeInstallmentItems.Clear();
        RefreshAvailableInstallmentsForAssignment();
    }

    [RelayCommand]
    private async Task SaveFeeTypeAsync()
    {
        if (string.IsNullOrWhiteSpace(FeeTypeName))
        {
            StatusMessage = "Le libellé du type de frais est obligatoire.";
            return;
        }

        IsBusy = true;
        try
        {
            if (SelectedCatalogFeeType is null)
            {
                var created = await _schoolFeeApi.CreateFeeTypeAsync(new CreateFeeTypeRequest(
                    FeeTypeName.Trim(),
                    FeeTypeCurrency,
                    FeeTypeIsMandatory,
                    FeeTypeIsActive));

                FeeTypeCode = created.Code;
                IsEditingFeeType = true;
                SelectedCatalogFeeType = created;
                StatusMessage = $"Type de frais créé (code {created.Code}).";
            }
            else
            {
                await _schoolFeeApi.UpdateFeeTypeAsync(SelectedCatalogFeeType.Id, new UpdateFeeTypeRequest(
                    FeeTypeName.Trim(),
                    FeeTypeCurrency,
                    FeeTypeIsMandatory,
                    FeeTypeIsActive));

                StatusMessage = "Type de frais mis à jour.";
            }

            await ReloadCatalogAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteFeeTypeAsync()
    {
        if (SelectedCatalogFeeType is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _schoolFeeApi.DeleteFeeTypeAsync(SelectedCatalogFeeType.Id);
            SelectedCatalogFeeType = null;
            ClearFeeTypeForm();
            FeeTypeInstallmentItems.Clear();
            RefreshAvailableInstallmentsForAssignment();
            StatusMessage = "Type de frais désactivé.";
            await ReloadCatalogAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewPricingCategory()
    {
        SelectedCatalogPricingCategory = null;
        ClearPricingCategoryForm();
        IsEditingPricingCategory = false;
        PricingCategoryIsActive = true;
    }

    [RelayCommand]
    private async Task SavePricingCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(PricingCategoryName))
        {
            StatusMessage = "Le libellé de la catégorie tarifaire est obligatoire.";
            return;
        }

        IsBusy = true;
        try
        {
            if (SelectedCatalogPricingCategory is null)
            {
                var created = await _schoolFeeApi.CreatePricingCategoryAsync(new CreateFeePricingCategoryRequest(
                    PricingCategoryName.Trim(),
                    string.IsNullOrWhiteSpace(PricingCategoryDescription) ? null : PricingCategoryDescription.Trim(),
                    PricingCategoryIsActive));

                PricingCategoryCode = created.Code;
                IsEditingPricingCategory = true;
                SelectedCatalogPricingCategory = created;
                StatusMessage = $"Catégorie tarifaire créée (code {created.Code}).";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(PricingCategoryCode))
                {
                    StatusMessage = "Le code de la catégorie tarifaire est obligatoire.";
                    return;
                }

                await _schoolFeeApi.UpdatePricingCategoryAsync(SelectedCatalogPricingCategory.Id, new UpdateFeePricingCategoryRequest(
                    PricingCategoryCode.Trim(),
                    PricingCategoryName.Trim(),
                    string.IsNullOrWhiteSpace(PricingCategoryDescription) ? null : PricingCategoryDescription.Trim(),
                    PricingCategoryIsActive));

                StatusMessage = "Catégorie tarifaire mise à jour.";
            }

            await ReloadCatalogAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeletePricingCategoryAsync()
    {
        if (SelectedCatalogPricingCategory is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _schoolFeeApi.DeletePricingCategoryAsync(SelectedCatalogPricingCategory.Id);
            SelectedCatalogPricingCategory = null;
            ClearPricingCategoryForm();
            StatusMessage = "Catégorie tarifaire désactivée.";
            await ReloadCatalogAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddFeeTypeInstallment()
    {
        if (InstallmentToAssign is null || SelectedCatalogFeeType is null)
        {
            return;
        }

        if (FeeTypeInstallmentItems.Any(i => i.FeeInstallmentId == InstallmentToAssign.Id))
        {
            StatusMessage = "Cette tranche est déjà affectée à ce type de frais.";
            return;
        }

        var nextOrder = FeeTypeInstallmentItems.Count == 0
            ? 1
            : FeeTypeInstallmentItems.Max(i => i.SortOrder) + 1;
        FeeTypeInstallmentItems.Add(new FeeTypeInstallmentItemViewModel(
            InstallmentToAssign.Id,
            InstallmentToAssign.Name,
            nextOrder));
        InstallmentToAssign = null;
        RefreshAvailableInstallmentsForAssignment();
        StatusMessage = null;
    }

    [RelayCommand]
    private void RemoveFeeTypeInstallment(FeeTypeInstallmentItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        FeeTypeInstallmentItems.Remove(item);
        RenumberFeeTypeInstallments();
        RefreshAvailableInstallmentsForAssignment();
    }

    [RelayCommand]
    private void MoveFeeTypeInstallmentUp(FeeTypeInstallmentItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var ordered = FeeTypeInstallmentItems.OrderBy(i => i.SortOrder).ToList();
        var index = ordered.IndexOf(item);
        if (index <= 0)
        {
            return;
        }

        (ordered[index - 1].SortOrder, ordered[index].SortOrder) = (ordered[index].SortOrder, ordered[index - 1].SortOrder);
        RebindFeeTypeInstallmentItems(ordered.OrderBy(i => i.SortOrder));
    }

    [RelayCommand]
    private void MoveFeeTypeInstallmentDown(FeeTypeInstallmentItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var ordered = FeeTypeInstallmentItems.OrderBy(i => i.SortOrder).ToList();
        var index = ordered.IndexOf(item);
        if (index < 0 || index >= ordered.Count - 1)
        {
            return;
        }

        (ordered[index + 1].SortOrder, ordered[index].SortOrder) = (ordered[index].SortOrder, ordered[index + 1].SortOrder);
        RebindFeeTypeInstallmentItems(ordered.OrderBy(i => i.SortOrder));
    }

    [RelayCommand]
    private async Task SaveFeeTypeInstallmentsAsync()
    {
        if (SelectedCatalogFeeType is null)
        {
            StatusMessage = "Sélectionnez un type de frais.";
            return;
        }

        IsBusy = true;
        try
        {
            await SaveInstallmentsForFeeTypeAsync(SelectedCatalogFeeType.Id, FeeTypeInstallmentItems);

            StatusMessage = "Tranches affectées au type de frais.";
            await LoadFeeTypeInstallmentsAsync(reloadSchedule: SelectedFeeType?.Id == SelectedCatalogFeeType.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleAddInstallmentPicker()
    {
        if (!CanOpenAddInstallmentPicker)
        {
            IsAddInstallmentPickerOpen = false;
            return;
        }

        if (!IsAddInstallmentPickerOpen)
        {
            RefreshAvailableInstallmentsForConfiguration();
        }

        IsAddInstallmentPickerOpen = !IsAddInstallmentPickerOpen;
    }

    [RelayCommand]
    private void AddScheduleInstallment(FeeInstallmentDto? installment)
    {
        var target = installment ?? InstallmentToAssignForConfiguration;
        if (target is null || SelectedFeeType is null)
        {
            return;
        }

        if (ScheduleLines.Any(l => l.FeeInstallmentId == target.Id))
        {
            StatusMessage = "Cette tranche est déjà dans le tableau.";
            return;
        }

        var row = new ScheduleLineViewModel(
            target.Id,
            target.Name,
            ScheduleLines.Count + 1,
            0,
            null);
        row.PropertyChanged += OnScheduleLinePropertyChanged;
        ScheduleLines.Add(row);
        RenumberScheduleLines();
        InstallmentToAssignForConfiguration = null;
        IsAddInstallmentPickerOpen = false;
        RefreshAvailableInstallmentsForConfiguration();
        OnPropertyChanged(nameof(InstallmentCount));
        SetStatus("Tranche ajoutée localement. Cliquez sur Enregistrer pour appliquer à la ou aux classes sélectionnées.", FeeStatusMessageKind.Info);
    }

    [RelayCommand]
    private void RemoveScheduleInstallment(ScheduleLineViewModel? line)
    {
        var removed = line ?? SelectedScheduleLine;
        if (removed is null || SelectedFeeType is null)
        {
            return;
        }

        ScheduleLines.Remove(removed);
        if (ReferenceEquals(SelectedScheduleLine, removed))
        {
            SelectedScheduleLine = null;
        }

        RenumberScheduleLines();
        RefreshAvailableInstallmentsForConfiguration();
        OnPropertyChanged(nameof(InstallmentCount));
        RecalculateTotal();
        StatusMessage = "Tranche retirée localement. Cliquez sur Enregistrer pour appliquer à la ou aux classes sélectionnées.";
        StatusMessageKind = FeeStatusMessageKind.Info;
    }

    [RelayCommand]
    private void NewInstallment()
    {
        SelectedInstallment = null;
        ClearInstallmentForm();
        InstallmentIsActive = true;
        InstallmentSortOrder = Installments.Count + 1;
    }

    [RelayCommand]
    private async Task SaveInstallmentAsync()
    {
        if (string.IsNullOrWhiteSpace(InstallmentName))
        {
            StatusMessage = "Le libellé de la tranche est obligatoire.";
            return;
        }

        IsBusy = true;
        try
        {
            var request = new SaveFeeInstallmentRequest(
                InstallmentName.Trim(),
                InstallmentSortOrder,
                InstallmentIsActive);

            if (SelectedInstallment is null)
            {
                await _schoolFeeApi.CreateInstallmentAsync(request);
                StatusMessage = "Tranche créée.";
            }
            else
            {
                await _schoolFeeApi.UpdateInstallmentAsync(SelectedInstallment.Id, request);
                StatusMessage = "Tranche mise à jour.";
            }

            await ReloadCatalogAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteInstallmentAsync()
    {
        if (SelectedInstallment is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _schoolFeeApi.DeleteInstallmentAsync(SelectedInstallment.Id);
            SelectedInstallment = null;
            ClearInstallmentForm();
            StatusMessage = "Tranche désactivée.";
            await ReloadCatalogAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal void OnClassSelectionChanged(ClassSelectionItemViewModel? changed = null)
    {
        if (_isRefreshingClassSelection)
        {
            return;
        }

        if (changed is { IsSelected: true })
        {
            var otherSelected = FilteredClasses.Where(c => c.IsSelected && c.Id != changed.Id).ToList();
            if (otherSelected.Count > 0)
            {
                var anchorSignature = GetClassSignature(otherSelected[0].Id);
                if (!string.Equals(GetClassSignature(changed.Id), anchorSignature, StringComparison.Ordinal))
                {
                    _isRefreshingClassSelection = true;
                    changed.IsSelected = false;
                    _isRefreshingClassSelection = false;
                    SetStatus(
                        "Cette classe a une configuration tarifaire différente. Sélectionnez-la seule ou regroupez uniquement des classes identiques.",
                        FeeStatusMessageKind.Warning);
                    UpdateClassSelectionCompatibility();
                    return;
                }
            }
        }

        NotifyClassSelectionChanged();
        UpdateClassSelectionCompatibility();
        _ = LoadScheduleAsync();
    }

    private async Task RefreshScheduleSignaturesAndLoadAsync()
    {
        await LoadFeeTypeInstallmentPoolAsync();
        await RefreshScheduleSignaturesAsync();
        EnforceCompatibleClassSelection();
        UpdateClassSelectionCompatibility();
        await LoadScheduleAsync();
    }

    private async Task LoadFeeTypeInstallmentPoolAsync()
    {
        _feeTypeInstallmentPool.Clear();

        if (SelectedFeeType is null)
        {
            return;
        }

        try
        {
            var items = await _schoolFeeApi.GetFeeTypeInstallmentsAsync(SelectedFeeType.Id);
            foreach (var item in items.OrderBy(i => i.SortOrder))
            {
                var installment = CatalogInstallments.FirstOrDefault(i => i.Id == item.FeeInstallmentId);
                if (installment is not null)
                {
                    _feeTypeInstallmentPool.Add(installment);
                    continue;
                }

                _feeTypeInstallmentPool.Add(new FeeInstallmentDto(
                    item.FeeInstallmentId,
                    item.InstallmentName,
                    item.SortOrder,
                    true));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RefreshScheduleSignaturesAsync()
    {
        _scheduleSignatures.Clear();

        if (SelectedAcademicYear is null || SelectedFeeType is null || SelectedPricingCategory is null)
        {
            return;
        }

        try
        {
            var signatures = await _schoolFeeApi.GetScheduleSignaturesAsync(
                SelectedAcademicYear.Id,
                SelectedPricingCategory.Id,
                SelectedFeeType.Id);

            foreach (var item in signatures)
            {
                _scheduleSignatures[item.PedagogicalClassId] = new ClassScheduleSignatureInfo(
                    item.Signature,
                    item.IsConfigured);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private string GetClassSignature(Guid classId) =>
        _scheduleSignatures.TryGetValue(classId, out var info) ? info.Signature : string.Empty;

    private void EnforceCompatibleClassSelection()
    {
        var selected = FilteredClasses.Where(c => c.IsSelected).ToList();
        if (selected.Count <= 1)
        {
            return;
        }

        var anchorSignature = GetClassSignature(selected[0].Id);
        _isRefreshingClassSelection = true;
        foreach (var item in selected.Skip(1))
        {
            if (!string.Equals(GetClassSignature(item.Id), anchorSignature, StringComparison.Ordinal))
            {
                item.IsSelected = false;
            }
        }

        _isRefreshingClassSelection = false;
    }

    private void UpdateClassSelectionCompatibility()
    {
        var selected = FilteredClasses.Where(c => c.IsSelected).ToList();
        var anchorSignature = selected.Count > 0 ? GetClassSignature(selected[0].Id) : null;

        foreach (var item in FilteredClasses)
        {
            if (_scheduleSignatures.TryGetValue(item.Id, out var info))
            {
                item.HasConfiguredSchedule = info.IsConfigured;
            }
            else
            {
                item.HasConfiguredSchedule = false;
            }

            item.IsSelectionCompatible = selected.Count == 0
                || item.IsSelected
                || anchorSignature is not null && string.Equals(GetClassSignature(item.Id), anchorSignature, StringComparison.Ordinal);
        }
    }

    private void ApplySignatureMetadata(ClassSelectionItemViewModel item)
    {
        if (_scheduleSignatures.TryGetValue(item.Id, out var info))
        {
            item.HasConfiguredSchedule = info.IsConfigured;
        }
    }

    private async Task ReloadCatalogAsync(bool reloadSchedule = true)
    {
        var selectedFeeTypeId = SelectedFeeType?.Id;
        var selectedCatalogFeeTypeId = SelectedCatalogFeeType?.Id;
        var selectedPricingCategoryId = SelectedPricingCategory?.Id;
        var selectedCatalogPricingCategoryId = SelectedCatalogPricingCategory?.Id;
        var catalog = await _schoolFeeApi.GetCatalogAsync();

        FeeTypes.Clear();
        CatalogFeeTypes.Clear();
        foreach (var item in catalog.FeeTypes)
        {
            CatalogFeeTypes.Add(item);
            if (item.IsActive)
            {
                FeeTypes.Add(item);
            }
        }

        PricingCategories.Clear();
        CatalogPricingCategories.Clear();
        foreach (var item in catalog.PricingCategories)
        {
            CatalogPricingCategories.Add(item);
            if (item.IsActive)
            {
                PricingCategories.Add(item);
            }
        }

        Installments.Clear();
        CatalogInstallments.Clear();
        foreach (var item in catalog.Installments)
        {
            CatalogInstallments.Add(item);
            if (item.IsActive)
            {
                Installments.Add(item);
            }
        }

        SelectedPricingCategory = PricingCategories.FirstOrDefault(c => c.Id == selectedPricingCategoryId)
            ?? PricingCategories.FirstOrDefault();
        SelectedCatalogPricingCategory = catalog.PricingCategories.FirstOrDefault(c => c.Id == selectedCatalogPricingCategoryId);
        SelectedFeeType = FeeTypes.FirstOrDefault(f => f.Id == selectedFeeTypeId) ?? FeeTypes.FirstOrDefault();
        SelectedCatalogFeeType = catalog.FeeTypes.FirstOrDefault(f => f.Id == selectedCatalogFeeTypeId);
        OnPropertyChanged(nameof(FeeTypeSectionHeaderText));
        OnPropertyChanged(nameof(PricingCategorySectionHeaderText));
        OnPropertyChanged(nameof(InstallmentSectionHeaderText));
        RefreshAvailableInstallmentsForAssignment();
        RefreshAvailableInstallmentsForConfiguration();
        if (reloadSchedule)
        {
            await RefreshScheduleSignaturesAndLoadAsync();
        }
        else
        {
            await LoadFeeTypeInstallmentPoolAsync();
            await RefreshScheduleSignaturesAsync();
            UpdateClassSelectionCompatibility();
        }
    }

    private void RefreshFilteredClasses(bool selectFirst = false)
    {
        var previousSelection = FilteredClasses
            .Where(c => c.IsSelected)
            .Select(c => c.Id)
            .ToHashSet();

        _isRefreshingClassSelection = true;
        FilteredClasses.Clear();

        IEnumerable<PedagogicalClassFilterItem> classes = _allPedagogicalClasses;
        if (SelectedSection is not null)
        {
            classes = classes.Where(c => c.SectionId == SelectedSection.Id);
        }

        var items = classes
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .OrderBy(c => c.DisplayName)
            .ToList();
        var hasPreviousSelection = previousSelection.Count > 0;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var isSelected = hasPreviousSelection
                ? previousSelection.Contains(item.Id)
                : selectFirst && index == 0;
            var row = new ClassSelectionItemViewModel(item.Id, item.DisplayName, isSelected);
            row.SelectionChanged += () => OnClassSelectionChanged(row);
            ApplySignatureMetadata(row);
            FilteredClasses.Add(row);
        }

        _isRefreshingClassSelection = false;
        RefreshVisibleClasses();
        UpdateClassSelectionCompatibility();
        NotifyClassSelectionChanged();
    }

    private void RefreshVisibleClasses()
    {
        VisibleClasses.Clear();
        var query = ClassSearchText.Trim();
        foreach (var item in FilteredClasses)
        {
            if (string.IsNullOrWhiteSpace(query)
                || item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                VisibleClasses.Add(item);
            }
        }
    }

    private void UpdateScheduleEditability()
    {
        if (SelectedAcademicYear is null)
        {
            IsScheduleEditable = true;
            ReadOnlyNotice = null;
            return;
        }

        IsScheduleEditable = AcademicYearFeeRules.CanEditFees(SelectedAcademicYear);
        ReadOnlyNotice = IsScheduleEditable ? null : AcademicYearFeeRules.GetReadOnlyReason(SelectedAcademicYear);
        OnPropertyChanged(nameof(IsScheduleReadOnly));
    }

    private async Task LoadScheduleStructureAsync()
    {
        ScheduleLines.Clear();
        AnnualTotal = 0;
        OnPropertyChanged(nameof(InstallmentCount));

        if (SelectedFeeType is null)
        {
            RefreshAvailableInstallmentsForConfiguration();
            return;
        }

        try
        {
            var items = await _schoolFeeApi.GetFeeTypeInstallmentsAsync(SelectedFeeType.Id);
            foreach (var item in items.OrderBy(i => i.SortOrder))
            {
                ScheduleLines.Add(CreateScheduleLine(item.FeeInstallmentId, item.InstallmentName, item.SortOrder, 0, null));
            }

            RefreshAvailableInstallmentsForConfiguration();
            RefreshDueDateConstraints();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void BuildScheduleLines(IReadOnlyList<ClassFeeScheduleLineDto> lines)
    {
        ScheduleLines.Clear();
        foreach (var line in lines.OrderBy(l => l.SortOrder))
        {
            var row = CreateScheduleLine(line.FeeInstallmentId, line.InstallmentName, line.SortOrder, line.Amount, line.DueDate);
            ScheduleLines.Add(row);
        }

        RefreshAvailableInstallmentsForConfiguration();
        RefreshDueDateConstraints();
    }

    private ScheduleLineViewModel CreateScheduleLine(
        Guid feeInstallmentId,
        string installmentName,
        int sortOrder,
        decimal amount,
        DateOnly? dueDate)
    {
        var row = new ScheduleLineViewModel(feeInstallmentId, installmentName, sortOrder, amount, dueDate);
        row.PropertyChanged += OnScheduleLinePropertyChanged;
        return row;
    }

    private void OnScheduleLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ScheduleLineViewModel line)
        {
            return;
        }

        if (e.PropertyName == nameof(ScheduleLineViewModel.DueDate))
        {
            EnforceDueDateOrder(line);
        }
        else if (e.PropertyName == nameof(ScheduleLineViewModel.Amount))
        {
            RecalculateTotal();
        }
    }

    private void EnforceDueDateOrder(ScheduleLineViewModel changedLine)
    {
        if (_isEnforcingDueDates)
        {
            return;
        }

        if (!changedLine.DueDate.HasValue)
        {
            RefreshDueDateConstraints();
            return;
        }

        var ordered = ScheduleLines.OrderBy(l => l.SortOrder).ToList();
        var index = ordered.FindIndex(l => l.FeeInstallmentId == changedLine.FeeInstallmentId);
        if (index < 0)
        {
            return;
        }

        var candidate = changedLine.DueDate.Value;
        var previous = index > 0 ? ordered[index - 1] : null;
        var next = index < ordered.Count - 1 ? ordered[index + 1] : null;

        if (previous?.DueDate is { } minDate && candidate < minDate)
        {
            _isEnforcingDueDates = true;
            changedLine.DueDate = minDate;
            _isEnforcingDueDates = false;
            SetStatus(
                $"La date limite ne peut pas être antérieure à « {previous.InstallmentName} » ({minDate:dd/MM/yyyy}).",
                FeeStatusMessageKind.Warning);
            RefreshDueDateConstraints();
            return;
        }

        if (next?.DueDate is { } maxDate && candidate > maxDate)
        {
            _isEnforcingDueDates = true;
            changedLine.DueDate = maxDate;
            _isEnforcingDueDates = false;
            SetStatus(
                $"La date limite ne peut pas être postérieure à « {next.InstallmentName} » ({maxDate:dd/MM/yyyy}).",
                FeeStatusMessageKind.Warning);
            RefreshDueDateConstraints();
            return;
        }

        if (StatusMessageKind == FeeStatusMessageKind.Warning)
        {
            StatusMessage = null;
            StatusMessageKind = FeeStatusMessageKind.None;
        }

        RefreshDueDateConstraints();
    }

    private void RefreshDueDateConstraints()
    {
        var ordered = ScheduleLines.OrderBy(l => l.SortOrder).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var line = ordered[index];
            line.MinDueDate = index > 0 ? ordered[index - 1].DueDate : null;
            line.MaxDueDate = index < ordered.Count - 1 ? ordered[index + 1].DueDate : null;
        }
    }

    private void RenumberScheduleLines()
    {
        var order = 1;
        foreach (var line in ScheduleLines)
        {
            line.SortOrder = order++;
        }

        RefreshDueDateConstraints();
    }

    private void RecalculateTotal()
    {
        AnnualTotal = ScheduleLines.Sum(l => l.Amount);
        OnPropertyChanged(nameof(InstallmentCount));
    }

    private void SetStatus(string? message, FeeStatusMessageKind kind = FeeStatusMessageKind.Info)
    {
        StatusMessage = message;
        StatusMessageKind = string.IsNullOrWhiteSpace(message) ? FeeStatusMessageKind.None : kind;
    }

    private void NotifyClassSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedClassCount));
        OnPropertyChanged(nameof(SelectedClassesSummary));
        OnPropertyChanged(nameof(CanOpenAddInstallmentPicker));
    }

    private void ClearFeeTypeForm()
    {
        FeeTypeCode = string.Empty;
        FeeTypeName = string.Empty;
        FeeTypeCurrency = Currency.CDF;
        FeeTypeIsMandatory = true;
        FeeTypeIsActive = true;
        IsEditingFeeType = false;
        FeeTypeInstallmentItems.Clear();
        InstallmentToAssign = null;
        RefreshAvailableInstallmentsForAssignment();
    }

    private void ClearPricingCategoryForm()
    {
        PricingCategoryCode = string.Empty;
        PricingCategoryName = string.Empty;
        PricingCategoryDescription = string.Empty;
        PricingCategoryIsActive = true;
        IsEditingPricingCategory = false;
    }

    private async Task LoadFeeTypeInstallmentsAsync(bool reloadSchedule = false)
    {
        FeeTypeInstallmentItems.Clear();
        InstallmentToAssign = null;

        if (SelectedCatalogFeeType is null)
        {
            RefreshAvailableInstallmentsForAssignment();
            return;
        }

        try
        {
            await PopulateInstallmentItemsAsync(SelectedCatalogFeeType.Id, FeeTypeInstallmentItems);
            RefreshAvailableInstallmentsForAssignment();
            if (reloadSchedule)
            {
                await LoadScheduleAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task PopulateInstallmentItemsAsync(
        Guid feeTypeId,
        ICollection<FeeTypeInstallmentItemViewModel> target)
    {
        var items = await _schoolFeeApi.GetFeeTypeInstallmentsAsync(feeTypeId);
        foreach (var item in items.OrderBy(i => i.SortOrder))
        {
            target.Add(new FeeTypeInstallmentItemViewModel(
                item.FeeInstallmentId,
                item.InstallmentName,
                item.SortOrder));
        }
    }

    private async Task SaveInstallmentsForFeeTypeAsync(
        Guid feeTypeId,
        IEnumerable<FeeTypeInstallmentItemViewModel> items)
    {
        var payload = items
            .OrderBy(i => i.SortOrder)
            .Select((item, index) => new SaveFeeTypeInstallmentItemRequest(item.FeeInstallmentId, index + 1))
            .ToList();

        await _schoolFeeApi.SaveFeeTypeInstallmentsAsync(
            feeTypeId,
            new SaveFeeTypeInstallmentsRequest(payload));
    }

    private async Task EnsureFeeTypeInstallmentsIncludeScheduleAsync()
    {
        if (SelectedFeeType is null || ScheduleLines.Count == 0)
        {
            return;
        }

        var existing = await _schoolFeeApi.GetFeeTypeInstallmentsAsync(SelectedFeeType.Id);
        var existingIds = existing.Select(e => e.FeeInstallmentId).ToHashSet();
        var merged = existing
            .Select(e => new SaveFeeTypeInstallmentItemRequest(e.FeeInstallmentId, e.SortOrder))
            .ToList();

        var maxOrder = merged.Count > 0 ? merged.Max(m => m.SortOrder) : 0;
        foreach (var line in ScheduleLines.OrderBy(l => l.SortOrder))
        {
            if (existingIds.Add(line.FeeInstallmentId))
            {
                merged.Add(new SaveFeeTypeInstallmentItemRequest(line.FeeInstallmentId, ++maxOrder));
            }
        }

        if (merged.Count <= existing.Count)
        {
            return;
        }

        await _schoolFeeApi.SaveFeeTypeInstallmentsAsync(
            SelectedFeeType.Id,
            new SaveFeeTypeInstallmentsRequest(merged));
        await LoadFeeTypeInstallmentPoolAsync();
    }

    private IEnumerable<FeeInstallmentDto> GetConfigurationInstallmentSources()
    {
        var byId = new Dictionary<Guid, FeeInstallmentDto>();
        foreach (var installment in CatalogInstallments.Where(i => i.IsActive))
        {
            byId[installment.Id] = installment;
        }

        foreach (var installment in _feeTypeInstallmentPool)
        {
            byId[installment.Id] = installment;
        }

        return byId.Values.OrderBy(i => i.SortOrder);
    }

    private void RefreshAvailableInstallmentsForConfiguration()
    {
        AvailableInstallmentsForConfiguration.Clear();
        var assignedIds = ScheduleLines.Select(i => i.FeeInstallmentId).ToHashSet();
        foreach (var installment in GetConfigurationInstallmentSources()
                     .Where(i => !assignedIds.Contains(i.Id)))
        {
            AvailableInstallmentsForConfiguration.Add(installment);
        }

        OnPropertyChanged(nameof(HasAvailableInstallmentsForConfiguration));
        OnPropertyChanged(nameof(CanOpenAddInstallmentPicker));
        if (!HasAvailableInstallmentsForConfiguration)
        {
            IsAddInstallmentPickerOpen = false;
        }
    }

    private void RefreshAvailableInstallmentsForAssignment()
    {
        AvailableInstallmentsForAssignment.Clear();
        var assignedIds = FeeTypeInstallmentItems.Select(i => i.FeeInstallmentId).ToHashSet();
        foreach (var installment in CatalogInstallments.Where(i => i.IsActive && !assignedIds.Contains(i.Id)))
        {
            AvailableInstallmentsForAssignment.Add(installment);
        }
    }

    private void RenumberFeeTypeInstallments()
    {
        RebindFeeTypeInstallmentItems(FeeTypeInstallmentItems.OrderBy(i => i.SortOrder));
        var order = 1;
        foreach (var item in FeeTypeInstallmentItems.OrderBy(i => i.SortOrder))
        {
            item.SortOrder = order++;
        }
    }

    private void RebindFeeTypeInstallmentItems(IEnumerable<FeeTypeInstallmentItemViewModel> items)
    {
        FeeTypeInstallmentItems.Clear();
        foreach (var item in items)
        {
            FeeTypeInstallmentItems.Add(item);
        }
    }

    private void ClearInstallmentForm()
    {
        InstallmentName = string.Empty;
        InstallmentSortOrder = 0;
        InstallmentIsActive = true;
    }

    private static Guid ResolveSectionIdForProgram(SchoolProgram program, IEnumerable<SectionDto> sections)
    {
        var code = program switch
        {
            SchoolProgram.Maternelle => "MAT",
            SchoolProgram.Primaire => "PRI",
            SchoolProgram.CTEB => "CTEB",
            SchoolProgram.Humanites => "HUM",
            SchoolProgram.HumanitesProfessionnelles => "HPRO",
            SchoolProgram.FilieresSpecialisees => "FS",
            _ => "PRI"
        };

        return sections.FirstOrDefault(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty;
    }
}

public sealed partial class FeeTypeInstallmentItemViewModel : ObservableObject
{
    public FeeTypeInstallmentItemViewModel(Guid feeInstallmentId, string installmentName, int sortOrder)
    {
        FeeInstallmentId = feeInstallmentId;
        InstallmentName = installmentName;
        _sortOrder = sortOrder;
    }

    public Guid FeeInstallmentId { get; }

    public string InstallmentName { get; }

    [ObservableProperty] private int _sortOrder;
}

public partial class ScheduleLineViewModel : ObservableObject
{
    public ScheduleLineViewModel(
        Guid feeInstallmentId,
        string installmentName,
        int sortOrder,
        decimal amount,
        DateOnly? dueDate)
    {
        FeeInstallmentId = feeInstallmentId;
        InstallmentName = installmentName;
        SortOrder = sortOrder;
        _amount = amount;
        DueDate = dueDate;
    }

    public Guid FeeInstallmentId { get; }
    public string InstallmentName { get; }

    [ObservableProperty] private int _sortOrder;

    [ObservableProperty] private decimal _amount;

    [ObservableProperty] private DateOnly? _dueDate;

    [ObservableProperty] private DateOnly? _minDueDate;

    [ObservableProperty] private DateOnly? _maxDueDate;

    public string AmountText
    {
        get => Amount.ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Amount = 0;
                OnPropertyChanged();
                return;
            }

            if (decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
            {
                Amount = parsed;
                OnPropertyChanged();
            }
        }
    }

    public DateTime? MinDueDatePicker => MinDueDate?.ToDateTime(TimeOnly.MinValue);

    public DateTime? MaxDueDatePicker => MaxDueDate?.ToDateTime(TimeOnly.MinValue);

    partial void OnMinDueDateChanged(DateOnly? value) => OnPropertyChanged(nameof(MinDueDatePicker));

    partial void OnMaxDueDateChanged(DateOnly? value) => OnPropertyChanged(nameof(MaxDueDatePicker));

    partial void OnDueDateChanged(DateOnly? value)
    {
        OnPropertyChanged(nameof(DueDatePicker));
        OnPropertyChanged(nameof(DueDateDisplay));
        NotifyStatusChanged();
    }

    partial void OnAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(AmountText));
        NotifyStatusChanged();
    }

    public string DueDateDisplay => DueDate?.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) ?? "—";

    public string StatusLabel => StatusKind switch
    {
        ScheduleLineStatusKind.Complete => "Complet",
        ScheduleLineStatusKind.Configured => "Configuré",
        _ => "En attente"
    };

    public ScheduleLineStatusKind StatusKind =>
        Amount > 0
            ? DueDate.HasValue ? ScheduleLineStatusKind.Complete : ScheduleLineStatusKind.Configured
            : ScheduleLineStatusKind.Pending;

    private void NotifyStatusChanged()
    {
        OnPropertyChanged(nameof(StatusKind));
        OnPropertyChanged(nameof(StatusLabel));
    }

    public DateTime? DueDatePicker
    {
        get => DueDate?.ToDateTime(TimeOnly.MinValue);
        set => DueDate = value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
    }
}

public sealed partial class ClassSelectionItemViewModel : ObservableObject
{
    public ClassSelectionItemViewModel(Guid id, string displayName, bool isSelected)
    {
        Id = id;
        DisplayName = displayName;
        _isSelected = isSelected;
    }

    public Guid Id { get; }
    public string DisplayName { get; }

    public event Action? SelectionChanged;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isSelectionCompatible = true;

    [ObservableProperty]
    private bool _hasConfiguredSchedule;

    public string SelectionTooltip =>
        IsSelectionCompatible
            ? HasConfiguredSchedule
                ? "Configuration tarifaire propre à cette classe."
                : "Configuration tarifaire identique aux classes compatibles."
            : "Configuration tarifaire différente des classes déjà sélectionnées.";

    partial void OnIsSelectionCompatibleChanged(bool value) => OnPropertyChanged(nameof(SelectionTooltip));

    partial void OnHasConfiguredScheduleChanged(bool value) => OnPropertyChanged(nameof(SelectionTooltip));

    partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();
}

public enum FeeStatusMessageKind
{
    None,
    Info,
    Success,
    Warning,
    Error
}

public enum ScheduleLineStatusKind
{
    Pending,
    Configured,
    Complete
}
