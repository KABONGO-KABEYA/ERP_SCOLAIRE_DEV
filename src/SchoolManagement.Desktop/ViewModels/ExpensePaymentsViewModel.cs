using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Accounting.DTOs;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Enregistrement des dépenses imputées sur les comptes bénéficiaires.</summary>
public partial class ExpensePaymentsViewModel : ViewModelBase
{
    private readonly IAccountingApiService _accountingApi;
    private readonly IRevenueAllocationApiService _allocationApi;
    private readonly ISchoolApiService _schoolApi;

    public ExpensePaymentsViewModel(
        IAccountingApiService accountingApi,
        IRevenueAllocationApiService allocationApi,
        ISchoolApiService schoolApi)
    {
        _accountingApi = accountingApi;
        _allocationApi = allocationApi;
        _schoolApi = schoolApi;
        ExpenseDate = DateOnly.FromDateTime(DateTime.Today);
        StatusMessage = "Enregistrez les dépenses effectuées pendant la période.";
        _ = InitializeAsync();
    }

    public ObservableCollection<ExpensePaymentDto> Payments { get; } = [];
    public ObservableCollection<AcademicYearDto> AcademicYears { get; } = [];
    public ObservableCollection<RevenueDestinationDto> Destinations { get; } = [];

    [ObservableProperty] private AcademicYearDto? _selectedAcademicYear;
    [ObservableProperty] private RevenueDestinationDto? _selectedDestination;
    [ObservableProperty] private string _newLabel = string.Empty;
    [ObservableProperty] private decimal _newAmount;
    [ObservableProperty] private DateOnly _expenseDate;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    partial void OnSelectedAcademicYearChanged(AcademicYearDto? value) => _ = SearchAsync();

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var years = await _schoolApi.GetAcademicYearsAsync();
            AcademicYears.Clear();
            foreach (var year in years)
            {
                AcademicYears.Add(year);
            }

            SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();

            var destinations = await _allocationApi.GetDestinationsAsync(activeOnly: true);
            Destinations.Clear();
            foreach (var destination in destinations)
            {
                Destinations.Add(destination);
            }

            SelectedDestination = Destinations.FirstOrDefault();
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

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (SelectedAcademicYear is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _accountingApi.SearchExpensePaymentsAsync(new ExpenseSearchRequest(
                SelectedAcademicYear.Id,
                PageSize: 200));
            Payments.Clear();
            foreach (var item in result.Items)
            {
                Payments.Add(item);
            }

            StatusMessage = $"{result.TotalCount} dépense(s) trouvée(s).";
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
    private async Task CreateAsync()
    {
        if (SelectedAcademicYear is null || SelectedDestination is null)
        {
            StatusMessage = "Sélectionnez l'année scolaire et le compte bénéficiaire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewLabel) || NewAmount <= 0)
        {
            StatusMessage = "Renseignez le libellé et un montant valide.";
            return;
        }

        IsBusy = true;
        try
        {
            await _accountingApi.CreateExpensePaymentAsync(new CreateExpensePaymentRequest(
                SelectedAcademicYear.Id,
                SelectedDestination.Id,
                NewLabel.Trim(),
                NewAmount,
                Currency.CDF,
                ExpenseDate));
            NewLabel = string.Empty;
            NewAmount = 0;
            await SearchAsync();
            StatusMessage = "Dépense enregistrée.";
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
}
