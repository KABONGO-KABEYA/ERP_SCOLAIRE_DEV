using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class GradeEntryEditItem : ObservableObject
{
    public GradeEntryEditItem(Guid studentId, string studentName, decimal score, bool isAbsent, string? comment)
    {
        StudentId = studentId;
        StudentName = studentName;
        _score = score;
        _isAbsent = isAbsent;
        _comment = comment;
    }

    public Guid StudentId { get; }

    public string StudentName { get; }

    [ObservableProperty]
    private decimal _score;

    [ObservableProperty]
    private bool _isAbsent;

    [ObservableProperty]
    private string? _comment;
}

public partial class GradesViewModel : ViewModelBase
{
    private readonly IGradeApiService _gradeApiService;
    private readonly ISchoolApiService _schoolApiService;

    public GradesViewModel(IGradeApiService gradeApiService, ISchoolApiService schoolApiService)
    {
        _gradeApiService = gradeApiService;
        _schoolApiService = schoolApiService;
        _ = InitializeAsync();
    }

    public ObservableCollection<EvaluationDto> Evaluations { get; } = [];
    public ObservableCollection<PeriodResultDto> PeriodResults { get; } = [];
    public ObservableCollection<GradeEntryEditItem> GradeEntries { get; } = [];

    [ObservableProperty] private SchoolLookupsDto? _lookups;
    [ObservableProperty] private ClassRoomLookupDto? _selectedClass;
    [ObservableProperty] private AcademicPeriodLookupDto? _selectedPeriod;
    [ObservableProperty] private AcademicYearDto? _selectedYear;
    [ObservableProperty] private CourseLookupDto? _selectedCourse;
    [ObservableProperty] private EvaluationDto? _selectedEvaluation;
    [ObservableProperty] private string _evaluationTitle = "Interrogation n°1";
    [ObservableProperty] private EvaluationType _evaluationType = EvaluationType.Interrogation;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public IEnumerable<AcademicPeriodLookupDto> FilteredPeriods =>
        Lookups?.AcademicPeriods.Where(p => SelectedYear is null || p.AcademicYearId == SelectedYear.Id) ?? [];

    public IEnumerable<CourseLookupDto> FilteredCourses =>
        Lookups?.Courses.Where(c => SelectedClass is null || c.ClassRoomId == SelectedClass.Id) ?? [];

    partial void OnSelectedClassChanged(ClassRoomLookupDto? value) =>
        SelectedCourse = FilteredCourses.FirstOrDefault();

    partial void OnSelectedYearChanged(AcademicYearDto? value)
    {
        SelectedPeriod = FilteredPeriods.FirstOrDefault();
        OnPropertyChanged(nameof(FilteredPeriods));
    }

    partial void OnSelectedEvaluationChanged(EvaluationDto? value) => _ = LoadGradeEntriesAsync();

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            Lookups = await _schoolApiService.GetLookupsAsync();
            SelectedYear = Lookups.AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? Lookups.AcademicYears.FirstOrDefault();
            SelectedPeriod = FilteredPeriods.FirstOrDefault();
            SelectedClass = Lookups.ClassRooms.FirstOrDefault();
            SelectedCourse = FilteredCourses.FirstOrDefault();
            await LoadEvaluationsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadEvaluationsAsync()
    {
        if (SelectedClass is null || SelectedPeriod is null) return;
        IsBusy = true;
        try
        {
            var items = await _gradeApiService.GetEvaluationsAsync(SelectedClass.Id, SelectedPeriod.Id);
            Evaluations.Clear();
            foreach (var e in items) Evaluations.Add(e);
            SelectedEvaluation = Evaluations.FirstOrDefault();
            OnPropertyChanged(nameof(FilteredPeriods));
            OnPropertyChanged(nameof(FilteredCourses));
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadGradeEntriesAsync()
    {
        GradeEntries.Clear();
        if (SelectedEvaluation is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var entries = await _gradeApiService.GetGradeEntriesAsync(SelectedEvaluation.Id);
            foreach (var entry in entries)
            {
                GradeEntries.Add(new GradeEntryEditItem(
                    entry.StudentId,
                    entry.StudentName,
                    entry.Score,
                    entry.IsAbsent,
                    entry.Comment));
            }

            StatusMessage = entries.Count == 0
                ? "Aucun élève inscrit pour cette évaluation."
                : $"{entries.Count} note(s) à saisir.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveGradesAsync()
    {
        if (SelectedEvaluation is null)
        {
            StatusMessage = "Sélectionnez une évaluation.";
            return;
        }

        if (GradeEntries.Count == 0)
        {
            StatusMessage = "Aucune note à enregistrer.";
            return;
        }

        IsBusy = true;
        try
        {
            await _gradeApiService.SubmitGradesAsync(new SubmitGradesRequest(
                SelectedEvaluation.Id,
                GradeEntries.Select(g => new GradeEntryInput(g.StudentId, g.Score, g.IsAbsent, g.Comment)).ToList()));

            StatusMessage = "Notes enregistrées.";
            await LoadGradeEntriesAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CreateEvaluationAsync()
    {
        if (SelectedClass is null || SelectedPeriod is null || SelectedYear is null || SelectedCourse is null)
        {
            StatusMessage = "Sélectionnez classe, période et cours.";
            return;
        }

        IsBusy = true;
        try
        {
            await _gradeApiService.CreateEvaluationAsync(new CreateEvaluationRequest(
                SelectedYear.Id,
                SelectedPeriod.Id,
                SelectedCourse.Id,
                SelectedClass.Id,
                EvaluationTitle,
                EvaluationType,
                1,
                20,
                DateOnly.FromDateTime(DateTime.Today)));

            StatusMessage = "Évaluation créée.";
            await LoadEvaluationsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CalculateResultsAsync()
    {
        if (SelectedClass is null || SelectedPeriod is null || SelectedYear is null) return;
        IsBusy = true;
        try
        {
            var results = await _gradeApiService.CalculateResultsAsync(new CalculatePeriodResultsRequest(
                SelectedClass.Id, SelectedYear.Id, SelectedPeriod.Id));
            PeriodResults.Clear();
            foreach (var r in results) PeriodResults.Add(r);
            StatusMessage = $"Résultats calculés pour {results.Count} élève(s).";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
