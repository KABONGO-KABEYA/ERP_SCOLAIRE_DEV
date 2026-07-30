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

using SchoolManagement.Desktop.UI;



namespace SchoolManagement.Desktop.ViewModels;



public sealed class SavedEvaluationListItem(EvaluationDto evaluation)
{
    public EvaluationDto Evaluation { get; } = evaluation;

    public string DisplayLabel
    {
        get
        {
            var status = Evaluation.IsOpen ? string.Empty : " [Fermée]";
            return $"{Evaluation.CourseName} — {Evaluation.EvaluationTypeName} — {Evaluation.Title} ({Evaluation.EvaluationDate:dd/MM/yyyy}){status}";
        }
    }
}



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

    private readonly IAuthSessionService _authSession;



    public GradesViewModel(
        IGradeApiService gradeApiService,
        ISchoolApiService schoolApiService,
        IAcademicApiService academicApiService,
        ICourseConfigurationApiService courseConfigurationApiService,
        IAuthSessionService authSession)
    {
        _gradeApiService = gradeApiService;
        _schoolApiService = schoolApiService;
        _academicApiService = academicApiService;
        _courseConfigurationApiService = courseConfigurationApiService;
        _authSession = authSession;
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
        _ = InitializeAsync();
    }

    private void OnGlobalAcademicYearChanged()
    {
        SessionYear = AcademicYearRefreshBridge.SelectedYear
            ?? SessionYears.FirstOrDefault(y => y.IsCurrent)
            ?? SessionYears.FirstOrDefault();
        if (IsSessionOpen)
        {
            LeaveCotationSession();
        }

        if (IsTeacherIdentityLocked)
        {
            _ = OpenCotationSessionAsync();
        }
    }



    private bool _isApplyingSavedEvaluation;



    public ObservableCollection<GradeEntryEditItem> GradeEntries { get; } = [];

    public ObservableCollection<EvaluationTypeDto> EvaluationTypes { get; } = [];

    public ObservableCollection<PedagogicalClassDto> PedagogicalClasses { get; } = [];

    public ObservableCollection<ClassLocalDto> ClassLocals { get; } = [];

    public ObservableCollection<CourseConfigurationItemDto> AssignedCourses { get; } = [];

    public ObservableCollection<SavedEvaluationListItem> SavedEvaluations { get; } = [];



    [ObservableProperty] private SchoolLookupsDto? _lookups;

    [ObservableProperty] private AcademicYearDto? _selectedYear;

    [ObservableProperty] private AcademicPeriodLookupDto? _selectedPeriod;

    [ObservableProperty] private PedagogicalClassDto? _selectedPedagogicalClass;

    [ObservableProperty] private ClassLocalDto? _selectedLocal;

    [ObservableProperty] private CourseConfigurationItemDto? _selectedCourse;

    [ObservableProperty] private EvaluationTypeDto? _selectedEvaluationType;

    [ObservableProperty] private SavedEvaluationListItem? _selectedSavedEvaluation;

    [ObservableProperty] private EvaluationDto? _currentEvaluation;

    [ObservableProperty] private DateTime _evaluationDate = DateTime.Today;

    [ObservableProperty] private string _evaluationTitle = "Interrogation n°1";

    [ObservableProperty] private decimal _evaluationCoefficient = 1;

    [ObservableProperty] private int _evaluationMaxScore = 20;

    [ObservableProperty] private string _teacherDisplayName = "—";

    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private bool _isGridLoaded;

    [ObservableProperty] private bool _isParametersExpanded;



    public bool HasSavedEvaluations => SavedEvaluations.Count > 0;



    public bool ShowNoSavedEvaluationsMessage =>

        !IsBusy

        && CanLoadStudentsFromLocal()

        && !HasSavedEvaluations;



    public string ParametersHeaderText => StatTotalStudents > 0

        ? $"Paramètres de l'évaluation ({StatTotalStudents} élève{(StatTotalStudents > 1 ? "s" : "")})"

        : "Paramètres de l'évaluation";



    public string ParametersToggleLabel => IsParametersExpanded ? "Fermer le menu paramètres" : "Ouvrir le menu paramètres";



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
        IsSessionOpen
            ? CotationPeriods.Select(p => new AcademicPeriodLookupDto(
                p.Id,
                p.Name,
                SelectedYear?.Id ?? Guid.Empty,
                p.OrderIndex))
            : Lookups?.AcademicPeriods.Where(p => SelectedYear is null || p.AcademicYearId == SelectedYear.Id) ?? [];



    public string SummaryClassName => SelectedLocal?.FullDisplayName ?? "—";

    public string SummaryLocalName => SelectedLocal?.FullDisplayName ?? "—";

    public string SummaryCourseName => SelectedCourse?.CourseName ?? "—";

    public string SummaryTeacherName => TeacherDisplayName;

    public string SummaryPeriodName => SelectedPeriod?.Name ?? "—";

    public string SummaryWorkTypeName => SelectedEvaluationType?.Name ?? EvaluationTitle;

    public string SummaryDate => EvaluationDate.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);

    public string SummaryMaxScoreLabel => $"{EvaluationMaxScore} points";

    public string SummaryCoefficientLabel => EvaluationCoefficient.ToString("0.##", CultureInfo.CurrentCulture);

    public string SummaryStudentCountLabel => StatTotalStudents.ToString(CultureInfo.CurrentCulture);



    public bool CanEditGrades =>

        IsGridLoaded

        && GradeEntries.Count > 0

        && !IsBusy

        && HasActivePeriod

        && CurrentEvaluation?.IsOpen != false;



    partial void OnSelectedYearChanged(AcademicYearDto? value)

    {
        if (_suppressCascade || IsSessionOpen)
        {
            return;
        }

        // Panneau identification : l'année de session est SessionYear.
        OnPropertyChanged(nameof(FilteredPeriods));
        NotifyCommands();
    }



    partial void OnSelectedPeriodChanged(AcademicPeriodLookupDto? value)
    {
        if (_suppressCascade)
        {
            return;
        }

        if (IsSessionOpen)
        {
            OnCotationPeriodChanged();
            return;
        }

        NotifySummary();
        _ = ReloadSavedEvaluationsAsync();
        _ = TryAutoLoadStudentsAsync();
    }



    partial void OnSelectedPedagogicalClassChanged(PedagogicalClassDto? value)
    {
        if (_suppressCascade || IsSessionOpen)
        {
            return;
        }

        _ = ReloadLocalsAndCoursesAsync();
    }



    partial void OnSelectedLocalChanged(ClassLocalDto? value)
    {
        if (_suppressCascade)
        {
            return;
        }

        if (IsSessionOpen)
        {
            _ = OnCotationClassChangedAsync();
            return;
        }

        _ = HandleSelectedLocalChangedAsync();
    }



    private async Task HandleSelectedLocalChangedAsync()
    {
        await ReloadCoursesAsync();
        NotifySummary();
        await ReloadSavedEvaluationsAsync();
        await TryAutoLoadStudentsAsync();
    }



    partial void OnSelectedCourseChanged(CourseConfigurationItemDto? value)

    {
        if (_suppressCascade)
        {
            return;
        }

        if (IsSessionOpen)
        {
            OnCotationCourseChanged();
            return;
        }

        TeacherDisplayName = value?.TeacherName ?? "—";

        if (value is not null && value.MaxPerPeriod > 0)

        {

            EvaluationMaxScore = value.MaxPerPeriod;

        }



        NotifySummary();

        NotifyCommands();

        if (!_isApplyingSavedEvaluation)
        {
            ClearSavedEvaluationSelection();
            _ = TryAutoLoadStudentsAsync();
        }

    }



    partial void OnSelectedEvaluationTypeChanged(EvaluationTypeDto? value)
    {
        if (_suppressCascade)
        {
            return;
        }

        if (IsSessionOpen)
        {
            OnCotationEvaluationTypeChanged();
            return;
        }

        NotifySummary();
        if (!_isApplyingSavedEvaluation)
        {
            ClearSavedEvaluationSelection();
        }

        _ = TryAutoLoadStudentsAsync();
    }



    partial void OnSelectedSavedEvaluationChanged(SavedEvaluationListItem? value)
    {
        if (_isApplyingSavedEvaluation || value is null)
        {
            return;
        }

        _ = ApplySavedEvaluationAsync(value.Evaluation);
    }



    partial void OnEvaluationDateChanged(DateTime value)
    {
        NotifySummary();
        if (!_isApplyingSavedEvaluation)
        {
            ClearSavedEvaluationSelection();
        }
    }



    partial void OnEvaluationTitleChanged(string value)
    {
        NotifySummary();
        if (!_isApplyingSavedEvaluation)
        {
            ClearSavedEvaluationSelection();
        }
    }



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



    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditGrades));
        NotifyCommands();
    }



    partial void OnIsGridLoadedChanged(bool value)

    {

        OnPropertyChanged(nameof(CanEditGrades));

        NotifyCommands();

    }



    partial void OnCurrentEvaluationChanged(EvaluationDto? value) =>

        OnPropertyChanged(nameof(CanEditGrades));



    private void NotifyCommands()

    {

        LoadStudentsCommand.NotifyCanExecuteChanged();

        SaveGradesCommand.NotifyCanExecuteChanged();

        SaveAndCloseGradesCommand.NotifyCanExecuteChanged();

        CancelGradesCommand.NotifyCanExecuteChanged();

        PrintGridCommand.NotifyCanExecuteChanged();

        ExportExcelCommand.NotifyCanExecuteChanged();

        RefreshSavedEvaluationsCommand.NotifyCanExecuteChanged();

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
        OnPropertyChanged(nameof(SummarySectionName));

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
        NotifyBanner();

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



    private bool CanSaveGrades() =>

        IsGridLoaded

        && GradeEntries.Count > 0

        && !IsBusy

        && CanLoadStudentsWithGrades()

        && HasActivePeriod

        && CurrentEvaluation?.IsOpen != false;



    [RelayCommand]

    private async Task InitializeAsync()

    {

        IsBusy = true;

        try

        {
            var years = await _schoolApiService.GetAcademicYearsAsync();
            SessionYears.Clear();
            foreach (var year in years.OrderByDescending(y => y.Label))
            {
                SessionYears.Add(year);
            }

            SessionYear = AcademicYearRefreshBridge.SelectedYear
                ?? SessionYears.FirstOrDefault(y => y.IsCurrent)
                ?? SessionYears.FirstOrDefault();
            SelectedYear = SessionYear;

            IsSessionOpen = false;
            IsEvaluationManagerOpen = false;
            IsGradeGridOpen = false;
            ApplyConnectedUserIdentity();

            if (IsTeacherIdentityLocked)
            {
                StatusMessage = null;
                await OpenCotationSessionAsync();
            }
            else
            {
                StatusMessage = "Identifiez l'enseignant pour accéder à la cotation.";
            }
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

        if (_isApplyingSavedEvaluation)

        {

            return;

        }



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



    private void ClearSavedEvaluationSelection()

    {

        if (SelectedSavedEvaluation is null)

        {

            return;

        }



        _isApplyingSavedEvaluation = true;

        try

        {

            SelectedSavedEvaluation = null;

        }

        finally

        {

            _isApplyingSavedEvaluation = false;

        }

    }



    private void SelectSavedEvaluation(EvaluationDto? evaluation)

    {

        _isApplyingSavedEvaluation = true;

        try

        {

            SelectedSavedEvaluation = evaluation is null

                ? null

                : SavedEvaluations.FirstOrDefault(item => item.Evaluation.Id == evaluation.Id);

        }

        finally

        {

            _isApplyingSavedEvaluation = false;

        }

    }



    private void NotifySavedEvaluationsState()

    {

        OnPropertyChanged(nameof(HasSavedEvaluations));

        OnPropertyChanged(nameof(ShowNoSavedEvaluationsMessage));

        RefreshSavedEvaluationsCommand.NotifyCanExecuteChanged();

    }



    private bool CanRefreshSavedEvaluations() => CanLoadStudentsFromLocal() && !IsBusy;



    [RelayCommand(CanExecute = nameof(CanRefreshSavedEvaluations))]

    private Task RefreshSavedEvaluationsAsync() => ReloadSavedEvaluationsAsync();



    private async Task ReloadSavedEvaluationsAsync()

    {

        SavedEvaluations.Clear();

        NotifySavedEvaluationsState();



        if (SelectedLocal is null || SelectedPeriod is null)

        {

            return;

        }



        try

        {

            var evaluations = await _gradeApiService.GetEvaluationsAsync(SelectedLocal.Id, SelectedPeriod.Id);

            foreach (var evaluation in evaluations

                         .OrderByDescending(e => e.EvaluationDate)

                         .ThenBy(e => e.CourseName)

                         .ThenBy(e => e.Title))

            {

                SavedEvaluations.Add(new SavedEvaluationListItem(evaluation));

            }



            SelectSavedEvaluation(CurrentEvaluation);

            NotifySavedEvaluationsState();

        }

        catch (Exception ex)

        {

            StatusMessage = ex.Message;

        }

    }



    private async Task ApplySavedEvaluationAsync(EvaluationDto evaluation)

    {

        if (SelectedYear is null || SelectedPeriod is null || SelectedLocal is null)

        {

            StatusMessage = "Sélectionnez l'année, la période et la salle.";

            return;

        }



        _isApplyingSavedEvaluation = true;

        IsBusy = true;

        StatusMessage = null;



        try

        {

            EvaluationTitle = evaluation.Title;

            EvaluationCoefficient = evaluation.Weight;

            EvaluationMaxScore = evaluation.MaxScore;

            EvaluationDate = evaluation.EvaluationDate.ToDateTime(TimeOnly.MinValue);

            SelectedEvaluationType = EvaluationTypes.FirstOrDefault(type => type.Id == evaluation.EvaluationTypeId);



            var course = AssignedCourses.FirstOrDefault(item => item.CourseId == evaluation.CourseId);

            if (course is not null)

            {

                SelectedCourse = course;

            }

            else

            {

                StatusMessage = $"Cours « {evaluation.CourseName} » introuvable dans la configuration actuelle.";

            }



            CurrentEvaluation = evaluation;

            await PopulateGradeEntriesAsync(evaluation);

            IsGridLoaded = true;

            RefreshStatistics();

            OnPropertyChanged(nameof(CanEditGrades));

            NotifyCommands();



            StatusMessage = evaluation.IsOpen

                ? $"{GradeEntries.Count} élève(s) — évaluation « {evaluation.Title} » chargée."

                : $"{GradeEntries.Count} élève(s) — évaluation « {evaluation.Title} » chargée (fermée, lecture seule).";

        }

        catch (Exception ex)

        {

            StatusMessage = ex.Message;

            IsGridLoaded = false;

        }

        finally

        {

            IsBusy = false;

            _isApplyingSavedEvaluation = false;

        }

    }



    private async Task PopulateGradeEntriesAsync(EvaluationDto? evaluation)

    {

        var enrollments = (await _academicApiService.GetEnrollmentsAsync(SelectedLocal!.Id, SelectedYear!.Id))

            .Where(enrollment => enrollment.IsActive)

            .OrderBy(enrollment => enrollment.StudentName)

            .ToList();



        Dictionary<Guid, GradeEntryDto> entryMap = [];

        if (evaluation is not null)

        {

            var entries = await _gradeApiService.GetGradeEntriesAsync(evaluation.Id);

            entryMap = entries.ToDictionary(entry => entry.StudentId);

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

            EvaluationDto? evaluation = null;



            if (CanLoadStudentsWithGrades())

            {

                evaluation = await ResolveEvaluationAsync();

                CurrentEvaluation = evaluation;

            }

            else

            {

                CurrentEvaluation = null;

            }



            await PopulateGradeEntriesAsync(evaluation);



            IsGridLoaded = true;

            RefreshStatistics();

            await ReloadSavedEvaluationsAsync();



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

            && e.EvaluationTypeId == SelectedEvaluationType!.Id);



        if (existing is not null)

        {

            EvaluationTitle = existing.Title;
            EvaluationCoefficient = existing.Weight;
            EvaluationMaxScore = existing.MaxScore;
            EvaluationDate = existing.EvaluationDate.ToDateTime(TimeOnly.MinValue);
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

            if (!CanLoadStudentsWithGrades())

            {

                StatusMessage = "Sélectionnez le cours et le type d'évaluation avant d'enregistrer.";

                return;

            }



            try

            {

                CurrentEvaluation = await ResolveEvaluationAsync();

            }

            catch (Exception ex)

            {

                StatusMessage = ex.Message;

                return;

            }

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

            await ReloadSavedEvaluationsAsync();

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


