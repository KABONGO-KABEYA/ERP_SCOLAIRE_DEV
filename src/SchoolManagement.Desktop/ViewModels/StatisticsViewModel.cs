using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class StatisticsViewModel : ViewModelBase
{
    private readonly IReportApiService _reportApiService;
    private readonly ISchoolApiService _schoolApiService;

    public StatisticsViewModel(IReportApiService reportApiService, ISchoolApiService schoolApiService)
    {
        _reportApiService = reportApiService;
        _schoolApiService = schoolApiService;
        _ = LoadAsync();
    }

    public ObservableCollection<EnrollmentByClassDto> EnrollmentByClass { get; } = [];
    public ObservableCollection<ClassAverageReportDto> ClassAverages { get; } = [];

    [ObservableProperty] private DashboardStatsDto? _dashboard;
    [ObservableProperty] private FinancialSummaryDto? _financialSummary;
    [ObservableProperty] private IReadOnlyList<AcademicYearDto> _academicYears = [];
    [ObservableProperty] private AcademicYearDto? _selectedYear;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    partial void OnSelectedYearChanged(AcademicYearDto? value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            if (AcademicYears.Count == 0)
            {
                AcademicYears = await _schoolApiService.GetAcademicYearsAsync();
                SelectedYear = AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();
            }

            Dashboard = await _reportApiService.GetDashboardAsync();
            FinancialSummary = await _reportApiService.GetFinancialSummaryAsync(SelectedYear?.Id);

            var enrollment = await _reportApiService.GetEnrollmentByClassAsync(SelectedYear?.Id);
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
