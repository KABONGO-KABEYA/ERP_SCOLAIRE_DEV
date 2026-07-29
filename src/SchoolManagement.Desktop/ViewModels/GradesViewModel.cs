using System.Collections.ObjectModel;

using System.Globalization;

using System.IO;

using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using Microsoft.Win32;

using SchoolManagement.Application.CourseConfiguration.DTOs;

using SchoolManagement.Application.Grades.DTOs;

using SchoolManagement.Application.Schools.DTOs;

using SchoolManagement.Desktop.Services;



namespace SchoolManagement.Desktop.ViewModels;



public partial class GradeEntryEditItem : ObservableObject

{

    private readonly Action? _onChanged;

    private readonly int _maxScore;



    public GradeEntryEditItem(

        int rowNumber,

        Guid studentId,

        string registrationNumber,

        string studentName,

        decimal? score,

        string? comment,

        int maxScore,

        Action? onChanged)

    {

        RowNumber = rowNumber;

        StudentId = studentId;

        RegistrationNumber = registrationNumber;

        StudentName = studentName;

        _maxScore = maxScore;

        _onChanged = onChanged;

        _score = score;

        _comment = comment;

    }



    public int RowNumber { get; }



    public Guid StudentId { get; }



    public string RegistrationNumber { get; }



    public string StudentName { get; }



    [ObservableProperty]

    private decimal? _score;



    [ObservableProperty]

    private string? _comment;



    public string ScoreDisplay =>

        Score?.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;



    partial void OnScoreChanged(decimal? value)

    {

        if (value is < 0)

        {

            Score = 0;

            return;

        }



        if (value > _maxScore)

        {

            Score = _maxScore;

            return;

        }



        OnPropertyChanged(nameof(ScoreDisplay));

        _onChanged?.Invoke();

    }



    partial void OnCommentChanged(string? value) => _onChanged?.Invoke();



    public GradeEntryInput ToInput() =>

        new(StudentId, Score ?? 0, false, Comment);

}



public partial class GradesViewModel : ViewModelBase

{

    private readonly IGradeApiService _gradeApiService;

    private readonly ISchoolApiService _schoolApiService;

    private readonly IAcademicApiService _academicApiService;

    private readonly ICourseConfigurationApiService _courseConfigurationApiService;



    public GradesViewModel(

        IGradeApiService gradeApiService,

        ISchoolApiService schoolApiService,

        IAcademicApiService academicApiService,

        ICourseConfigurationApiService courseConfigurationApiService)

    {

        _gradeApiService = gradeApiService;

        _schoolApiService = schoolApiService;

        _academicApiService = academicApiService;

        _courseConfigurationApiService = courseConfigurationApiService;

        _ = InitializeAsync();

    }



    public ObservableCollection<GradeEntryEditItem> GradeEntries { get; } = [];

    public ObservableCollection<EvaluationTypeDto> EvaluationTypes { get; } = [];

    public ObservableCollection<PedagogicalClassDto> PedagogicalClasses { get; } = [];

    public ObservableCollection<ClassLocalDto> ClassLocals { get; } = [];

    public ObservableCollection<CourseConfigurationItemDto> AssignedCourses { get; } = [];



    [ObservableProperty] private SchoolLookupsDto? _lookups;

    [ObservableProperty] private AcademicYearDto? _selectedYear;

    [ObservableProperty] private AcademicPeriodLookupDto? _selectedPeriod;

    [ObservableProperty] private PedagogicalClassDto? _selectedPedagogicalClass;

    [ObservableProperty] private ClassLocalDto? _selectedLocal;

    [ObservableProperty] private CourseConfigurationItemDto? _selectedCourse;

    [ObservableProperty] private EvaluationTypeDto? _selectedEvaluationType;

    [ObservableProperty] private EvaluationDto? _currentEvaluation;

    [ObservableProperty] private DateTime _evaluationDate = DateTime.Today;

    [ObservableProperty] private string _evaluationTitle = "Interrogation n°1";

    [ObservableProperty] private decimal _evaluationCoefficient = 1;

    [ObservableProperty] private int _evaluationMaxScore = 20;

    [ObservableProperty] private string _teacherDisplayName = "—";

    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private bool _isGridLoaded;

