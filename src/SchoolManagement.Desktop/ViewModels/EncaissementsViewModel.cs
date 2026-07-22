using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Helpers;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.Views;
using SchoolManagement.Desktop.Views.Encaissements;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Encaissements — situations de paiement depuis la base de données.</summary>
public partial class EncaissementsViewModel : ViewModelBase
{
    private readonly IFinanceApiService _financeApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly IEnrollmentWizardApiService _wizardApi;
    private readonly ISchoolFeeApiService _schoolFeeApi;
    private readonly IPaymentApiService _paymentApi;
    private readonly IRevenueAllocationApiService _allocationApi;
    private readonly IWithholdingApiService _withholdingApi;
    private readonly IAuthSessionService _authSession;
    private readonly IStudentDossierPathResolver _dossierPathResolver;
    private readonly IFeeTypeStatementPrintService _statementPrint;
    private CancellationTokenSource? _searchCts;
    private bool _suppressSearch;

    private Guid? _defaultFeeTypeId;

    public EncaissementsViewModel(
        IFinanceApiService financeApi,
        ISchoolApiService schoolApi,
        IEnrollmentWizardApiService wizardApi,
        ISchoolFeeApiService schoolFeeApi,
        IPaymentApiService paymentApi,
        IRevenueAllocationApiService allocationApi,
        IWithholdingApiService withholdingApi,
        IAuthSessionService authSession,
        IStudentDossierPathResolver dossierPathResolver,
        IFeeTypeStatementPrintService statementPrint)
    {
        _financeApi = financeApi;
        _schoolApi = schoolApi;
        _wizardApi = wizardApi;
        _schoolFeeApi = schoolFeeApi;
        _paymentApi = paymentApi;
        _allocationApi = allocationApi;
        _withholdingApi = withholdingApi;
        _authSession = authSession;
        _dossierPathResolver = dossierPathResolver;
        _statementPrint = statementPrint;
        StatusMessage = "Chargement des élèves inscrits…";
        PaymentStatuses =
        [
            new PaymentStatusFilterItem(null, "Tous les statuts"),
            new PaymentStatusFilterItem(PaymentSituationStatus.AJour, "À jour"),
            new PaymentStatusFilterItem(PaymentSituationStatus.EnRetard, "En retard"),
            new PaymentStatusFilterItem(PaymentSituationStatus.Impaye, "Impayé"),
            new PaymentStatusFilterItem(PaymentSituationStatus.Credit, "Crédit")
        ];
        SelectedPaymentStatusFilter = PaymentStatuses[0];
        _ = InitializeAsync();
    }

