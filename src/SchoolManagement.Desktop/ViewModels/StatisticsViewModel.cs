using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;

namespace SchoolManagement.Desktop.ViewModels;

public partial class StatisticsViewModel : ViewModelBase
{
    private readonly IReportApiService _reportApiService;

    public StatisticsViewModel(IReportApiService reportApiService)
    {
        _reportApiService = reportApiService;
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
        _ = LoadAsync();
    }

    private void OnGlobalAcademicYearChanged() => _ = LoadAsync();

    public ObservableCollection<EnrollmentByClassDto> EnrollmentByClass { get; } = [];
    public ObservableCollection<ClassAverageReportDto> ClassAverages { get; } = [];

    [ObservableProperty] private DashboardStatsDto? _dashboard;
    [ObservableProperty] private FinancialSummaryDto? _financialSummary;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var yearId = AcademicYearRefreshBridge.SelectedYearId;
            if (yearId is null)
            {
                StatusMessage = "Aucune année scolaire sélectionnée (barre du haut).";
                return;
            }

            Dashboard = await _reportApiService.GetDashboardAsync();
            FinancialSummary = await _reportApiService.GetFinancialSummaryAsync(yearId);

            var enrollment = await _reportApiService.GetEnrollmentByClassAsync(yearId);
            EnrollmentByClass.Clear();
            foreach (var item in enrollment) EnrollmentByClass.Add(item);

            var averages = await _reportApiService.GetClassAveragesAsync();
            ClassAverages.Clear();
            foreach (var item in averages) ClassAverages.Add(item);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
