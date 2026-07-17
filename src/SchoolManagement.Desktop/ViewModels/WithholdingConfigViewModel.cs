using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Withholdings.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Configuration Paramètres : types de retenues + configurations par année scolaire.</summary>
public partial class WithholdingConfigViewModel : ViewModelBase
{
    private readonly IWithholdingApiService _withholdingApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly ISchoolFeeApiService _schoolFeeApi;
    private readonly object _typesSync = new();
    private readonly object _configsSync = new();
    private int _typesLoadVersion;
    private int _configsLoadVersion;

    public WithholdingConfigViewModel(
        IWithholdingApiService withholdingApi,
        ISchoolApiService schoolApi,
        ISchoolFeeApiService schoolFeeApi)
    {
        _withholdingApi = withholdingApi;
        _schoolApi = schoolApi;
        _schoolFeeApi = schoolFeeApi;

        CalculationModes =
        [
            new CalculationModeOption(WithholdingCalculationMode.Pourcentage, "Pourcentage"),
            new CalculationModeOption(WithholdingCalculationMode.MontantFixe, "Montant fixe")
        ];
        SelectedCalculationMode = CalculationModes[0];
    }

    public ObservableCollection<WithholdingTypeDto> Types { get; } = [];
    public ObservableCollection<WithholdingConfigurationListItem> Configurations { get; } = [];
    public ObservableCollection<AcademicYearDto> AcademicYears { get; } = [];
    public ObservableCollection<FeeTypeDto> FeeTypes { get; } = [];
    public ObservableCollection<NullableLookupOption> InstallmentOptions { get; } = [];
    public ObservableCollection<NullableLookupOption> CategoryOptions { get; } = [];
    public ObservableCollection<WithholdingTypeDto> ActiveTypes { get; } = [];
    public IReadOnlyList<CalculationModeOption> CalculationModes { get; }

    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private FeeStatusMessageKind _statusMessageKind = FeeStatusMessageKind.None;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private WithholdingTypeDto? _selectedType;
    [ObservableProperty] private string _typeCode = string.Empty;
    [ObservableProperty] private string _typeName = string.Empty;
    [ObservableProperty] private string _typeDescription = string.Empty;
    [ObservableProperty] private bool _typeIsActive = true;

    [ObservableProperty] private AcademicYearDto? _selectedConfigYear;
    [ObservableProperty] private WithholdingTypeDto? _selectedConfigType;
    [ObservableProperty] private FeeTypeDto? _selectedConfigFeeType;
    [ObservableProperty] private NullableLookupOption? _selectedConfigInstallment;
    [ObservableProperty] private NullableLookupOption? _selectedConfigCategory;
    [ObservableProperty] private CalculationModeOption? _selectedCalculationMode;
    [ObservableProperty] private string _configValueText = "0";
    [ObservableProperty] private bool _configIsActive = true;
    [ObservableProperty] private WithholdingConfigurationListItem? _selectedConfiguration;

    [ObservableProperty] private AcademicYearDto? _filterYear;
    [ObservableProperty] private WithholdingTypeDto? _filterType;
    [ObservableProperty] private FeeTypeDto? _filterFeeType;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _filterActiveOnly = true;
    [ObservableProperty] private int _totalCount;

    [ObservableProperty] private bool _isConfigurationExpanded = true;
    [ObservableProperty] private bool _isTypesExpanded = true;

    public string ConfigurationSectionHeaderText => "Configuration des retenues";
    public string TypesSectionHeaderText => $"Types de retenues ({Types.Count})";

    public string ConfigurationToggleLabel =>
        IsConfigurationExpanded
            ? "Masquer la configuration des retenues"
            : "Afficher la configuration des retenues";

    public string TypesToggleLabel =>
        IsTypesExpanded
            ? "Masquer les types de retenues"
            : "Afficher les types de retenues";

    public string ConfigurationsCountLabel => $"{TotalCount} configuration(s)";

    public string ValueFieldLabel =>
        SelectedCalculationMode?.Mode == WithholdingCalculationMode.Pourcentage
            ? "Valeur (%)"
            : "Valeur (montant)";