    [ObservableProperty] private bool _isParametersExpanded = true;



    public string ParametersHeaderText => StatTotalStudents > 0

        ? $"Paramètres de l'évaluation ({StatTotalStudents} élève{(StatTotalStudents > 1 ? "s" : "")})"

        : "Paramètres de l'évaluation";



    public string ParametersToggleLabel => IsParametersExpanded ? "Masquer les paramètres" : "Afficher les paramètres";



    partial void OnIsParametersExpandedChanged(bool value) => OnPropertyChanged(nameof(ParametersToggleLabel));



    partial void OnStatTotalStudentsChanged(int value) => OnPropertyChanged(nameof(ParametersHeaderText));



    [RelayCommand]

    private void ToggleParameters() => IsParametersExpanded = !IsParametersExpanded;



    [ObservableProperty] private int _statTotalStudents;

    [ObservableProperty] private int _statGraded;

    [ObservableProperty] private int _statNotGraded;

    [ObservableProperty] private decimal? _statAverage;

    [ObservableProperty] private decimal? _statMaxScore;

    [ObservableProperty] private decimal? _statMinScore;



    public IEnumerable<AcademicPeriodLookupDto> FilteredPeriods =>

        Lookups?.AcademicPeriods.Where(p => SelectedYear is null || p.AcademicYearId == SelectedYear.Id) ?? [];



    public string SummaryClassName => SelectedPedagogicalClass?.DisplayName ?? "—";

    public string SummaryLocalName => SelectedLocal?.FullDisplayName ?? "—";

    public string SummaryCourseName => SelectedCourse?.CourseName ?? "—";

    public string SummaryTeacherName => TeacherDisplayName;

    public string SummaryPeriodName => SelectedPeriod?.Name ?? "—";

    public string SummaryWorkTypeName => SelectedEvaluationType?.Name ?? EvaluationTitle;

    public string SummaryDate => EvaluationDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

    public string SummaryMaxScoreLabel => $"{EvaluationMaxScore} points";

    public string SummaryCoefficientLabel => EvaluationCoefficient.ToString("0.##", CultureInfo.CurrentCulture);

    public string SummaryStudentCountLabel => StatTotalStudents.ToString(CultureInfo.CurrentCulture);



    public bool CanEditGrades => IsGridLoaded && CurrentEvaluation is not null && CurrentEvaluation.IsOpen;



    partial void OnSelectedYearChanged(AcademicYearDto? value)

    {

        SelectedPeriod = FilteredPeriods.FirstOrDefault();

        OnPropertyChanged(nameof(FilteredPeriods));

        _ = ReloadLocalsAndCoursesAsync();

        NotifyCommands();

    }



    partial void OnSelectedPeriodChanged(AcademicPeriodLookupDto? value)
    {
        NotifySummary();
        _ = TryAutoLoadStudentsAsync();
    }



    partial void OnSelectedPedagogicalClassChanged(PedagogicalClassDto? value) =>

        _ = ReloadLocalsAndCoursesAsync();



    partial void OnSelectedLocalChanged(ClassLocalDto? value) =>

        _ = HandleSelectedLocalChangedAsync();



    private async Task HandleSelectedLocalChangedAsync()
    {
        await ReloadCoursesAsync();
        NotifySummary();
        await TryAutoLoadStudentsAsync();
    }



    partial void OnSelectedCourseChanged(CourseConfigurationItemDto? value)

    {

        TeacherDisplayName = value?.TeacherName ?? "—";

        if (value is not null && value.MaxPerPeriod > 0)

        {

            EvaluationMaxScore = value.MaxPerPeriod;

        }



        NotifySummary();

        NotifyCommands();

        _ = TryAutoLoadStudentsAsync();

    }



    partial void OnSelectedEvaluationTypeChanged(EvaluationTypeDto? value)
    {
        NotifySummary();
        _ = TryAutoLoadStudentsAsync();
    }



    partial void OnEvaluationDateChanged(DateTime value) => NotifySummary();



    partial void OnEvaluationMaxScoreChanged(int value)

    {

        NotifySummary();

        foreach (var entry in GradeEntries)

        {

            if (entry.Score > value)

            {

                entry.Score = value;

            }

        }

    }



