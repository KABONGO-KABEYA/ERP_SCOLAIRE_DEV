using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Accounting.DTOs;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Demandes de paiement — workflow comptable.</summary>
public partial class ExpenseRequestsViewModel : ViewModelBase
{
    private readonly IAccountingApiService _accountingApi;
    private readonly IRevenueAllocationApiService _allocationApi;

    public ExpenseRequestsViewModel(
        IAccountingApiService accountingApi,
        IRevenueAllocationApiService allocationApi)
    {
        _accountingApi = accountingApi;
        _allocationApi = allocationApi;
        RequestDate = DateOnly.FromDateTime(DateTime.Today);
        StatusMessage = "Consultez et gérez les demandes de paiement.";
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
        _ = InitializeAsync();
    }

    private void OnGlobalAcademicYearChanged() => _ = SearchAsync();

    public ObservableCollection<ExpenseRequestDto> Requests { get; } = [];
    public ObservableCollection<RevenueDestinationDto> Destinations { get; } = [];

    [ObservableProperty] private RevenueDestinationDto? _selectedDestination;
    [ObservableProperty] private ExpenseRequestDto? _selectedRequest;
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private string? _newDescription;
    [ObservableProperty] private decimal _newAmount;
    [ObservableProperty] private DateOnly _requestDate;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public bool CanSubmit => SelectedRequest?.Status == ExpenseRequestStatus.Brouillon;
    public bool CanApprove => SelectedRequest?.Status == ExpenseRequestStatus.Soumise;

    partial void OnSelectedRequestChanged(ExpenseRequestDto? value)
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(CanApprove));
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
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
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null)
        {
            StatusMessage = "Aucune année scolaire sélectionnée (barre du haut).";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _accountingApi.SearchExpenseRequestsAsync(new ExpenseSearchRequest(
                yearId.Value,
                PageSize: 200));
            Requests.Clear();
            foreach (var item in result.Items)
            {
                Requests.Add(item);
            }

            StatusMessage = $"{result.TotalCount} demande(s) trouvée(s).";
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
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null || SelectedDestination is null)
        {
            StatusMessage = "Sélectionnez l'année scolaire (barre du haut) et le compte bénéficiaire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewTitle) || NewAmount <= 0)
        {
            StatusMessage = "Renseignez l'objet et un montant valide.";
            return;
        }

        IsBusy = true;
        try
        {
            await _accountingApi.CreateExpenseRequestAsync(new CreateExpenseRequestRequest(
                yearId.Value,
                SelectedDestination.Id,
                NewTitle.Trim(),
                NewDescription?.Trim(),
                NewAmount,
                Currency.CDF,
                RequestDate));
            NewTitle = string.Empty;
            NewDescription = null;
            NewAmount = 0;
            await SearchAsync();
            StatusMessage = "Demande créée en brouillon.";
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
    private async Task SubmitAsync()
    {
        if (SelectedRequest is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _accountingApi.SubmitExpenseRequestAsync(SelectedRequest.Id);
            await SearchAsync();
            StatusMessage = "Demande soumise.";
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
    private async Task ApproveAsync()
    {
        if (SelectedRequest is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _accountingApi.ApproveExpenseRequestAsync(SelectedRequest.Id);
            await SearchAsync();
            StatusMessage = "Demande approuvée.";
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
