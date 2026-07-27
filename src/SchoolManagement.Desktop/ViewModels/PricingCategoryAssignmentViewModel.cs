using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Attribution des catégories tarifaires — données issues de Enrollments.</summary>
public partial class PricingCategoryAssignmentViewModel : ViewModelBase
{
    private readonly IFinanceApiService _financeApi;
    private readonly ISchoolFeeApiService _schoolFeeApi;
    private readonly IEnrollmentWizardApiService _wizardApi;
    private readonly IAuthSessionService _authSession;
    private CancellationTokenSource? _searchCts;

    public PricingCategoryAssignmentViewModel(
        IFinanceApiService financeApi,
        ISchoolFeeApiService schoolFeeApi,
        IEnrollmentWizardApiService wizardApi,
        IAuthSessionService authSession)
    {
        _financeApi = financeApi;
        _schoolFeeApi = schoolFeeApi;
        _wizardApi = wizardApi;
        _authSession = authSession;
        StatusMessage = "À l'inscription, chaque élève est affecté à la catégorie « Générale ».";
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
        _ = InitializeAsync();
    }

    private void OnGlobalAcademicYearChanged()
    {
        CurrentPage = 1;
        QueueSearch();
    }

    public bool CanAssignPricingCategory => _authSession.IsAdministrator;

    public ObservableCollection<StudentPricingAssignmentDto> Students { get; } = [];
    public ObservableCollection<SectionDto> Sections { get; } = [];
    public ObservableCollection<FeePricingCategoryDto> PricingCategories { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private StudentPricingAssignmentDto? _selectedStudent;
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

        if (!_authSession.IsAdministrator)
        {
            StatusMessage = "Seul l'administrateur peut attribuer ou modifier la catégorie tarifaire d'un élève.";
            return;
        }

        SelectedStudent = item;
        var dialog = new ChangePricingCategoryWindow(item, PricingCategories.ToList(), _financeApi)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        StatusMessage = dialog.UpdatedAssignment is null
            ? "Catégorie tarifaire mise à jour."
            : $"Catégorie « {dialog.UpdatedAssignment.FeePricingCategoryName} » appliquée à {dialog.UpdatedAssignment.FullName}.";
        await SearchAsync();
        if (dialog.UpdatedAssignment is not null)
        {
            SelectedStudent = Students.FirstOrDefault(s => s.EnrollmentId == dialog.UpdatedAssignment.EnrollmentId);
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
        var dialog = new PricingCategoryHistoryWindow(item, _financeApi)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ViewApplicableFees(StudentPricingAssignmentDto? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedStudent = item;
        var dialog = new ApplicableFeesWindow(item, _financeApi)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
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
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null)
        {
            Students.Clear();
            StudentsFoundCount = 0;
            StatusMessage = "Aucune année scolaire sélectionnée (barre du haut).";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _financeApi.SearchPricingAssignmentsAsync(new StudentPricingAssignmentSearchRequest(
                yearId.Value,
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