    public ObservableCollection<StudentPaymentSituationDto> Students { get; } = [];
    public ObservableCollection<AcademicYearDto> AcademicYears { get; } = [];
    public ObservableCollection<SectionDto> Sections { get; } = [];
    public ObservableCollection<FeeTypeDto> FeeTypes { get; } = [];
    public IReadOnlyList<PaymentStatusFilterItem> PaymentStatuses { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private StudentPaymentSituationDto? _selectedStudent;
    [ObservableProperty] private AcademicYearDto? _selectedAcademicYear;
    [ObservableProperty] private SectionDto? _selectedSection;
    [ObservableProperty] private FeeTypeDto? _selectedFeeType;
    [ObservableProperty] private PaymentStatusFilterItem? _selectedPaymentStatusFilter;
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
    public bool CanMutatePaidPayments => _authSession.IsAdministrator;

    partial void OnIsFiltersExpandedChanged(bool value) => OnPropertyChanged(nameof(FiltersToggleLabel));
    partial void OnStudentsFoundCountChanged(int value) => OnPropertyChanged(nameof(FiltersHeaderText));
    partial void OnCurrentPageChanged(int value) => NotifyPagination();
    partial void OnTotalPagesChanged(int value) => NotifyPagination();

    partial void OnSearchTextChanged(string value) => QueueSearch();

    partial void OnSelectedAcademicYearChanged(AcademicYearDto? value)
    {
        if (_suppressSearch)
        {
            return;
        }

        CurrentPage = 1;
        QueueSearch();
    }

    partial void OnSelectedSectionChanged(SectionDto? value)
    {
        if (_suppressSearch)
        {
            return;
        }

        CurrentPage = 1;
        QueueSearch();
    }

    partial void OnSelectedFeeTypeChanged(FeeTypeDto? value)
    {
        if (_suppressSearch)
        {
            return;
        }

        CurrentPage = 1;
        QueueSearch();
    }

    partial void OnSelectedPaymentStatusFilterChanged(PaymentStatusFilterItem? value)
    {
        if (_suppressSearch)
        {
            return;
        }

        CurrentPage = 1;
        QueueSearch();
    }

    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    [RelayCommand]
    private async Task ApplyFiltersAsync() => await SearchAsync();

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        _suppressSearch = true;
        try
        {
            SearchText = string.Empty;
            SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();
            SelectedSection = null;
            SelectedFeeType = ResolveDefaultFeeType();
            SelectedPaymentStatusFilter = PaymentStatuses[0];
            CurrentPage = 1;
        }
        finally
        {
            _suppressSearch = false;
        }

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
    private async Task NewPaymentAsync() =>
        await OpenActionAsync(SelectedStudent, EncaissementActionMode.CollectPayment);

    [RelayCommand]
    private async Task PreviewReceiptAsync()
    {
        var student = SelectedStudent;
        if (student is null)
        {
            StatusMessage = "Sélectionnez un élève.";
            MessageBox.Show("Sélectionnez un élève dans la liste.", "Aperçu du reçu", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var feeTypeId = SelectedFeeType?.Id ?? student.FeeTypeId;
        if (feeTypeId is null || feeTypeId == Guid.Empty)
        {
            StatusMessage = "Sélectionnez un type de frais.";
            MessageBox.Show("Sélectionnez un type de frais dans les filtres.", "Aperçu du reçu", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Génération du relevé…";
            await _statementPrint.PreviewForStudentAsync(
                student.StudentId,
                student.AcademicYearId,
                feeTypeId.Value);
            StatusMessage = $"Relevé {SelectedFeeType?.Name ?? student.FeeTypeName} — {student.FullName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Aperçu du reçu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CollectPaymentAsync(StudentPaymentSituationDto? item) =>
        await OpenActionAsync(item, EncaissementActionMode.CollectPayment);

    [RelayCommand]
    private async Task ViewPaymentHistoryAsync(StudentPaymentSituationDto? item) =>
        await OpenActionAsync(item, EncaissementActionMode.PaymentHistory);

    [RelayCommand]
    private async Task ViewFinancialSituationAsync(StudentPaymentSituationDto? item) =>
        await OpenActionAsync(item, EncaissementActionMode.FinancialSituation);

    [RelayCommand]
    private async Task ViewAllocationsAsync(StudentPaymentSituationDto? item) =>
        await OpenActionAsync(item, EncaissementActionMode.Allocations);

    [RelayCommand]
    private async Task ViewWithholdingsAsync(StudentPaymentSituationDto? item) =>
        await OpenActionAsync(item, EncaissementActionMode.Withholdings);

    [RelayCommand]
    private async Task ReprintReceiptAsync(StudentPaymentSituationDto? item)
    {
        var student = item ?? SelectedStudent;
        if (student is null)
        {
            StatusMessage = "Sélectionnez un élève.";
            MessageBox.Show("Sélectionnez un élève dans la liste.", "Aperçu du reçu", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var feeTypeId = SelectedFeeType?.Id ?? student.FeeTypeId;
        if (feeTypeId is null || feeTypeId == Guid.Empty)
        {
            StatusMessage = "Sélectionnez un type de frais.";
            MessageBox.Show("Sélectionnez un type de frais dans les filtres.", "Aperçu du reçu", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Génération du relevé…";
            await _statementPrint.PreviewForStudentAsync(
                student.StudentId,
                student.AcademicYearId,
                feeTypeId.Value);
            StatusMessage = $"Relevé {SelectedFeeType?.Name ?? student.FeeTypeName} — {student.FullName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Aperçu du reçu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditPaymentAsync(StudentPaymentSituationDto? item)
    {
        if (!_authSession.IsAdministrator)
        {
            StatusMessage = "Seul l'administrateur peut modifier un frais déjà payé.";
            MessageBox.Show(StatusMessage, "Modification", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await OpenActionAsync(item, EncaissementActionMode.EditPayment);
    }

    [RelayCommand]
    private async Task CancelPaymentAsync(StudentPaymentSituationDto? item)
    {
        if (!_authSession.IsAdministrator)
        {
            StatusMessage = "Seul l'administrateur peut supprimer un frais déjà payé.";
            MessageBox.Show(StatusMessage, "Annulation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await OpenActionAsync(item, EncaissementActionMode.CancelPayment);
    }

    private async Task OpenActionAsync(StudentPaymentSituationDto? item, EncaissementActionMode mode)
    {
        var student = item ?? SelectedStudent;
        if (student is null)
        {
            StatusMessage = "Sélectionnez un élève.";
            return;
        }

        SelectedStudent = student;

        if (mode == EncaissementActionMode.CollectPayment && SelectedFeeType is null)
        {
            StatusMessage = "Sélectionnez un type de frais avant d'encaisser.";
            return;
        }

        if (mode == EncaissementActionMode.CollectPayment && student.FeeTypeId is null)
        {
            StatusMessage = "Aucun type de frais associé à cet élève.";
            return;
        }

        try
        {
            if (mode == EncaissementActionMode.CollectPayment)
            {
                var collectDialog = new CollectPaymentWindow(
                    student,
                    _paymentApi,
                    _financeApi,
                    _authSession,
                    _dossierPathResolver,
                    _statementPrint)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                var collectResult = collectDialog.ShowDialog();
                if (collectResult == true || collectDialog.NeedsRefresh)
                {
                    StatusMessage = $"Paiement enregistré pour {student.FullName}.";
                    await SearchAsync();
                }

                return;
            }

            var dialog = new EncaissementActionWindow(
                mode,
                student,
                _paymentApi,
                _financeApi,
                _allocationApi,
                _withholdingApi,
                _schoolApi,
                _statementPrint,
                _authSession)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var result = dialog.ShowDialog();
            if (result == true || dialog.NeedsRefresh)
            {
                StatusMessage = mode switch
                {
                    EncaissementActionMode.CancelPayment => $"Paiement annulé — {student.FullName}.",
                    _ => StatusMessage
                };
                await SearchAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        _suppressSearch = true;
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

            FeeTypes.Clear();
            var catalog = await _schoolFeeApi.GetCatalogAsync();
            foreach (var feeType in catalog.FeeTypes.Where(f => f.IsActive).OrderBy(f => f.Name))
            {
                FeeTypes.Add(feeType);
            }

            var school = await _schoolApi.GetCurrentSchoolAsync();
            _defaultFeeTypeId = school?.DefaultFeeTypeId;

            SelectedFeeType = ResolveDefaultFeeType();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            _suppressSearch = false;
            IsBusy = false;
        }

        await SearchAsync();
    }

    private FeeTypeDto? ResolveDefaultFeeType() =>
        DefaultFeeTypeHelper.Resolve(FeeTypes, _defaultFeeTypeId);

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

        if (SelectedFeeType is null)
        {
            Students.Clear();
            StudentsFoundCount = 0;
            StatusMessage = FeeTypes.Count == 0
                ? "Aucun type de frais configuré. Créez « Frais scolaire » dans Paramètres → Frais scolaires."
                : "Sélectionnez un type de frais.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _financeApi.SearchPaymentSituationsAsync(new StudentPaymentSituationSearchRequest(
                SelectedAcademicYear.Id,
                SelectedSection?.Id,
                null,
                null,
                null,
                SelectedFeeType.Id,
                SelectedPaymentStatusFilter?.Status,
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

            StatusMessage = $"{result.TotalCount} élève(s) — {SelectedFeeType.Name}.";
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

public sealed record PaymentStatusFilterItem(PaymentSituationStatus? Status, string Label);
