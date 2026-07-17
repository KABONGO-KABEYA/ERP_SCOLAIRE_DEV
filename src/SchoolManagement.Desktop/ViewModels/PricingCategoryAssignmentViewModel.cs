using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Attribution des catégories tarifaires — données issues de Enrollments.</summary>
public partial class PricingCategoryAssignmentViewModel : ViewModelBase
{
    private readonly IFinanceApiService _financeApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly ISchoolFeeApiService _schoolFeeApi;
    private readonly IEnrollmentWizardApiService _wizardApi;
    private CancellationTokenSource? _searchCts;

    public PricingCategoryAssignmentViewModel(
        IFinanceApiService financeApi,
        ISchoolApiService schoolApi,
        ISchoolFeeApiService schoolFeeApi,
        IEnrollmentWizardApiService wizardApi)
    {
        _financeApi = financeApi;
        _schoolApi = schoolApi;
        _schoolFeeApi = schoolFeeApi;
        _wizardApi = wizardApi;
        StatusMessage = "À l'inscription, chaque élève est affecté à la catégorie « Générale ».";
        _ = InitializeAsync();
    }

    public ObservableCollection<StudentPricingAssignmentDto> Students { get; } = [];
    public ObservableCollection<AcademicYearDto> AcademicYears { get; } = [];
    public ObservableCollection<SectionDto> Sections { get; } = [];
    public ObservableCollection<FeePricingCategoryDto> PricingCategories { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private StudentPricingAssignmentDto? _selectedStudent;
    [ObservableProperty] private AcademicYearDto? _selectedAcademicYear;
    [ObservableProperty] private SectionDto? _selectedSection;
    [ObservableProperty] private FeePricingCategoryDto? _selectedPricingCategoryFilter;
    [ObservableProperty] private FeePricingCategoryDto? _selectedPricingCategoryEdit;
    [ObservableProperty] private bool _isFiltersExpanded = true;
    [ObservableProperty] private int _studentsFoundCount;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _pageSize = 50;
    [ObservableProperty] private int _totalPages = 1;

    public string FiltersHeaderText => $"Filtres de recherche ({StudentsFoundCount})";
    public string FiltersToggleLabel => IsFiltersExpanded ? "Masquer les filtres" : "Afficher les filtres";
    public string PaginationLabel => $"Page {CurrentPage} / {TotalPages}";
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;

    partial void OnIsFiltersExpandedChanged(bool value) => OnPropertyChanged(nameof(FiltersToggleLabel));
    partial void OnStudentsFoundCountChanged(int value) => OnPropertyChanged(nameof(FiltersHeaderText));
    partial void OnCurrentPageChanged(int value) => NotifyPagination();
    partial void OnTotalPagesChanged(int value) => NotifyPagination();
    partial void OnSearchTextChanged(string value) => QueueSearch();

    partial void OnSelectedAcademicYearChanged(AcademicYearDto? value)
    {
        CurrentPage = 1;
        QueueSearch();
    }

    partial void OnSelectedSectionChanged(SectionDto? value)
    {
        CurrentPage = 1;
        QueueSearch();
    }

    partial void OnSelectedPricingCategoryFilterChanged(FeePricingCategoryDto? value)
    {
        CurrentPage = 1;
        QueueSearch();
    }

    partial void OnSelectedStudentChanged(StudentPricingAssignmentDto? value)
    {
        if (value is null)
        {
            return;
        }

        SelectedPricingCategoryEdit = PricingCategories.FirstOrDefault(c => c.Id == value.FeePricingCategoryId);
    }

    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    [RelayCommand]
    private async Task ApplyFiltersAsync() => await SearchAsync();

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();
        SelectedSection = null;
        SelectedPricingCategoryFilter = null;
        CurrentPage = 1;
        await SearchAsync();
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (!CanGoPreviousPage)
        {
            return;
        }

        CurrentPage--;
        await SearchAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!CanGoNextPage)
        {
            return;
        }

        CurrentPage++;
        await SearchAsync();
    }

    [RelayCommand]
    private async Task ChangePricingCategoryAsync(StudentPricingAssignmentDto? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedStudent = item;
        var targetCategory = SelectedPricingCategoryFilter
            ?? SelectedPricingCategoryEdit
            ?? PricingCategories.FirstOrDefault(c => c.Id == item.FeePricingCategoryId);

        if (targetCategory is null)
        {
            StatusMessage = "Aucune catégorie tarifaire disponible.";
            return;
        }

        if (targetCategory.Id == item.FeePricingCategoryId)
        {
            StatusMessage =
                "Sélectionnez d'abord une autre catégorie dans le filtre « Catégorie tarifaire », puis relancez « Modifier la catégorie tarifaire ».";
            return;
        }

        IsBusy = true;
        try
        {
            var updated = await _financeApi.UpdatePricingAssignmentAsync(
                item.EnrollmentId,
                new UpdateEnrollmentPricingCategoryRequest(targetCategory.Id));
            StatusMessage = $"Catégorie « {updated.FeePricingCategoryName} » appliquée à {updated.FullName}.";
            await SearchAsync();
            SelectedStudent = Students.FirstOrDefault(s => s.EnrollmentId == updated.EnrollmentId);
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
    private void ViewCategoryHistory(StudentPricingAssignmentDto? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedStudent = item;
        StatusMessage = $"Historique des changements de catégorie — {item.FullName} (prochaine étape).";
    }

    [RelayCommand]
    private void ViewApplicableFees(StudentPricingAssignmentDto? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedStudent = item;
        StatusMessage =
            $"Frais applicables — {item.FullName} / catégorie {item.FeePricingCategoryName} (consultation barème à venir).";
    }

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

            SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();

            Sections.Clear();
            var structure = await _wizardApi.GetStructureOptionsAsync();
            foreach (var section in structure.Sections.OrderBy(s => s.Name))
            {
                Sections.Add(section);
            }

            PricingCategories.Clear();
            var catalog = await _schoolFeeApi.GetCatalogAsync();
            foreach (var category in catalog.PricingCategories.Where(c => c.IsActive).OrderBy(c => c.Name))
            {
                PricingCategories.Add(category);
            }

            await SearchAsync();
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

    private void QueueSearch()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = DebouncedSearchAsync(token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            if (!token.IsCancellationRequested)
            {
                CurrentPage = 1;
                await SearchAsync();
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }

    private async Task SearchAsync()
    {
        if (SelectedAcademicYear is null)
        {
            Students.Clear();
            StudentsFoundCount = 0;
            StatusMessage = "Sélectionnez une année scolaire.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _financeApi.SearchPricingAssignmentsAsync(new StudentPricingAssignmentSearchRequest(
                SelectedAcademicYear.Id,
                SelectedSection?.Id,
                null,
                null,
                SelectedPricingCategoryFilter?.Id,
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                CurrentPage,
                PageSize));

            Students.Clear();
            foreach (var item in result.Items)
            {
                Students.Add(item);
            }

            StudentsFoundCount = result.TotalCount;
            TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)PageSize));
            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }

            StatusMessage = $"{result.TotalCount} élève(s) trouvé(s).";
            NotifyPagination();
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

    private void NotifyPagination()
    {
        OnPropertyChanged(nameof(PaginationLabel));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
    }
}
