using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Helpers;
using SchoolManagement.Desktop.Models;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using Microsoft.Win32;

namespace SchoolManagement.Desktop.ViewModels;

public sealed record SituationFilterOption(PaymentSituationReportFilter Value, string Label);

public sealed record ScopeKindOption(PaymentSituationScopeKind Value, string Label);

public sealed record SortKindOption(PaymentSituationSortKind Value, string Label);

public sealed record StudyOptionFilterItem(string? Value, string Label);

public partial class InstallmentCheckItem : ObservableObject
{
    public InstallmentCheckItem(Guid id, string name, int sortOrder)
    {
        Id = id;
        Name = name;
        SortOrder = sortOrder;
    }

    public Guid Id { get; }
    public string Name { get; }
    public int SortOrder { get; }

    [ObservableProperty] private bool _isSelected;
}

/// <summary>État Finance — Situation des paiements (tableau croisé section/classe).</summary>
public partial class PaymentSituationReportViewModel : ViewModelBase
{
    private readonly IReportApiService _reportApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly ISchoolFeeApiService _schoolFeeApi;
    private readonly IEnrollmentWizardApiService _wizardApi;
    private bool _suppressReload;
    private bool _initialized;
    private List<EnrollmentClassOptionDto> _allClasses = [];
    private List<SectionDto> _structureSections = [];
    private Guid? _defaultFeeTypeId;

    public PaymentSituationReportViewModel(
        IReportApiService reportApi,
        ISchoolApiService schoolApi,
        ISchoolFeeApiService schoolFeeApi,
        IEnrollmentWizardApiService wizardApi)
    {
        _reportApi = reportApi;
        _schoolApi = schoolApi;
        _schoolFeeApi = schoolFeeApi;
        _wizardApi = wizardApi;

        SituationFilters =
        [
            new SituationFilterOption(PaymentSituationReportFilter.All, "Tout le monde"),
            new SituationFilterOption(PaymentSituationReportFilter.InOrder, "En ordre seulement"),
            new SituationFilterOption(PaymentSituationReportFilter.NotInOrder, "Non en ordre seulement")
        ];
        ScopeKinds =
        [
            new ScopeKindOption(PaymentSituationScopeKind.EntireFeeType, "Totalité du type de frais"),
            new ScopeKindOption(PaymentSituationScopeKind.SelectedInstallments, "Tranche(s) spécifique(s)")
        ];
        SortKinds =
        [
            new SortKindOption(PaymentSituationSortKind.Name, "Nom"),
            new SortKindOption(PaymentSituationSortKind.RegistrationNumber, "Matricule"),
            new SortKindOption(PaymentSituationSortKind.ClassName, "Classe"),
            new SortKindOption(PaymentSituationSortKind.BalanceDescending, "Solde décroissant")
        ];

        SelectedSituationFilter = SituationFilters[0];
        SelectedScopeKind = ScopeKinds[0];
        SelectedSortKind = SortKinds[0];
        StatusMessage = "Sélectionnez les critères puis générez l'état.";
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
    }

    private void OnGlobalAcademicYearChanged()
    {
        if (_initialized && !_suppressReload)
        {
            _ = ReloadStructureAsync();
        }
    }

    public ObservableCollection<PaymentSituationSectionGroup> SectionGroups { get; } = [];
    public ObservableCollection<PaymentSituationColumnHeader> InstallmentHeaders { get; } = [];
    public ObservableCollection<FeeTypeDto> FeeTypes { get; } = [];
    public ObservableCollection<SectionDto> Sections { get; } = [];
    public ObservableCollection<EnrollmentClassOptionDto> ClassRooms { get; } = [];
    public ObservableCollection<StudyOptionFilterItem> StudyOptions { get; } = [];
    public ObservableCollection<FeePricingCategoryDto> PricingCategories { get; } = [];
    public ObservableCollection<InstallmentCheckItem> Installments { get; } = [];

    public IReadOnlyList<SituationFilterOption> SituationFilters { get; }
    public IReadOnlyList<ScopeKindOption> ScopeKinds { get; }
    public IReadOnlyList<SortKindOption> SortKinds { get; }

