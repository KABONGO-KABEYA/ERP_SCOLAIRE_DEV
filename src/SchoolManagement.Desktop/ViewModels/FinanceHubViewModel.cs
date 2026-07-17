using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public enum FinanceSection
{
    Encaissements = 0,
    CategoriesTarifaires = 1,
    Rapports = 2,
    Consultation = 3
}

/// <summary>Module Financier : encaissements, catégories tarifaires, rapports (config dans Paramètres).</summary>
public partial class FinanceHubViewModel : ViewModelBase
{
    private readonly IRevenueAllocationApiService _allocationApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly IStudentApiService _studentApi;

    public FinanceHubViewModel(
        EncaissementsViewModel encaissements,
        PricingCategoryAssignmentViewModel pricingCategoryAssignment,
        FinancialReportsViewModel financialReports,
        IRevenueAllocationApiService allocationApi,
        ISchoolApiService schoolApi,
        IStudentApiService studentApi)
    {
        Encaissements = encaissements;
        PricingCategoryAssignment = pricingCategoryAssignment;
        FinancialReports = financialReports;
        _allocationApi = allocationApi;
        _schoolApi = schoolApi;
        _studentApi = studentApi;
        ApplyNavigation(FinanceNavCatalog.DefaultItem);
        _ = InitializeAsync();
    }

    public EncaissementsViewModel Encaissements { get; }

    public PricingCategoryAssignmentViewModel PricingCategoryAssignment { get; }

    public FinancialReportsViewModel FinancialReports { get; }

    public ObservableCollection<RevenueDestinationDto> Destinations { get; } = [];
    public ObservableCollection<RevenueAllocationEntryDto> Entries { get; } = [];
    public ObservableCollection<DestinationTotalDto> DestinationTotals { get; } = [];
    public ObservableCollection<FeeTypeTotalDto> FeeTypeTotals { get; } = [];
    public ObservableCollection<AcademicYearDto> AcademicYears { get; } = [];
    public ObservableCollection<StudentDto> Students { get; } = [];

    [ObservableProperty] private FinanceSection _selectedSection = FinanceSection.Encaissements;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private AcademicYearDto? _filterYear;
    [ObservableProperty] private DateTime? _filterFromDate;
    [ObservableProperty] private DateTime? _filterToDate;
    [ObservableProperty] private StudentDto? _filterStudent;
    [ObservableProperty] private RevenueDestinationDto? _filterDestination;
    [ObservableProperty] private decimal _grandTotal;

    public bool IsEncaissementsSelected => SelectedSection == FinanceSection.Encaissements;
    public bool IsCategoriesTarifairesSelected => SelectedSection == FinanceSection.CategoriesTarifaires;
    public bool IsRapportsSelected => SelectedSection == FinanceSection.Rapports;
    public bool IsConsultationSelected => SelectedSection == FinanceSection.Consultation;

    public string? ActiveNavKey { get; private set; }

    public string SelectedSectionTitle => FinanceNavCatalog.FindByKey(ActiveNavKey ?? string.Empty)?.Title
        ?? "Encaissements";

    public string SelectedSectionDescription => FinanceNavCatalog.FindByKey(ActiveNavKey ?? string.Empty)?.Subtitle
        ?? "Suivi des paiements scolaires et opérations d'encaissement";

    public void ApplyNavigation(FinanceNavItem item)
    {
        ActiveNavKey = item.Key;
        SelectedSection = item.Section;
        OnPropertyChanged(nameof(ActiveNavKey));
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));
    }

    partial void OnSelectedSectionChanged(FinanceSection value)
    {
        OnPropertyChanged(nameof(IsEncaissementsSelected));
        OnPropertyChanged(nameof(IsCategoriesTarifairesSelected));
        OnPropertyChanged(nameof(IsRapportsSelected));
        OnPropertyChanged(nameof(IsConsultationSelected));
        if (value == FinanceSection.Consultation)
        {
            _ = SearchEntriesAsync();
        }
    }

    [RelayCommand]
    private void SelectSection(string? section)
    {
        if (Enum.TryParse<FinanceSection>(section, out var parsed))
        {
            SelectedSection = parsed;
            var item = FinanceNavCatalog.Groups
                .SelectMany(g => g.Items)
                .FirstOrDefault(i => i.Section == parsed);
            if (item is not null)
            {
                ApplyNavigation(item);
            }
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            AcademicYears.Clear();
            foreach (var year in await _schoolApi.GetAcademicYearsAsync())
            {
                AcademicYears.Add(year);
            }

            FilterYear = AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();

            Destinations.Clear();
            foreach (var destination in await _allocationApi.GetDestinationsAsync(activeOnly: true))
            {
                Destinations.Add(destination);
            }

            var students = await _studentApi.SearchAsync(new StudentSearchRequest(
                null, null, null, null, null, null,
                ApplyFilters: false, IncludeAll: true, Page: 1, PageSize: 200));
            Students.Clear();
            foreach (var student in students.Items)
            {
                Students.Add(student);
            }
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
    private async Task SearchEntriesAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _allocationApi.SearchEntriesAsync(new RevenueAllocationSearchRequest(
                FilterYear?.Id,
                FilterFromDate is null ? null : DateOnly.FromDateTime(FilterFromDate.Value),
                FilterToDate is null ? null : DateOnly.FromDateTime(FilterToDate.Value),
                FilterStudent?.Id,
                null,
                FilterDestination?.Id,
                null,
                Page: 1,
                PageSize: 200));

            Entries.Clear();
            foreach (var item in result.Items)
            {
                Entries.Add(item);
            }

            DestinationTotals.Clear();
            foreach (var total in result.Totals.ByDestination)
            {
                DestinationTotals.Add(total);
            }

            FeeTypeTotals.Clear();
            foreach (var total in result.Totals.ByFeeType)
            {
                FeeTypeTotals.Add(total);
            }

            GrandTotal = result.Totals.GrandTotal;
            StatusMessage = $"{result.TotalCount} ligne(s) — total {GrandTotal:N2}";
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
    private async Task ExportExcelAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "repartition-recettes.xlsx",
                Filter = "Excel|*.xlsx"
            };
            if (ErpFileDialog.ShowSave(dialog) != true)
            {
                return;
            }

            var bytes = await _allocationApi.ExportExcelAsync(BuildExportRequest());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            StatusMessage = $"Export Excel enregistré : {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "repartition-recettes.pdf",
                Filter = "PDF|*.pdf"
            };
            if (ErpFileDialog.ShowSave(dialog) != true)
            {
                return;
            }

            var bytes = await _allocationApi.ExportPdfAsync(BuildExportRequest());
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            StatusMessage = $"Export PDF enregistré : {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private RevenueAllocationSearchRequest BuildExportRequest() =>
        new(
            FilterYear?.Id,
            FilterFromDate is null ? null : DateOnly.FromDateTime(FilterFromDate.Value),
            FilterToDate is null ? null : DateOnly.FromDateTime(FilterToDate.Value),
            FilterStudent?.Id,
            null,
            FilterDestination?.Id,
            null);
}

public partial class KeyDetailEditorRow : ObservableObject
{
    [ObservableProperty] private Guid _destinationId;
    [ObservableProperty] private string _destinationCode = string.Empty;
    [ObservableProperty] private string _destinationName = string.Empty;
    [ObservableProperty] private AllocationCalculationType _calculationType = AllocationCalculationType.Pourcentage;
    [ObservableProperty] private decimal _value;
    [ObservableProperty] private int _sortOrder;
}