    partial void OnEvaluationCoefficientChanged(decimal value) => NotifySummary();



    partial void OnIsBusyChanged(bool value) => NotifyCommands();



    partial void OnIsGridLoadedChanged(bool value)

    {

        OnPropertyChanged(nameof(CanEditGrades));

        NotifyCommands();

    }



    private void NotifyCommands()

    {

        LoadStudentsCommand.NotifyCanExecuteChanged();

        SaveGradesCommand.NotifyCanExecuteChanged();

        SaveAndCloseGradesCommand.NotifyCanExecuteChanged();

        CancelGradesCommand.NotifyCanExecuteChanged();

        PrintGridCommand.NotifyCanExecuteChanged();

        ExportExcelCommand.NotifyCanExecuteChanged();

    }



    private void NotifySummary()

    {

        OnPropertyChanged(nameof(SummaryClassName));

        OnPropertyChanged(nameof(SummaryLocalName));

        OnPropertyChanged(nameof(SummaryCourseName));

        OnPropertyChanged(nameof(SummaryTeacherName));

        OnPropertyChanged(nameof(SummaryPeriodName));

        OnPropertyChanged(nameof(SummaryWorkTypeName));

        OnPropertyChanged(nameof(SummaryDate));

        OnPropertyChanged(nameof(SummaryMaxScoreLabel));

        OnPropertyChanged(nameof(SummaryCoefficientLabel));

        OnPropertyChanged(nameof(SummaryStudentCountLabel));

    }



    public void RefreshStatistics()

    {

        StatTotalStudents = GradeEntries.Count;

        StatGraded = GradeEntries.Count(e => e.Score.HasValue);

        StatNotGraded = GradeEntries.Count(e => !e.Score.HasValue);



        var scored = GradeEntries

            .Where(e => e.Score.HasValue)

            .Select(e => e.Score!.Value)

            .ToList();



        StatAverage = scored.Count == 0 ? null : Math.Round(scored.Average(), 2);

        StatMaxScore = scored.Count == 0 ? null : scored.Max();

        StatMinScore = scored.Count == 0 ? null : scored.Min();

        NotifySummary();

    }



    private bool CanLoadStudentsFromLocal() =>

        !IsBusy

        && SelectedYear is not null

        && SelectedPeriod is not null

        && SelectedLocal is not null;



    private bool CanLoadStudentsWithGrades() =>

        CanLoadStudentsFromLocal()

        && SelectedCourse is not null

        && SelectedEvaluationType is not null;



    private bool CanLoadStudents() => CanLoadStudentsFromLocal();



    private bool CanSaveGrades() => CanEditGrades && GradeEntries.Count > 0 && !IsBusy;



    [RelayCommand]

    private async Task InitializeAsync()