    [ObservableProperty] private FeeTypeDto? _selectedFeeType;
    [ObservableProperty] private SituationFilterOption? _selectedSituationFilter;
    [ObservableProperty] private ScopeKindOption? _selectedScopeKind;
    [ObservableProperty] private SortKindOption? _selectedSortKind;
    [ObservableProperty] private SectionDto? _selectedSection;
    [ObservableProperty] private EnrollmentClassOptionDto? _selectedClassRoom;
    [ObservableProperty] private StudyOptionFilterItem? _selectedStudyOption;
    [ObservableProperty] private FeePricingCategoryDto? _selectedPricingCategory;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isFiltersExpanded = true;
    [ObservableProperty] private bool _isStudyOptionFilterEnabled;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private PaymentSituationReportResultDto? _lastResult;

    public string FiltersToggleLabel => IsFiltersExpanded ? "Masquer les filtres" : "Afficher les filtres";
    public bool IsInstallmentScope => SelectedScopeKind?.Value == PaymentSituationScopeKind.SelectedInstallments;
    public bool HasResult => LastResult is not null;
    public string TotalsSummary => LastResult is null
        ? string.Empty
        : $"Reste {LastResult.TotalBalance:N0} {LastResult.Currency}";

    partial void OnIsFiltersExpandedChanged(bool value) => OnPropertyChanged(nameof(FiltersToggleLabel));
    partial void OnSelectedScopeKindChanged(ScopeKindOption? value) => OnPropertyChanged(nameof(IsInstallmentScope));
    partial void OnLastResultChanged(PaymentSituationReportResultDto? value)
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(TotalsSummary));
    }

    partial void OnSelectedFeeTypeChanged(FeeTypeDto? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadInstallmentsAsync();
        }
    }

    partial void OnSelectedSectionChanged(SectionDto? value)
    {
        if (!_suppressReload)
        {
            RefreshStudyOptions();
            RefreshClassRooms();
        }
    }

    partial void OnSelectedStudyOptionChanged(StudyOptionFilterItem? value)
    {
        if (!_suppressReload)
        {
            RefreshClassRooms();
        }
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await InitializeAsync();
    }

    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    [RelayCommand]
    private void ResetFilters()
    {
        _suppressReload = true;
        try
        {
            SelectedSituationFilter = SituationFilters[0];
            SelectedScopeKind = ScopeKinds[0];
            SelectedSortKind = SortKinds[0];
            SelectedSection = null;
            SelectedPricingCategory = null;
            foreach (var installment in Installments)
            {
                installment.IsSelected = false;
            }

            RefreshStudyOptions();
            RefreshClassRooms();
            SelectedClassRoom = null;
            LastResult = null;
            SectionGroups.Clear();
            InstallmentHeaders.Clear();
            StatusMessage = "Filtres réinitialisés — générez l'état.";
        }
        finally
        {
            _suppressReload = false;
        }
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (AcademicYearRefreshBridge.SelectedYearId is null || SelectedFeeType is null)
        {
            StatusMessage = "Année scolaire (barre du haut) et type de frais sont obligatoires.";
            return;
        }

        if (IsInstallmentScope && !Installments.Any(i => i.IsSelected))
        {
            StatusMessage = "Sélectionnez au moins une tranche.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _reportApi.GetPaymentSituationReportAsync(BuildRequest());
            LastResult = result;
            ApplyPivotGroups(result);
            StatusMessage = $"{result.TotalCount} élève(s) — {result.ScopeLabel} — {result.SituationLabel}.";
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
    private async Task ExportPdfAsync()
    {
        if (!await EnsureResultAsync())
        {
            return;
        }

        await ExportAsync(
            "PDF",
            "situation-paiements.pdf",
            "PDF files|*.pdf",
            req => _reportApi.ExportPaymentSituationReportPdfAsync(req));
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (!await EnsureResultAsync())
        {
            return;
        }

        await ExportAsync(
            "Excel",
            "situation-paiements.xlsx",
            "Excel files|*.xlsx",
            req => _reportApi.ExportPaymentSituationReportExcelAsync(req));
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (!await EnsureResultAsync())
        {
            return;
        }

        try
        {
            IsBusy = true;
            var bytes = await _reportApi.ExportPaymentSituationReportPdfAsync(BuildRequest());
            var temp = Path.Combine(Path.GetTempPath(), $"situation-paiements-{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(temp, bytes);
            var psi = new System.Diagnostics.ProcessStartInfo(temp)
            {
                UseShellExecute = true,
                Verb = "print"
            };
            System.Diagnostics.Process.Start(psi);
            StatusMessage = "Document envoyé à l'impression.";
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

    private async Task<bool> EnsureResultAsync()
    {
        if (LastResult is not null)
        {
            return true;
        }

        await GenerateAsync();
        return LastResult is not null;
    }

    private async Task ExportAsync(
        string label,
        string defaultName,
        string filter,
        Func<PaymentSituationReportRequest, Task<byte[]>> exporter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = defaultName,
            Filter = filter
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var bytes = await exporter(BuildRequest());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            StatusMessage = $"Export {label} enregistré : {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyPivotGroups(PaymentSituationReportResultDto result)
    {
        SectionGroups.Clear();
        InstallmentHeaders.Clear();
        foreach (var column in result.InstallmentColumns)
        {
            InstallmentHeaders.Add(new PaymentSituationColumnHeader { Name = column.InstallmentName });
        }

        foreach (var sectionGroup in result.PivotRows
                     .GroupBy(r => r.SectionName)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var section = new PaymentSituationSectionGroup
            {
                SectionName = sectionGroup.Key,
                SectionRemaining = sectionGroup.Sum(r => r.Balance),
                StudentCount = sectionGroup.Count()
            };

            foreach (var classGroup in sectionGroup
                         .GroupBy(r => r.ClassName)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var classRow = new PaymentSituationClassGroup
                {
                    ClassName = classGroup.Key,
                    ClassRemaining = classGroup.Sum(r => r.Balance),
                    StudentCount = classGroup.Count()
                };

                foreach (var row in classGroup)
                {
                    var student = new PaymentSituationStudentRow
                    {
                        FullName = row.FullName,
                        AmountExpected = row.AmountExpected,
                        Remaining = row.Balance
                    };

                    for (var i = 0; i < result.InstallmentColumns.Count; i++)
                    {
                        var paid = i < row.InstallmentPaid.Count
                            ? row.InstallmentPaid[i]
                            : 0m;
                        var applicable = i < row.InstallmentApplicable.Count && row.InstallmentApplicable[i];
                        student.InstallmentCells.Add(new PaymentSituationAmountCell
                        {
                            Amount = paid,
                            IsApplicable = applicable
                        });
                    }

                    classRow.Students.Add(student);
                }

                section.Classes.Add(classRow);
            }

            SectionGroups.Add(section);
        }
    }

    private PaymentSituationReportRequest BuildRequest()
    {
        var installmentIds = IsInstallmentScope
            ? Installments.Where(i => i.IsSelected).Select(i => i.Id).ToList()
            : null;

        return new PaymentSituationReportRequest(
            AcademicYearRefreshBridge.SelectedYearId!.Value,
            SelectedFeeType!.Id,
            SelectedScopeKind?.Value ?? PaymentSituationScopeKind.EntireFeeType,
            installmentIds,
            SelectedSituationFilter?.Value ?? PaymentSituationReportFilter.All,
            EducationCycle: null,
            SelectedSection?.Id,
            PedagogicalClassId: null,
            SelectedClassRoom?.ClassRoomId,
            IsStudyOptionFilterEnabled ? SelectedStudyOption?.Value : null,
            SelectedPricingCategory?.Id,
            SelectedSortKind?.Value ?? PaymentSituationSortKind.Name);
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        _suppressReload = true;
        try
        {
            var catalog = await _schoolFeeApi.GetCatalogAsync();
            FeeTypes.Clear();
            foreach (var fee in catalog.FeeTypes.Where(f => f.IsActive).OrderBy(f => f.Name))
            {
                FeeTypes.Add(fee);
            }

            PricingCategories.Clear();
            foreach (var cat in catalog.PricingCategories.Where(c => c.IsActive).OrderBy(c => c.Name))
            {
                PricingCategories.Add(cat);
            }

            SelectedPricingCategory = null;
            var school = await _schoolApi.GetCurrentSchoolAsync();
            _defaultFeeTypeId = school?.DefaultFeeTypeId;
            SelectedFeeType = DefaultFeeTypeHelper.Resolve(FeeTypes, _defaultFeeTypeId);

            await ReloadStructureAsync();
            await ReloadInstallmentsAsync();
            StatusMessage = "Critères prêts — générez l'état.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            _suppressReload = false;
            IsBusy = false;
        }
    }

    private async Task ReloadStructureAsync()
    {
        if (AcademicYearRefreshBridge.SelectedYearId is null)
        {
            StatusMessage = "Aucune année scolaire sélectionnée (barre du haut).";
            return;
        }

        try
        {
            var options = await _wizardApi.GetStructureOptionsAsync();
            _allClasses = options.Classes.Where(c => c.IsSelectable).ToList();
            _structureSections = options.Sections.ToList();

            RefreshSectionsAndClasses();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RefreshSectionsAndClasses()
    {
        Sections.Clear();
        foreach (var section in _structureSections.OrderBy(s => s.Name))
        {
            Sections.Add(section);
        }

        SelectedSection = null;
        RefreshStudyOptions();
        RefreshClassRooms();
    }

    private void RefreshStudyOptions()
    {
        StudyOptions.Clear();
        StudyOptions.Add(new StudyOptionFilterItem(null, "Toutes les options"));

        IEnumerable<EnrollmentClassOptionDto> sectionClasses = _allClasses;
        if (SelectedSection is not null)
        {
            sectionClasses = sectionClasses.Where(c => c.SectionId == SelectedSection.Id);
        }

        var classList = sectionClasses.ToList();
        var organizesOptions = SelectedSection is not null && SectionOrganizesOptions(classList);
        IsStudyOptionFilterEnabled = organizesOptions;

        if (organizesOptions)
        {
            foreach (var option in classList
                         .Select(c => c.StudyOption)
                         .Where(o => !string.IsNullOrWhiteSpace(o))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(o => o))
            {
                StudyOptions.Add(new StudyOptionFilterItem(option, option!));
            }
        }

        SelectedStudyOption = StudyOptions[0];
    }

    /// <summary>
    /// Une section « organise les options » s'il existe plusieurs options distinctes
    /// (ou un mélange option / sans option) parmi ses classes.
    /// </summary>
    private static bool SectionOrganizesOptions(IReadOnlyList<EnrollmentClassOptionDto> classes)
    {
        if (classes.Count == 0)
        {
            return false;
        }

        var options = classes
            .Select(c => c.StudyOption)
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return options.Count > 1
               || (options.Count == 1 && classes.Any(c => string.IsNullOrWhiteSpace(c.StudyOption)));
    }

    private void RefreshClassRooms()
    {
        ClassRooms.Clear();
        IEnumerable<EnrollmentClassOptionDto> query = _allClasses;
        if (SelectedSection is not null)
        {
            query = query.Where(c => c.SectionId == SelectedSection.Id);
        }

        if (IsStudyOptionFilterEnabled && !string.IsNullOrWhiteSpace(SelectedStudyOption?.Value))
        {
            query = query.Where(c =>
                string.Equals(c.StudyOption, SelectedStudyOption.Value, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var room in query.OrderBy(c => c.FullDisplayName))
        {
            ClassRooms.Add(room);
        }

        SelectedClassRoom = null;
    }

    private async Task ReloadInstallmentsAsync()
    {
        Installments.Clear();
        if (SelectedFeeType is null)
        {
            return;
        }

        try
        {
            var items = await _schoolFeeApi.GetFeeTypeInstallmentsAsync(SelectedFeeType.Id);
            foreach (var item in items.OrderBy(i => i.SortOrder).ThenBy(i => i.InstallmentName))
            {
                Installments.Add(new InstallmentCheckItem(item.FeeInstallmentId, item.InstallmentName, item.SortOrder));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