    partial void OnIsConfigurationExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(ConfigurationToggleLabel));

    partial void OnIsTypesExpandedChanged(bool value) =>
        OnPropertyChanged(nameof(TypesToggleLabel));

    partial void OnSelectedCalculationModeChanged(CalculationModeOption? value) =>
        OnPropertyChanged(nameof(ValueFieldLabel));

    partial void OnTotalCountChanged(int value) =>
        OnPropertyChanged(nameof(ConfigurationsCountLabel));

    partial void OnSelectedTypeChanged(WithholdingTypeDto? value)
    {
        if (value is null)
        {
            TypeCode = string.Empty;
            TypeName = string.Empty;
            TypeDescription = string.Empty;
            TypeIsActive = true;
            return;
        }

        TypeCode = value.Code;
        TypeName = value.Name;
        TypeDescription = value.Description ?? string.Empty;
        TypeIsActive = value.IsActive;
    }

    partial void OnSelectedConfigurationChanged(WithholdingConfigurationListItem? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedConfigYear = AcademicYears.FirstOrDefault(y => y.Id == value.AcademicYearId) ?? SelectedConfigYear;
        SelectedConfigType = ActiveTypes.FirstOrDefault(t => t.Id == value.WithholdingTypeId)
            ?? Types.FirstOrDefault(t => t.Id == value.WithholdingTypeId);
        SelectedConfigFeeType = FeeTypes.FirstOrDefault(f => f.Id == value.FeeTypeId);
        SelectedConfigInstallment = InstallmentOptions.FirstOrDefault(i => i.Id == value.FeeInstallmentId)
            ?? InstallmentOptions.FirstOrDefault();
        SelectedConfigCategory = CategoryOptions.FirstOrDefault(c => c.Id == value.PricingCategoryId)
            ?? CategoryOptions.FirstOrDefault();
        SelectedCalculationMode = CalculationModes.FirstOrDefault(m => m.Mode == value.CalculationMode)
            ?? CalculationModes[0];
        ConfigValueText = value.Value.ToString(CultureInfo.CurrentCulture);
        ConfigIsActive = value.IsActive;
    }

    partial void OnSelectedConfigYearChanged(AcademicYearDto? value)
    {
        if (FilterYear is null && value is not null)
        {
            FilterYear = value;
        }
    }

    [RelayCommand]
    private void ToggleConfiguration() => IsConfigurationExpanded = !IsConfigurationExpanded;

    [RelayCommand]
    private void ToggleTypes() => IsTypesExpanded = !IsTypesExpanded;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        SetStatus(null);
        try
        {
            AcademicYears.Clear();
            foreach (var year in await _schoolApi.GetAcademicYearsAsync())
            {
                AcademicYears.Add(year);
            }

            SelectedConfigYear ??= AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();
            FilterYear ??= SelectedConfigYear;

            FeeTypes.Clear();
            InstallmentOptions.Clear();
            CategoryOptions.Clear();
            InstallmentOptions.Add(NullableLookupOption.All("Toutes les tranches"));
            CategoryOptions.Add(NullableLookupOption.All("Toutes les catégories"));

            var catalog = await _schoolFeeApi.GetCatalogAsync();
            foreach (var feeType in catalog.FeeTypes
                         .Where(f => f.IsActive)
                         .GroupBy(f => f.Id)
                         .Select(g => g.First())
                         .OrderBy(f => f.Name))
            {
                FeeTypes.Add(feeType);
            }

            foreach (var installment in catalog.Installments.Where(i => i.IsActive).OrderBy(i => i.SortOrder).ThenBy(i => i.Name))
            {
                InstallmentOptions.Add(new NullableLookupOption(installment.Id, installment.Name));
            }

            foreach (var category in catalog.PricingCategories.Where(c => c.IsActive).OrderBy(c => c.Name))
            {
                CategoryOptions.Add(new NullableLookupOption(category.Id, category.Name));
            }

            SelectedConfigInstallment ??= InstallmentOptions.FirstOrDefault();
            SelectedConfigCategory ??= CategoryOptions.FirstOrDefault();
            SelectedConfigFeeType ??= FeeTypes.FirstOrDefault();

            await LoadTypesAsync();
            SelectedConfigType ??= ActiveTypes.FirstOrDefault();
            await SearchConfigurationsAsync();
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
    private async Task LoadTypesAsync()
    {
        var version = Interlocked.Increment(ref _typesLoadVersion);
        var items = await _withholdingApi.GetTypesAsync();
        lock (_typesSync)
        {
            if (version != _typesLoadVersion)
            {
                return;
            }

            Types.Clear();
            ActiveTypes.Clear();
            foreach (var item in items)
            {
                Types.Add(item);
                if (item.IsActive)
                {
                    ActiveTypes.Add(item);
                }
            }
        }

        OnPropertyChanged(nameof(TypesSectionHeaderText));
    }

    [RelayCommand]
    private void NewType()
    {
        SelectedType = null;
        TypeCode = string.Empty;
        TypeName = string.Empty;
        TypeDescription = string.Empty;
        TypeIsActive = true;
    }

    [RelayCommand]
    private async Task SaveTypeAsync()
    {
        IsBusy = true;
        try
        {
            var request = new SaveWithholdingTypeRequest(
                TypeCode.Trim(),
                TypeName.Trim(),
                string.IsNullOrWhiteSpace(TypeDescription) ? null : TypeDescription.Trim(),
                TypeIsActive);

            if (SelectedType is null)
            {
                await _withholdingApi.CreateTypeAsync(request);
                SetStatus("Type de retenue créé.", FeeStatusMessageKind.Success);
            }
            else
            {
                await _withholdingApi.UpdateTypeAsync(SelectedType.Id, request);
                SetStatus("Type de retenue mis à jour.", FeeStatusMessageKind.Success);
            }

            await LoadTypesAsync();
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
    private async Task DeactivateTypeAsync()
    {
        if (SelectedType is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _withholdingApi.DeactivateTypeAsync(SelectedType.Id);
            SetStatus("Type de retenue désactivé.", FeeStatusMessageKind.Success);
            await LoadTypesAsync();
            SelectedConfigType = ActiveTypes.FirstOrDefault(t => t.Id == SelectedConfigType?.Id)
                ?? ActiveTypes.FirstOrDefault();
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
    private void NewConfiguration()
    {
        SelectedConfiguration = null;
        ConfigValueText = "0";
        ConfigIsActive = true;
        SelectedCalculationMode = CalculationModes[0];
        SelectedConfigInstallment = InstallmentOptions.FirstOrDefault();
        SelectedConfigCategory = CategoryOptions.FirstOrDefault();
        SetStatus(
            "Nouvelle configuration : les valeurs s'appliquent uniquement aux nouveaux paiements de l'année scolaire sélectionnée.",
            FeeStatusMessageKind.Info);
    }

    [RelayCommand]
    private async Task SearchConfigurationsAsync()
    {
        var version = Interlocked.Increment(ref _configsLoadVersion);
        IsBusy = true;
        try
        {
            var result = await _withholdingApi.SearchConfigurationsAsync(BuildSearchRequest(pageSize: 200));
            lock (_configsSync)
            {
                if (version != _configsLoadVersion)
                {
                    return;
                }

                Configurations.Clear();
                foreach (var item in result.Items)
                {
                    Configurations.Add(WithholdingConfigurationListItem.FromDto(item));
                }

                TotalCount = result.TotalCount;
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
    private async Task SaveConfigurationAsync()
    {
        if (SelectedConfigYear is null)
        {
            SetStatus("Sélectionnez une année scolaire.", FeeStatusMessageKind.Warning);
            return;
        }

        if (SelectedConfigType is null)
        {
            SetStatus("Sélectionnez un type de retenue.", FeeStatusMessageKind.Warning);
            return;
        }

        if (SelectedConfigFeeType is null)
        {
            SetStatus("Sélectionnez un type de frais.", FeeStatusMessageKind.Warning);
            return;
        }

        if (SelectedCalculationMode is null)
        {
            SetStatus("Sélectionnez un mode de calcul.", FeeStatusMessageKind.Warning);
            return;
        }

        if (!decimal.TryParse(ConfigValueText.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            && !decimal.TryParse(ConfigValueText, NumberStyles.Number, CultureInfo.CurrentCulture, out value))
        {
            SetStatus("La valeur doit être un nombre valide.", FeeStatusMessageKind.Warning);
            return;
        }

        if (value < 0)
        {
            SetStatus("La valeur ne peut pas être négative.", FeeStatusMessageKind.Warning);
            return;
        }

        if (SelectedCalculationMode.Mode == WithholdingCalculationMode.Pourcentage && value > 100m)
        {
            SetStatus("Un pourcentage ne peut pas dépasser 100 %.", FeeStatusMessageKind.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var request = new SaveWithholdingConfigurationRequest(
                SelectedConfigYear.Id,
                SelectedConfigType.Id,
                SelectedConfigFeeType.Id,
                SelectedConfigInstallment?.Id,
                SelectedConfigCategory?.Id,
                SelectedCalculationMode.Mode,
                value,
                ConfigIsActive);

            if (SelectedConfiguration is null)
            {
                await _withholdingApi.CreateConfigurationAsync(request);
                SetStatus("Configuration de retenue créée pour cette année scolaire.", FeeStatusMessageKind.Success);
            }
            else
            {
                await _withholdingApi.UpdateConfigurationAsync(SelectedConfiguration.Id, request);
                SetStatus("Configuration de retenue mise à jour.", FeeStatusMessageKind.Success);
            }

            await SearchConfigurationsAsync();
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
    private async Task DeactivateConfigurationAsync()
    {
        if (SelectedConfiguration is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _withholdingApi.DeactivateConfigurationAsync(SelectedConfiguration.Id);
            SetStatus("Configuration désactivée.", FeeStatusMessageKind.Success);
            await SearchConfigurationsAsync();
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
    private async Task DeleteConfigurationAsync()
    {
        if (SelectedConfiguration is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _withholdingApi.DeleteConfigurationAsync(SelectedConfiguration.Id);
            SetStatus("Configuration supprimée.", FeeStatusMessageKind.Success);
            SelectedConfiguration = null;
            await SearchConfigurationsAsync();
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
    private async Task ExportExcelAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Classeur Excel (*.xlsx)|*.xlsx",
                FileName = "retenues.xlsx",
                AddExtension = true,
                DefaultExt = ".xlsx"
            };
            ErpFileDialog.PrepareSave(dialog);
            if (ErpFileDialog.ShowSave(dialog) != true)
            {
                return;
            }

            IsBusy = true;
            var bytes = await _withholdingApi.ExportExcelAsync(BuildSearchRequest(pageSize: 5000));
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            SetStatus("Export Excel généré.", FeeStatusMessageKind.Success);
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
    private async Task ExportPdfAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Document PDF (*.pdf)|*.pdf",
                FileName = "retenues.pdf",
                AddExtension = true,
                DefaultExt = ".pdf"
            };
            ErpFileDialog.PrepareSave(dialog);
            if (ErpFileDialog.ShowSave(dialog) != true)
            {
                return;
            }

            IsBusy = true;
            var bytes = await _withholdingApi.ExportPdfAsync(BuildSearchRequest(pageSize: 5000));
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            SetStatus("Export PDF généré.", FeeStatusMessageKind.Success);
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

    private WithholdingConfigurationSearchRequest BuildSearchRequest(int pageSize) =>
        new(
            FilterYear?.Id,
            FilterType?.Id,
            FilterFeeType?.Id,
            null,
            null,
            null,
            FilterActiveOnly ? true : null,
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            1,
            pageSize);

    private void SetStatus(string? message, FeeStatusMessageKind kind = FeeStatusMessageKind.Info)
    {
        StatusMessage = message;
        StatusMessageKind = string.IsNullOrWhiteSpace(message) ? FeeStatusMessageKind.None : kind;
    }
}

public sealed record CalculationModeOption(WithholdingCalculationMode Mode, string Label);

public sealed record NullableLookupOption(Guid? Id, string Label)
{
    public static NullableLookupOption All(string label) => new(null, label);
}

public sealed class WithholdingConfigurationListItem
{
    public Guid Id { get; init; }
    public Guid AcademicYearId { get; init; }
    public string AcademicYearLabel { get; init; } = string.Empty;
    public Guid WithholdingTypeId { get; init; }
    public string WithholdingTypeCode { get; init; } = string.Empty;
    public string WithholdingTypeName { get; init; } = string.Empty;
    public Guid FeeTypeId { get; init; }
    public string FeeTypeName { get; init; } = string.Empty;
    public Guid? FeeInstallmentId { get; init; }
    public string FeeInstallmentName { get; init; } = string.Empty;
    public Guid? PricingCategoryId { get; init; }
    public string PricingCategoryName { get; init; } = string.Empty;
    public WithholdingCalculationMode CalculationMode { get; init; }
    public string CalculationModeLabel { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string ValueDisplay { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string StatusLabel { get; init; } = string.Empty;

    public static WithholdingConfigurationListItem FromDto(WithholdingConfigurationDto dto) => new()
    {
        Id = dto.Id,
        AcademicYearId = dto.AcademicYearId,
        AcademicYearLabel = dto.AcademicYearLabel,
        WithholdingTypeId = dto.WithholdingTypeId,
        WithholdingTypeCode = dto.WithholdingTypeCode,
        WithholdingTypeName = dto.WithholdingTypeName,
        FeeTypeId = dto.FeeTypeId,
        FeeTypeName = dto.FeeTypeName,
        FeeInstallmentId = dto.FeeInstallmentId,
        FeeInstallmentName = dto.FeeInstallmentName ?? "Toutes",
        PricingCategoryId = dto.PricingCategoryId,
        PricingCategoryName = dto.PricingCategoryName ?? "Toutes",
        CalculationMode = dto.CalculationMode,
        CalculationModeLabel = dto.CalculationMode == WithholdingCalculationMode.Pourcentage
            ? "Pourcentage"
            : "Montant fixe",
        Value = dto.Value,
        ValueDisplay = dto.CalculationMode == WithholdingCalculationMode.Pourcentage
            ? string.Create(CultureInfo.CurrentCulture, $"{dto.Value:N2} %")
            : string.Create(CultureInfo.CurrentCulture, $"{dto.Value:N2}"),
        IsActive = dto.IsActive,
        StatusLabel = dto.IsActive ? "Active" : "Inactive"
    };
}
