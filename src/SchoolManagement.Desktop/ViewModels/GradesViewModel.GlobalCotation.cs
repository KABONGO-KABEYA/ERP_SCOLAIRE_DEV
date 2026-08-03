using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Grades.DTOs;

namespace SchoolManagement.Desktop.ViewModels;

public partial class GradesViewModel
{
    [ObservableProperty] private bool _isGlobalCotationOpen;
    [ObservableProperty] private string? _globalClassName;
    [ObservableProperty] private string? _globalPeriodLabel;
    [ObservableProperty] private string? _globalStatusMessage;
    [ObservableProperty] private DateTime _globalEvaluationDate = DateTime.Today;
    [ObservableProperty] private string _globalEvaluationTitle = string.Empty;
    [ObservableProperty] private EvaluationTypeDto? _globalEvaluationType;
    [ObservableProperty] private bool _isGlobalBusy;
    [ObservableProperty] private int _globalCotationTabIndex;
    [ObservableProperty] private bool _isGlobalSessionEditable = true;
    [ObservableProperty] private GlobalCotationSessionOption? _selectedGlobalSession;
    [ObservableProperty] private bool _suppressGlobalSessionLoad;

    private Guid _globalClassRoomId;
    private Guid _globalPeriodId;
    private Guid _globalYearId;
    private Guid _globalTeacherId;

    public ObservableCollection<EvaluationTypeDto> GlobalEvaluationTypes { get; } = [];
    public ObservableCollection<GlobalCotationCourseColumn> GlobalCourseColumns { get; } = [];
    public ObservableCollection<GlobalCotationStudentRow> GlobalRows { get; } = [];
    public ObservableCollection<GlobalCotationSessionOption> GlobalSessionOptions { get; } = [];

    public bool ShowGlobalCotation => IsSessionOpen && IsGlobalCotationOpen;
    public bool ShowGlobalSaisieTab => ShowGlobalCotation && GlobalCotationTabIndex == 0;
    public bool ShowGlobalVueTab => ShowGlobalCotation && GlobalCotationTabIndex == 1;
    public bool CanSaveGlobalCotation => IsGlobalSessionEditable && !IsGlobalBusy;

