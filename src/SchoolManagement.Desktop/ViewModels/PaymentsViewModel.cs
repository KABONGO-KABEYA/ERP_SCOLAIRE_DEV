using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class PaymentsViewModel : ViewModelBase
{
    private readonly IPaymentApiService _paymentApiService;
    private readonly IStudentApiService _studentApiService;
    private readonly ISchoolApiService _schoolApiService;

    public PaymentsViewModel(
        IPaymentApiService paymentApiService,
        IStudentApiService studentApiService,
        ISchoolApiService schoolApiService)
    {
        _paymentApiService = paymentApiService;
        _studentApiService = studentApiService;
        _schoolApiService = schoolApiService;
        _ = InitializeAsync();
    }

    public ObservableCollection<PaymentDto> Payments { get; } = [];
    public ObservableCollection<StudentDto> Students { get; } = [];

    [ObservableProperty] private SchoolLookupsDto? _lookups;
    [ObservableProperty] private StudentDto? _selectedStudent;
    [ObservableProperty] private FeeTypeLookupDto? _selectedFeeType;

    partial void OnSelectedFeeTypeChanged(FeeTypeLookupDto? value)
    {
        if (value is not null)
        {
            Currency = value.Currency;
        }
    }

    [ObservableProperty] private CashRegisterLookupDto? _selectedCashRegister;
    [ObservableProperty] private AcademicYearDto? _selectedYear;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private Currency _currency = Currency.CDF;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            Lookups = await _schoolApiService.GetLookupsAsync();
            SelectedYear = Lookups.AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? Lookups.AcademicYears.FirstOrDefault();
            SelectedCashRegister = Lookups.CashRegisters.FirstOrDefault();
            SelectedFeeType = Lookups.FeeTypes.FirstOrDefault();

            var students = await _studentApiService.SearchAsync(new StudentSearchRequest(
                null, null, null, null, null, null,
                ApplyFilters: false, IncludeAll: true, Page: 1, PageSize: 100));
            Students.Clear();
            foreach (var s in students.Items) Students.Add(s);
            SelectedStudent = Students.FirstOrDefault();

            await LoadPaymentsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadPaymentsAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _paymentApiService.SearchAsync(new PaymentSearchRequest(SelectedStudent?.Id, null, null, 1, 50));
            Payments.Clear();
            foreach (var p in result.Items) Payments.Add(p);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RecordPaymentAsync()
    {
        if (SelectedStudent is null || SelectedFeeType is null || SelectedCashRegister is null || SelectedYear is null || Amount <= 0)
        {
            StatusMessage = "Complétez tous les champs du paiement.";
            return;
        }

        IsBusy = true;
        try
        {
            await _paymentApiService.CreateAsync(new CreatePaymentRequest(
                SelectedStudent.Id,
                SelectedYear.Id,
                SelectedCashRegister.Id,
                null,
                Currency,
                "Cash",
                null,
                [new PaymentLineRequest(SelectedFeeType.Id, Amount, Currency, SelectedFeeType.Name)]));

            StatusMessage = "Paiement enregistré.";
            Amount = 0;
            await LoadPaymentsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