    {

        IsBusy = true;

        try

        {

            Lookups = await _schoolApiService.GetLookupsAsync();

            var types = await _gradeApiService.GetEvaluationTypesAsync();

            EvaluationTypes.Clear();

            foreach (var type in types)

            {

                EvaluationTypes.Add(type);

            }



            var classes = await _schoolApiService.GetPedagogicalClassesAsync();

            PedagogicalClasses.Clear();

            foreach (var pedagogicalClass in classes.Where(c => c.IsEnabled).OrderBy(c => c.LevelOrder))

            {

                PedagogicalClasses.Add(pedagogicalClass);

            }



            SelectedEvaluationType = EvaluationTypes.FirstOrDefault(t => t.Code == "INTERRO") ?? EvaluationTypes.FirstOrDefault();

            SelectedYear = Lookups.AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? Lookups.AcademicYears.FirstOrDefault();

            SelectedPeriod = FilteredPeriods.FirstOrDefault();

            SelectedPedagogicalClass = PedagogicalClasses.FirstOrDefault();

            await ReloadLocalsAndCoursesAsync();

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



    private async Task ReloadLocalsAndCoursesAsync()

    {

        ClassLocals.Clear();

        AssignedCourses.Clear();

        SelectedLocal = null;

        SelectedCourse = null;

        TeacherDisplayName = "—";



        if (SelectedPedagogicalClass is null || SelectedYear is null)

        {

            return;

        }



        try

        {

            var locals = await _schoolApiService.GetClassLocalsAsync(SelectedPedagogicalClass.Id, SelectedYear.Id);

            foreach (var local in locals.Where(l => l.IsActive).OrderBy(l => l.LocalName))

            {

                ClassLocals.Add(local);

            }



            SelectedLocal = ClassLocals.FirstOrDefault();

        }

        catch (Exception ex)

        {

            StatusMessage = ex.Message;

        }

    }



    private async Task ReloadCoursesAsync()

    {

        AssignedCourses.Clear();

        SelectedCourse = null;

        TeacherDisplayName = "—";



        if (SelectedLocal is null || SelectedPedagogicalClass is null || SelectedYear is null)

        {

            return;

        }



        try

        {

            var configuration = await _courseConfigurationApiService.GetConfigurationAsync(

                SelectedYear.Id,

                SelectedPedagogicalClass.Id,

                SelectedLocal.Id);



            foreach (var course in configuration.Items.Where(i => i.IsActive).OrderBy(i => i.CourseName))

            {

                AssignedCourses.Add(course);

            }



            SelectedCourse = AssignedCourses.FirstOrDefault();

        }

        catch (Exception ex)

        {

            StatusMessage = ex.Message;

        }

    }



    [RelayCommand(CanExecute = nameof(CanLoadStudents))]

    private Task LoadStudentsAsync() => LoadStudentsCoreAsync();



    private async Task TryAutoLoadStudentsAsync()

    {

        if (!CanLoadStudentsFromLocal())

        {

            GradeEntries.Clear();

            IsGridLoaded = false;

            CurrentEvaluation = null;

            RefreshStatistics();

            NotifyCommands();

            return;

        }



        await LoadStudentsCoreAsync();

    }



    private async Task LoadStudentsCoreAsync()

    {

        if (SelectedYear is null || SelectedPeriod is null || SelectedLocal is null)

        {

            StatusMessage = "Sélectionnez l'année, la période et la salle.";

            return;

        }



        IsBusy = true;

        StatusMessage = null;

        try

        {

            var enrollments = (await _academicApiService.GetEnrollmentsAsync(SelectedLocal.Id, SelectedYear.Id))

                .Where(e => e.IsActive)

                .OrderBy(e => e.StudentName)

                .ToList();



            Dictionary<Guid, GradeEntryDto> entryMap;

            if (CanLoadStudentsWithGrades())

            {

                CurrentEvaluation = await ResolveEvaluationAsync();

                var entries = await _gradeApiService.GetGradeEntriesAsync(CurrentEvaluation.Id);

                entryMap = entries.ToDictionary(e => e.StudentId);

            }

            else

            {

                CurrentEvaluation = null;

                entryMap = [];

            }



            GradeEntries.Clear();

            var row = 1;

            foreach (var enrollment in enrollments)

            {

                if (entryMap.TryGetValue(enrollment.StudentId, out var entry))

                {

                    GradeEntries.Add(CreateEntryItem(

                        row++,

                        entry.StudentId,

                        enrollment.RegistrationNumber,

                        entry.StudentName,

                        entry.Score == 0 && entry.Id == Guid.Empty ? null : entry.Score,

                        entry.Comment));

                }

                else

                {

                    GradeEntries.Add(CreateEntryItem(

                        row++,

                        enrollment.StudentId,

                        enrollment.RegistrationNumber,

                        enrollment.StudentName,

                        null,

                        null));

                }

            }



            IsGridLoaded = true;

            RefreshStatistics();

            StatusMessage = CanLoadStudentsWithGrades()

                ? $"{GradeEntries.Count} élève(s) chargé(s) — barème /{EvaluationMaxScore}."

                : $"{GradeEntries.Count} élève(s) chargé(s) — sélectionnez un cours pour coter.";

        }

        catch (Exception ex)

        {

            StatusMessage = ex.Message;

            IsGridLoaded = false;

        }

        finally

        {

            IsBusy = false;

            NotifyCommands();

        }

    }



    private async Task<EvaluationDto> ResolveEvaluationAsync()

    {

        var evaluations = await _gradeApiService.GetEvaluationsAsync(SelectedLocal!.Id, SelectedPeriod!.Id);

        var date = DateOnly.FromDateTime(EvaluationDate.Date);

        var existing = evaluations.FirstOrDefault(e =>

            e.CourseId == SelectedCourse!.CourseId

            && e.EvaluationTypeId == SelectedEvaluationType!.Id

            && e.EvaluationDate == date

            && string.Equals(e.Title, EvaluationTitle.Trim(), StringComparison.OrdinalIgnoreCase));



        if (existing is not null)

        {

            return existing;

        }



        return await _gradeApiService.CreateEvaluationAsync(new CreateEvaluationRequest(

            SelectedYear!.Id,

            SelectedPeriod.Id,

            SelectedCourse!.CourseId,

            SelectedLocal.Id,

            SelectedEvaluationType!.Id,

            null,

            EvaluationTitle.Trim(),

            EvaluationCoefficient,

            EvaluationMaxScore,

            date));

    }



    private GradeEntryEditItem CreateEntryItem(

        int rowNumber,

        Guid studentId,

        string registrationNumber,

        string studentName,

        decimal? score,

        string? comment) =>

        new(

            rowNumber,

            studentId,

            registrationNumber,

            studentName,

            score,

            comment,

            EvaluationMaxScore,

            RefreshStatistics);



    [RelayCommand(CanExecute = nameof(CanSaveGrades))]

    private async Task SaveGradesAsync() => await PersistGradesAsync(closeAfterSave: false);



    [RelayCommand(CanExecute = nameof(CanSaveGrades))]

    private async Task SaveAndCloseGradesAsync() => await PersistGradesAsync(closeAfterSave: true);



    private async Task PersistGradesAsync(bool closeAfterSave)

    {

        if (CurrentEvaluation is null)

        {

            StatusMessage = "Chargez d'abord les élèves.";

            return;

        }



        var invalid = GradeEntries.FirstOrDefault(e =>

            e.Score is not null

            && (e.Score < 0 || e.Score > EvaluationMaxScore));

        if (invalid is not null)

        {

            StatusMessage = $"Note invalide pour {invalid.StudentName} (0 à {EvaluationMaxScore}).";

            return;

        }



        IsBusy = true;

        try

        {

            await _gradeApiService.SubmitGradesAsync(new SubmitGradesRequest(

                CurrentEvaluation.Id,

                GradeEntries.Select(e => e.ToInput()).ToList()));



            StatusMessage = closeAfterSave ? "Notes enregistrées. Vous pouvez fermer l'écran." : "Notes enregistrées.";

            RefreshStatistics();

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



    [RelayCommand(CanExecute = nameof(CanSaveGrades))]

    private async Task CancelGradesAsync()

    {

        if (CurrentEvaluation is null)

        {

            GradeEntries.Clear();

            IsGridLoaded = false;

            RefreshStatistics();

            StatusMessage = "Saisie annulée.";

            return;

        }



        await LoadStudentsCoreAsync();

        StatusMessage = "Modifications annulées — données rechargées.";

    }



    [RelayCommand(CanExecute = nameof(CanSaveGrades))]

    private void PrintGrid()

    {

        StatusMessage = "Impression de la grille — fonctionnalité à venir.";

    }



    [RelayCommand(CanExecute = nameof(CanSaveGrades))]

    private void ExportExcel()

    {

        if (GradeEntries.Count == 0)

        {

            return;

        }



        var dialog = new SaveFileDialog

        {

            Filter = "CSV Excel (*.csv)|*.csv",

            FileName = $"Cotation_{SummaryCourseName}_{SummaryDate}.csv".Replace(' ', '_')

        };



        if (dialog.ShowDialog() != true)

        {

            return;

        }



        var builder = new StringBuilder();

        builder.AppendLine("N°;Matricule;Nom;Points;Observation");

        foreach (var entry in GradeEntries)

        {

            builder.Append(entry.RowNumber).Append(';')

                .Append(entry.RegistrationNumber).Append(';')

                .Append(entry.StudentName).Append(';')

                .Append(entry.ScoreDisplay).Append(';')

                .Append(entry.Comment?.Replace(';', ',') ?? string.Empty)

                .AppendLine();

        }



        File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);

        StatusMessage = $"Export enregistré : {dialog.FileName}";

    }

}