    partial void OnIsGlobalCotationOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowGlobalCotation));
        OnPropertyChanged(nameof(ShowGlobalSaisieTab));
        OnPropertyChanged(nameof(ShowGlobalVueTab));
        OnPropertyChanged(nameof(ShowAssignmentsHome));
        OnPropertyChanged(nameof(ShowEvaluationManager));
        OnPropertyChanged(nameof(ShowCotationWorkspace));
        NotifyPedagogicalSheetUi();
        if (value)
        {
            GlobalCotationTabIndex = 0;
        }
    }

    partial void OnGlobalCotationTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowGlobalSaisieTab));
        OnPropertyChanged(nameof(ShowGlobalVueTab));
        NotifyPedagogicalSheetUi();
        if (value == 1 && IsGlobalCotationOpen)
        {
            _ = EnsurePedagogicalSheetLoadedAsync(force: true);
        }
    }

    [RelayCommand]
    private void SelectGlobalCotationTab(object? tabIndex)
    {
        if (tabIndex is int i)
        {
            GlobalCotationTabIndex = i;
            return;
        }

        if (tabIndex is string s && int.TryParse(s, out var parsed))
        {
            GlobalCotationTabIndex = parsed;
        }
    }

    [RelayCommand]
    private async Task OpenGlobalCotationAsync(CotationClassGroup? group)
    {
        if (group is null || _session is null || SelectedYear is null)
        {
            return;
        }

        IsGlobalBusy = true;
        GlobalStatusMessage = null;
        try
        {
            var grid = await _gradeApiService.GetGlobalCotationGridAsync(
                SelectedYear.Id,
                group.ClassRoomId,
                _session.TeacherId);

            _globalClassRoomId = grid.ClassRoomId;
            _globalPeriodId = grid.AcademicPeriodId;
            _globalYearId = grid.AcademicYearId;
            _globalTeacherId = _session.TeacherId;
            GlobalClassName = grid.ClassDisplayName;
            GlobalPeriodLabel = $"{grid.PeriodName} ({grid.PeriodKindLabel})";
            GlobalEvaluationDate = DateTime.Today;
            if (grid.PeriodStart is DateOnly start && grid.PeriodEnd is DateOnly end)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                if (today < start) GlobalEvaluationDate = start.ToDateTime(TimeOnly.MinValue);
                else if (today > end) GlobalEvaluationDate = end.ToDateTime(TimeOnly.MinValue);
            }

            GlobalEvaluationTypes.Clear();
            foreach (var t in grid.EvaluationTypes)
            {
                GlobalEvaluationTypes.Add(t);
            }

            GlobalEvaluationType = GlobalEvaluationTypes.FirstOrDefault();
            GlobalEvaluationTitle = GlobalEvaluationType?.Name ?? string.Empty;

            GlobalCourseColumns.Clear();
            foreach (var c in grid.Courses)
            {
                GlobalCourseColumns.Add(new GlobalCotationCourseColumn(c));
            }

            GlobalRows.Clear();
            foreach (var s in grid.Students)
            {
                var row = new GlobalCotationStudentRow(s);
                foreach (var col in GlobalCourseColumns)
                {
                    row.Cells.Add(new GlobalCotationCell(col));
                }

                GlobalRows.Add(row);
            }

            IsEvaluationManagerOpen = false;
            IsGradeGridOpen = false;
            IsGlobalCotationOpen = true;
            GlobalCotationTabIndex = 0;
            IsParametersExpanded = false;
            IsGlobalSessionEditable = true;
            NotifyBanner();
            NotifyCommands();
            GlobalColumnsChanged?.Invoke();
            await ReloadGlobalSessionOptionsAsync(selectNew: true);
            // Précharge la feuille pédagogique pour l'onglet Vue globale.
            _ = EnsurePedagogicalSheetLoadedAsync(force: true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsGlobalBusy = false;
            NotifyCommands();
        }
    }

    /// <summary>Signal pour reconstruire les colonnes dynamiques de la grille WPF.</summary>
    public event Action? GlobalColumnsChanged;

    [RelayCommand]
    private async Task CloseGlobalCotationAsync()
    {
        IsGlobalCotationOpen = false;
        GlobalRows.Clear();
        GlobalCourseColumns.Clear();
        GlobalSessionOptions.Clear();
        SelectedGlobalSession = null;
        GlobalStatusMessage = null;
        IsGlobalSessionEditable = true;
        await RefreshAssignmentProgressAsync();
        NotifyBanner();
        NotifyCommands();
    }

    [RelayCommand]
    private void CancelGlobalCotation()
    {
        if (SelectedGlobalSession is { IsNew: false })
        {
            _ = LoadSelectedGlobalSessionAsync();
            GlobalStatusMessage = "Modifications annulées — données rechargées.";
            return;
        }

        ClearGlobalGridScores();
        GlobalStatusMessage = "Saisie annulée.";
    }

    [RelayCommand]
    private void StartNewGlobalSession()
    {
        var neu = GlobalSessionOptions.FirstOrDefault(o => o.IsNew);
        if (neu is null)
        {
            neu = GlobalCotationSessionOption.CreateNew();
            GlobalSessionOptions.Insert(0, neu);
        }

        SelectedGlobalSession = neu;
    }

    partial void OnIsGlobalBusyChanged(bool value) => OnPropertyChanged(nameof(CanSaveGlobalCotation));

    partial void OnIsGlobalSessionEditableChanged(bool value) => OnPropertyChanged(nameof(CanSaveGlobalCotation));

    partial void OnSelectedGlobalSessionChanged(GlobalCotationSessionOption? value)
    {
        if (_suppressGlobalSessionLoad || value is null)
        {
            return;
        }

        _ = ApplyGlobalSessionSelectionAsync(value);
    }

    private async Task ReloadGlobalSessionOptionsAsync(bool selectNew)
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            var sessions = await _gradeApiService.GetGlobalCotationSessionsAsync(
                _globalYearId,
                _globalClassRoomId,
                _globalPeriodId,
                _globalTeacherId);

            _suppressGlobalSessionLoad = true;
            GlobalSessionOptions.Clear();
            GlobalSessionOptions.Add(GlobalCotationSessionOption.CreateNew());
            foreach (var s in sessions)
            {
                GlobalSessionOptions.Add(new GlobalCotationSessionOption(s));
            }

            if (selectNew)
            {
                SelectedGlobalSession = GlobalSessionOptions.FirstOrDefault();
            }
            else if (SelectedGlobalSession is { IsNew: false, Session: not null } current)
            {
                SelectedGlobalSession = GlobalSessionOptions.FirstOrDefault(o =>
                    o.Session is not null
                    && o.Session.EvaluationTypeId == current.Session.EvaluationTypeId
                    && string.Equals(o.Session.Title, current.Session.Title, StringComparison.OrdinalIgnoreCase))
                    ?? GlobalSessionOptions.FirstOrDefault();
            }
            else
            {
                SelectedGlobalSession = GlobalSessionOptions.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            GlobalStatusMessage = ex.Message;
        }
        finally
        {
            _suppressGlobalSessionLoad = false;
        }
    }

    private async Task ApplyGlobalSessionSelectionAsync(GlobalCotationSessionOption option)
    {
        if (option.IsNew)
        {
            ClearGlobalGridScores();
            IsGlobalSessionEditable = true;
            GlobalEvaluationType = GlobalEvaluationTypes.FirstOrDefault();
            GlobalEvaluationTitle = GlobalEvaluationType?.Name ?? string.Empty;
            GlobalEvaluationDate = DateTime.Today;
            GlobalStatusMessage = "Nouvelle saisie.";
            return;
        }

        await LoadSelectedGlobalSessionAsync();
    }

    private async Task LoadSelectedGlobalSessionAsync()
    {
        if (SelectedGlobalSession?.Session is null || _session is null)
        {
            return;
        }

        IsGlobalBusy = true;
        GlobalStatusMessage = null;
        try
        {
            var session = SelectedGlobalSession.Session;
            var detail = await _gradeApiService.LoadGlobalCotationSessionAsync(
                _globalYearId,
                _globalClassRoomId,
                _globalPeriodId,
                _globalTeacherId,
                session.EvaluationTypeId,
                session.Title);

            GlobalEvaluationType = GlobalEvaluationTypes.FirstOrDefault(t => t.Id == detail.EvaluationTypeId)
                ?? GlobalEvaluationTypes.FirstOrDefault();
            GlobalEvaluationTitle = detail.Title;
            GlobalEvaluationDate = detail.EvaluationDate.ToDateTime(TimeOnly.MinValue);
            IsGlobalSessionEditable = detail.CanEdit;

            ClearGlobalGridScores();

            var byCourse = detail.Courses.ToDictionary(c => c.CourseId);
            foreach (var col in GlobalCourseColumns)
            {
                if (!byCourse.TryGetValue(col.CourseId, out var courseLoad))
                {
                    continue;
                }

                col.MaxScore = courseLoad.MaxScore > 0 ? courseLoad.MaxScore : col.MaxScore;
                var gradesByStudent = courseLoad.Grades.ToDictionary(g => g.StudentId);
                foreach (var row in GlobalRows)
                {
                    var cell = row.Cells.First(c => c.Column.CourseId == col.CourseId);
                    if (!gradesByStudent.TryGetValue(row.StudentId, out var grade))
                    {
                        continue;
                    }

                    cell.ScoreText = grade.IsAbsent
                        ? "ABS"
                        : grade.Score.ToString("0.##", CultureInfo.CurrentCulture);
                }
            }

            GlobalStatusMessage = detail.CanEdit
                ? $"Évaluation chargée : {detail.Title} — modification possible."
                : detail.ReadOnlyReason ?? "Consultation seule.";
        }
        catch (Exception ex)
        {
            GlobalStatusMessage = ex.Message;
        }
        finally
        {
            IsGlobalBusy = false;
        }
    }

    private void ClearGlobalGridScores()
    {
        foreach (var row in GlobalRows)
        {
            foreach (var cell in row.Cells)
            {
                cell.ScoreText = string.Empty;
            }
        }
    }

    partial void OnGlobalEvaluationTypeChanged(EvaluationTypeDto? value)
    {
        if (value is null || SelectedGlobalSession is { IsNew: false })
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(GlobalEvaluationTitle)
            || GlobalEvaluationTypes.Any(t => t.Name == GlobalEvaluationTitle))
        {
            GlobalEvaluationTitle = value.Name;
        }
    }

    [RelayCommand]
    private async Task SaveGlobalCotationAsync() => await PersistGlobalCotationAsync(closeAfterSave: false);

    [RelayCommand]
    private async Task SaveAndCloseGlobalCotationAsync() => await PersistGlobalCotationAsync(closeAfterSave: true);

    private async Task PersistGlobalCotationAsync(bool closeAfterSave)
    {
        if (!IsGlobalSessionEditable)
        {
            GlobalStatusMessage = "Cette évaluation est fermée : modification impossible.";
            return;
        }

        if (GlobalEvaluationType is null)
        {
            GlobalStatusMessage = "Sélectionnez un type d'évaluation.";
            return;
        }

        if (string.IsNullOrWhiteSpace(GlobalEvaluationTitle))
        {
            GlobalStatusMessage = "Saisissez un libellé d'évaluation.";
            return;
        }

        foreach (var col in GlobalCourseColumns)
        {
            if (col.MaxScore <= 0)
            {
                GlobalStatusMessage = $"Maximum invalide pour « {col.CourseName} ».";
                return;
            }
        }

        foreach (var row in GlobalRows)
        {
            foreach (var cell in row.Cells)
            {
                if (!cell.TryParseScore(out _, out var error) && !string.IsNullOrWhiteSpace(cell.ScoreText))
                {
                    GlobalStatusMessage = $"{row.StudentName} — {cell.Column.CourseName} : {error}";
                    return;
                }
            }
        }

        var courseBlocks = new List<GlobalCotationCourseSaveDto>();
        foreach (var col in GlobalCourseColumns)
        {
            var grades = new List<GradeEntryInput>();
            foreach (var row in GlobalRows)
            {
                var cell = row.Cells.First(c => c.Column.CourseId == col.CourseId);
                if (string.IsNullOrWhiteSpace(cell.ScoreText))
                {
                    continue;
                }

                if (cell.IsAbsentEntry)
                {
                    grades.Add(new GradeEntryInput(row.StudentId, 0, true, "ABS"));
                    continue;
                }

                if (!cell.TryParseScore(out var score, out var error))
                {
                    GlobalStatusMessage = $"{row.StudentName} — {col.CourseName} : {error}";
                    return;
                }

                grades.Add(new GradeEntryInput(row.StudentId, score, false, null));
            }

            if (grades.Count > 0)
            {
                courseBlocks.Add(new GlobalCotationCourseSaveDto(col.CourseId, col.MaxScore, grades));
            }
        }

        if (courseBlocks.Count == 0)
        {
            GlobalStatusMessage = "Saisissez au moins une note avant d'enregistrer.";
            return;
        }

        IsGlobalBusy = true;
        GlobalStatusMessage = null;
        try
        {
            var result = await _gradeApiService.SaveGlobalCotationAsync(new SaveGlobalCotationRequest(
                _globalYearId,
                _globalPeriodId,
                _globalClassRoomId,
                GlobalEvaluationType.Id,
                GlobalEvaluationTitle.Trim(),
                DateOnly.FromDateTime(GlobalEvaluationDate.Date),
                courseBlocks));

            var wasUpdate = SelectedGlobalSession is { IsNew: false } || result.EvaluationsCreated == 0;
            GlobalStatusMessage = wasUpdate
                ? $"Mis à jour : {result.GradesSaved} note(s) — {result.EvaluationsCreated} nouvelle(s) évaluation(s)."
                : $"Enregistré : {result.EvaluationsCreated} évaluation(s), {result.GradesSaved} note(s).";

            await RefreshAssignmentProgressAsync();
            await EnsurePedagogicalSheetLoadedAsync(force: true);
            await ReloadGlobalSessionOptionsAsync(selectNew: false);

            // Resélectionner la vague enregistrée
            _suppressGlobalSessionLoad = true;
            SelectedGlobalSession = GlobalSessionOptions.FirstOrDefault(o =>
                o.Session is not null
                && o.Session.EvaluationTypeId == GlobalEvaluationType.Id
                && string.Equals(o.Session.Title, GlobalEvaluationTitle.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? SelectedGlobalSession;
            _suppressGlobalSessionLoad = false;

            if (closeAfterSave)
            {
                await CloseGlobalCotationAsync();
            }
        }
        catch (Exception ex)
        {
            GlobalStatusMessage = ex.Message;
        }
        finally
        {
            IsGlobalBusy = false;
        }
    }
}

public partial class GlobalCotationCourseColumn : ObservableObject
{
    public GlobalCotationCourseColumn(GlobalCotationCourseColumnDto dto)
    {
        CourseId = dto.CourseId;
        AssignmentId = dto.AssignmentId;
        CourseName = dto.CourseName;
        _maxScore = dto.MaxScore > 0 ? dto.MaxScore : 20;
    }

    public Guid CourseId { get; }
    public Guid AssignmentId { get; }
    public string CourseName { get; }

    [ObservableProperty] private int _maxScore;

    public string MaxScoreLabel => $"/{MaxScore}";

    partial void OnMaxScoreChanged(int value)
    {
        OnPropertyChanged(nameof(MaxScoreLabel));
        MaxScoreChanged?.Invoke(this);
    }

    public event Action<GlobalCotationCourseColumn>? MaxScoreChanged;
}

public partial class GlobalCotationStudentRow : ObservableObject
{
    public GlobalCotationStudentRow(GlobalCotationStudentRowDto dto)
    {
        RowNumber = dto.RowNumber;
        StudentId = dto.StudentId;
        RegistrationNumber = dto.RegistrationNumber;
        StudentName = dto.StudentName;
    }

    public int RowNumber { get; }
    public Guid StudentId { get; }
    public string RegistrationNumber { get; }
    public string StudentName { get; }
    public ObservableCollection<GlobalCotationCell> Cells { get; } = [];
}

public partial class GlobalCotationCell : ObservableObject
{
    public GlobalCotationCell(GlobalCotationCourseColumn column)
    {
        Column = column;
        column.MaxScoreChanged += _ => Validate();
    }

    public GlobalCotationCourseColumn Column { get; }

    [ObservableProperty] private string _scoreText = string.Empty;
    [ObservableProperty] private bool _isInvalid;
    [ObservableProperty] private bool _isValid;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string? _validationMessage;

    public bool IsAbsentEntry =>
        string.Equals(ScoreText?.Trim(), "ABS", StringComparison.OrdinalIgnoreCase);

    partial void OnScoreTextChanged(string value) => Validate();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ScoreText))
        {
            IsEmpty = true;
            IsInvalid = false;
            IsValid = false;
            ValidationMessage = null;
            return;
        }

        IsEmpty = false;
        if (IsAbsentEntry)
        {
            IsInvalid = false;
            IsValid = true;
            ValidationMessage = null;
            return;
        }

        if (!TryParseScore(out _, out var error))
        {
            IsInvalid = true;
            IsValid = false;
            ValidationMessage = error;
        }
        else
        {
            IsInvalid = false;
            IsValid = true;
            ValidationMessage = null;
        }
    }

    public bool TryParseScore(out decimal score, out string? error)
    {
        score = 0;
        error = null;
        if (string.IsNullOrWhiteSpace(ScoreText) || IsAbsentEntry)
        {
            return true;
        }

        if (!decimal.TryParse(
                ScoreText.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out score)
            && !decimal.TryParse(ScoreText, NumberStyles.Number, CultureInfo.CurrentCulture, out score))
        {
            error = "Note invalide.";
            return false;
        }

        if (score < 0)
        {
            error = "Note négative.";
            return false;
        }

        if (score > Column.MaxScore)
        {
            error = $"Maximum /{Column.MaxScore} dépassé.";
            return false;
        }

        return true;
    }
}

public sealed class GlobalCotationSessionOption
{
    private GlobalCotationSessionOption(bool isNew, GlobalCotationSessionSummaryDto? session)
    {
        IsNew = isNew;
        Session = session;
        DisplayLabel = isNew
            ? "— Nouvelle saisie —"
            : session?.DisplayLabel ?? "—";
    }

    public static GlobalCotationSessionOption CreateNew() => new(true, null);

    public GlobalCotationSessionOption(GlobalCotationSessionSummaryDto session)
        : this(false, session)
    {
    }

    public bool IsNew { get; }
    public GlobalCotationSessionSummaryDto? Session { get; }
    public string DisplayLabel { get; }
}

/// <summary>Converters utilisés par la grille globale (couleur de cellule).</summary>
public sealed class GlobalCellBackgroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var isInvalid = values.Length > 0 && values[0] is true;
        var isValid = values.Length > 1 && values[1] is true;
        if (isInvalid) return new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2));
        if (isValid) return new SolidColorBrush(Color.FromRgb(0xEC, 0xFD, 0xF5));
        return Brushes.White;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
