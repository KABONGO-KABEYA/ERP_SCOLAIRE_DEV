using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.CourseConfiguration.DTOs;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class GradesViewModel
{
    private CotationSessionDto? _session;
    private bool _suppressCascade;
    private CotationPeriodDto? _activeCotationPeriod;
    private Guid _managedCourseId;
    private Guid? _managedAssignmentId;
    private string? _managedCourseName;
    private readonly List<EvaluationTypeDto> _sessionEvaluationTypes = [];

    public ObservableCollection<AcademicYearDto> SessionYears { get; } = [];
    public ObservableCollection<CotationClassDto> CotationClasses { get; } = [];
    public ObservableCollection<CotationPeriodDto> CotationPeriods { get; } = [];
    public ObservableCollection<CotationAssignmentCard> AssignmentCards { get; } = [];
    public ObservableCollection<CotationClassGroup> AssignmentClassGroups { get; } = [];

    [ObservableProperty] private bool _isSessionOpen;
    [ObservableProperty] private bool _isEvaluationManagerOpen;
    [ObservableProperty] private bool _isGradeGridOpen;
    [ObservableProperty] private AcademicYearDto? _sessionYear;
    [ObservableProperty] private string _teacherEmployeeNumber = string.Empty;
    [ObservableProperty] private string _teacherPassword = string.Empty;
    [ObservableProperty] private string? _sessionError;
    [ObservableProperty] private string _sessionAccessLabel = string.Empty;
    [ObservableProperty] private string _summarySectionName = "—";
    [ObservableProperty] private string _connectedUserLabel = string.Empty;
    [ObservableProperty] private bool _isTeacherIdentityLocked;
    [ObservableProperty] private bool _requiresTeacherPassword;
    [ObservableProperty] private bool _hasActivePeriod;
    [ObservableProperty] private bool _periodSelectionLocked;
    [ObservableProperty] private string _activePeriodBanner = "Aucune période ouverte";
    [ObservableProperty] private string _activePeriodDetails = string.Empty;
    [ObservableProperty] private string? _noOpenPeriodMessage;
    [ObservableProperty] private string _bannerPeriodKind = "—";
    [ObservableProperty] private bool _isCreateEvalDialogOpen;
    [ObservableProperty] private bool _isEditEvalDialogOpen;
    [ObservableProperty] private string? _evalDialogError;
    [ObservableProperty] private DateTime _newEvalDate = DateTime.Today;
    [ObservableProperty] private string _newEvalTitle = string.Empty;
    [ObservableProperty] private EvaluationTypeDto? _newEvalType;
    [ObservableProperty] private int _newEvalMaxScore = 20;
    [ObservableProperty] private CotationEvaluationListItem? _editingEvaluation;
    [ObservableProperty] private bool _isManagedEvaluationsLoading;
    [ObservableProperty] private string? _managedEvaluationsError;
    [ObservableProperty] private int _evaluationManagerTabIndex;
    [ObservableProperty] private bool _isCreateEvalWizardOpen;
    [ObservableProperty] private int _createEvalWizardStep = 1;
    private bool _openGradesAfterCreate;

    public ObservableCollection<CotationEvaluationListItem> ManagedEvaluations { get; } = [];

    public bool ShowIdentificationPanel => !IsSessionOpen;
    public bool ShowAssignmentsHome => IsSessionOpen && !IsEvaluationManagerOpen && !IsGradeGridOpen && !IsGlobalCotationOpen;
    public bool ShowEvaluationManager => IsSessionOpen && IsEvaluationManagerOpen && !IsGradeGridOpen && !IsGlobalCotationOpen;
    public bool ShowCotationWorkspace => IsSessionOpen && IsGradeGridOpen && !IsGlobalCotationOpen;
    public bool CanEnterGrades => HasActivePeriod;
    public bool ShowSwitchTeacherButton => IsSessionOpen && !IsTeacherIdentityLocked;
    public bool CanManageEvaluations => HasActivePeriod;

    partial void OnHasActivePeriodChanged(bool value)
    {
        OnPropertyChanged(nameof(CanManageEvaluations));
        OnPropertyChanged(nameof(CanEnterGrades));
        BeginCreateEvaluationCommand.NotifyCanExecuteChanged();
        BeginCreateEvalWizardCommand.NotifyCanExecuteChanged();
    }

    public bool HasManagedEvaluations => ManagedEvaluations.Count > 0;
    public bool ShowManagedEvaluationsLoading =>
        IsEvaluationManagerOpen
        && IsManagedEvaluationsLoading
        && EvaluationManagerTabIndex == 0;
    public bool ShowManagedEvaluationsEmpty =>
        IsEvaluationManagerOpen
        && !IsManagedEvaluationsLoading
        && string.IsNullOrWhiteSpace(ManagedEvaluationsError)
        && !HasManagedEvaluations
        && EvaluationManagerTabIndex == 0;
    public bool ShowManagedEvaluationsError =>
        IsEvaluationManagerOpen
        && !IsManagedEvaluationsLoading
        && !string.IsNullOrWhiteSpace(ManagedEvaluationsError)
        && EvaluationManagerTabIndex == 0;
    public bool ShowManagedEvaluationsGrid =>
        IsEvaluationManagerOpen
        && !IsManagedEvaluationsLoading
        && string.IsNullOrWhiteSpace(ManagedEvaluationsError)
        && HasManagedEvaluations
        && EvaluationManagerTabIndex == 0;
    public bool ShowManagerTabPlaceholder =>
        IsEvaluationManagerOpen
        && EvaluationManagerTabIndex is 2 or 3;

    public bool ShowManagerTabEvaluations => EvaluationManagerTabIndex == 0;
    public bool IsManagerTabNotes => EvaluationManagerTabIndex == 1;
    public bool IsManagerTabStats => EvaluationManagerTabIndex == 2;
    public bool IsManagerTabHistory => EvaluationManagerTabIndex == 3;

    public int ManagerKpiEvaluations => ManagedEvaluations.Count;
    public int ManagerKpiStudents => ManagerStudentCount;
    public int ManagerKpiGradesEntered => ManagedEvaluations.Sum(e => e.GradedCount);
    public string ManagerKpiAverage => "—";

    public string ManagerStudentBadge =>
        ManagerStudentCount <= 1
            ? $"{ManagerStudentCount} élève"
            : $"{ManagerStudentCount} élèves";

    public string BannerPeriodRange
    {
        get
        {
            if (_activeCotationPeriod?.StartDate is DateOnly start
                && _activeCotationPeriod.EndDate is DateOnly end)
            {
                return $"{start:dd/MM/yyyy} → {end:dd/MM/yyyy}";
            }

            return string.IsNullOrWhiteSpace(ActivePeriodDetails) ? "—" : ActivePeriodDetails;
        }
    }

    public string CreateEvalWizardStepLabel => CreateEvalWizardStep switch
    {
        1 => "Étape 1 / 3 — Type d'évaluation",
        2 => "Étape 2 / 3 — Paramètres",
        _ => "Étape 3 / 3 — Ouverture de la saisie"
    };

    public bool ShowCreateEvalWizardStep1 => IsCreateEvalWizardOpen && CreateEvalWizardStep == 1;
    public bool ShowCreateEvalWizardStep2 => IsCreateEvalWizardOpen && CreateEvalWizardStep == 2;
    public bool ShowCreateEvalWizardStep3 => IsCreateEvalWizardOpen && CreateEvalWizardStep == 3;

    public int ManagerStudentCount => AssignmentCards
        .FirstOrDefault(a => a.ClassRoomId == SelectedLocal?.Id
                             && (a.CourseId == SelectedCourse?.CourseId || a.CourseId == _managedCourseId))
        ?.StudentCount
        ?? StatTotalStudents;

    public string BannerTeacher => TeacherDisplayName;
    public string BannerClass => SelectedLocal?.FullDisplayName ?? "—";
    public string BannerSection => SummarySectionName;
    public string BannerCourse => SelectedCourse?.CourseName ?? "—";
    public string BannerPeriod => SelectedPeriod?.Name ?? "—";
    public string BannerEvaluation => SelectedEvaluationType?.Name ?? EvaluationTitle;
    public string BannerStudents => StatTotalStudents.ToString();
    public string BannerGraded => StatGraded.ToString();
    public string BannerRemaining => StatNotGraded.ToString();
    public string BannerAverage => StatAverage?.ToString("0.00") ?? "—";

    partial void OnIsSessionOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowIdentificationPanel));
        OnPropertyChanged(nameof(ShowAssignmentsHome));
        OnPropertyChanged(nameof(ShowEvaluationManager));
        OnPropertyChanged(nameof(ShowCotationWorkspace));
        OnPropertyChanged(nameof(ShowGlobalCotation));
        OnPropertyChanged(nameof(ShowSwitchTeacherButton));
    }

    partial void OnIsEvaluationManagerOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAssignmentsHome));
        OnPropertyChanged(nameof(ShowEvaluationManager));
        OnPropertyChanged(nameof(ShowCotationWorkspace));
        OnPropertyChanged(nameof(ShowGlobalCotation));
        OnPropertyChanged(nameof(CanManageEvaluations));
        if (value)
        {
            EvaluationManagerTabIndex = 0;
        }
        else
        {
            IsCreateEvalWizardOpen = false;
            CreateEvalWizardStep = 1;
        }

        NotifyManagedEvaluationsState();
        BeginCreateEvaluationCommand.NotifyCanExecuteChanged();
        BeginCreateEvalWizardCommand.NotifyCanExecuteChanged();
    }

    partial void OnEvaluationManagerTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowManagerTabEvaluations));
        OnPropertyChanged(nameof(IsManagerTabNotes));
        OnPropertyChanged(nameof(IsManagerTabStats));
        OnPropertyChanged(nameof(IsManagerTabHistory));
        OnPropertyChanged(nameof(ShowManagerTabPlaceholder));
        NotifyManagedEvaluationsState();
        NotifyCourseNotesUi();
        if (value == 1 && IsEvaluationManagerOpen)
        {
            _ = ReloadCourseNotesGridAsync();
        }
    }

    partial void OnIsCreateEvalWizardOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCreateEvalWizardStep1));
        OnPropertyChanged(nameof(ShowCreateEvalWizardStep2));
        OnPropertyChanged(nameof(ShowCreateEvalWizardStep3));
    }

    partial void OnCreateEvalWizardStepChanged(int value)
    {
        OnPropertyChanged(nameof(CreateEvalWizardStepLabel));
        OnPropertyChanged(nameof(ShowCreateEvalWizardStep1));
        OnPropertyChanged(nameof(ShowCreateEvalWizardStep2));
        OnPropertyChanged(nameof(ShowCreateEvalWizardStep3));
    }

    partial void OnIsGradeGridOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAssignmentsHome));
        OnPropertyChanged(nameof(ShowEvaluationManager));
        OnPropertyChanged(nameof(ShowCotationWorkspace));
        OnPropertyChanged(nameof(ShowGlobalCotation));
    }

    private void ApplyConnectedUserIdentity()
    {
        var user = _authSession.CurrentUser;
        if (user is null)
        {
            ConnectedUserLabel = string.Empty;
            IsTeacherIdentityLocked = false;
            RequiresTeacherPassword = true;
            return;
        }

        ConnectedUserLabel = $"Connecté : {user.FullName} ({user.UserName})";
        var isElevated = _authSession.IsAdministrator
            || user.Roles.Any(r =>
                r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
                || r.Equals("DIRECTION", StringComparison.OrdinalIgnoreCase)
                || r.Equals("PROMOTEUR", StringComparison.OrdinalIgnoreCase)
                || r.Equals("PREFET", StringComparison.OrdinalIgnoreCase)
                || r.Equals("PREFET_ETUDES", StringComparison.OrdinalIgnoreCase));

        IsTeacherIdentityLocked = !isElevated && user.TeacherId.HasValue;
        RequiresTeacherPassword = !IsTeacherIdentityLocked;

        if (string.IsNullOrWhiteSpace(TeacherEmployeeNumber))
        {
            TeacherEmployeeNumber = user.UserName;
        }
    }

    private Guid? ResolveSessionYearId() =>
        AcademicYearRefreshBridge.SelectedYearId
        ?? SessionYear?.Id
        ?? SessionYears.FirstOrDefault(y => y.IsCurrent)?.Id
        ?? SessionYears.FirstOrDefault()?.Id;

    [RelayCommand]
    private async Task OpenCotationSessionAsync()
    {
        SessionError = null;
        var yearId = ResolveSessionYearId();
        if (yearId is null)
        {
            SessionError = "Aucune année scolaire active.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TeacherEmployeeNumber))
        {
            SessionError = "Saisissez le matricule / identifiant enseignant.";
            return;
        }

        IsBusy = true;
        try
        {
            var session = await _gradeApiService.OpenCotationSessionAsync(
                new OpenCotationSessionRequest(
                    yearId.Value,
                    TeacherEmployeeNumber.Trim(),
                    string.IsNullOrWhiteSpace(TeacherPassword) ? null : TeacherPassword));

            await ApplySessionAsync(session);
        }
        catch (Exception ex)
        {
            SessionError = ex.Message;
            IsSessionOpen = false;
            IsEvaluationManagerOpen = false;
            IsGradeGridOpen = false;
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    [RelayCommand]
    private void LeaveCotationSession()
    {
        _session = null;
        _activeCotationPeriod = null;
        IsSessionOpen = false;
        IsEvaluationManagerOpen = false;
        IsGradeGridOpen = false;
        IsGlobalCotationOpen = false;
        TeacherPassword = string.Empty;
        SessionError = null;
        SessionAccessLabel = string.Empty;
        TeacherDisplayName = "—";
        SummarySectionName = "—";
        BannerPeriodKind = "—";
        ClearWorkspaceSelections();
        CotationClasses.Clear();
        CotationPeriods.Clear();
        _sessionEvaluationTypes.Clear();
        AssignmentCards.Clear();
        AssignmentClassGroups.Clear();
        ManagedEvaluations.Clear();
        ClassLocals.Clear();
        AssignedCourses.Clear();
        EvaluationTypes.Clear();
        SavedEvaluations.Clear();
        GradeEntries.Clear();
        IsGridLoaded = false;
        IsParametersExpanded = false;
        IsCreateEvalDialogOpen = false;
        IsEditEvalDialogOpen = false;
        IsCreateEvalWizardOpen = false;
        CreateEvalWizardStep = 1;
        HasActivePeriod = false;
        NoOpenPeriodMessage = null;
        ActivePeriodBanner = "Aucune période ouverte";
        ActivePeriodDetails = string.Empty;
        RefreshStatistics();
        NotifyBanner();
        ApplyConnectedUserIdentity();
        StatusMessage = IsTeacherIdentityLocked
            ? null
            : "Identifiez l'enseignant pour accéder à la cotation.";
    }

    [RelayCommand]
    private async Task BackToAssignmentsAsync()
    {
        IsGradeGridOpen = false;
        IsEvaluationManagerOpen = false;
        IsGlobalCotationOpen = false;
        IsParametersExpanded = false;
        IsCreateEvalDialogOpen = false;
        IsEditEvalDialogOpen = false;
        IsCreateEvalWizardOpen = false;
        CreateEvalWizardStep = 1;
        ManagedEvaluations.Clear();
        ClearDependentSelections(clearClass: true);
        _activeCotationPeriod = null;
        HasActivePeriod = false;
        BannerPeriodKind = "—";
        ActivePeriodBanner = "Aucune période ouverte";
        ActivePeriodDetails = string.Empty;
        NoOpenPeriodMessage = null;
        StatusMessage = null;
        await RefreshAssignmentProgressAsync();
        NotifyBanner();
        NotifyCommands();
    }

    [RelayCommand]
    private async Task BackToEvaluationManagerAsync()
    {
        IsGradeGridOpen = false;
        IsParametersExpanded = false;
        GradeEntries.Clear();
        IsGridLoaded = false;
        CurrentEvaluation = null;
        IsEvaluationManagerOpen = true;
        await ReloadManagedEvaluationsAsync();
        await RefreshAssignmentProgressAsync();
        NotifyBanner();
        NotifyCommands();
    }

    [RelayCommand]
    private async Task CoterAsync(CotationAssignmentCard? card)
    {
        if (card is null || _session is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            await PrepareAssignmentContextAsync(card);

            if (!HasActivePeriod)
            {
                StatusMessage = NoOpenPeriodMessage
                    ?? "Aucune sous-période n'est ouverte. Cotation impossible.";
                IsEvaluationManagerOpen = false;
                IsGradeGridOpen = false;
                return;
            }

            IsGradeGridOpen = false;
            IsEvaluationManagerOpen = true;
            IsParametersExpanded = false;
            await ReloadManagedEvaluationsAsync();
            NotifyBanner();
            NotifyCommands();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            IsEvaluationManagerOpen = false;
            IsGradeGridOpen = false;
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    [RelayCommand]
    private async Task OpenManagedEvaluationAsync(CotationEvaluationListItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await ApplySavedEvaluationAsync(item.Source);
            IsEvaluationManagerOpen = true;
            IsGradeGridOpen = true;
            IsParametersExpanded = false;
            NotifyBanner();
            NotifyCommands();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void SelectEvaluationManagerTab(object? tabIndex)
    {
        if (tabIndex is int i)
        {
            EvaluationManagerTabIndex = i;
            return;
        }

        if (tabIndex is string s && int.TryParse(s, out var parsed))
        {
            EvaluationManagerTabIndex = parsed;
        }
    }

    [RelayCommand(CanExecute = nameof(CanManageEvaluations))]
    private void BeginCreateEvaluation()
    {
        if (!HasManagedEvaluations)
        {
            BeginCreateEvalWizard();
            return;
        }

        PrepareNewEvaluationDefaults();
        _openGradesAfterCreate = false;
        EvalDialogError = null;
        IsCreateEvalDialogOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanManageEvaluations))]
    private void BeginCreateEvalWizard()
    {
        PrepareNewEvaluationDefaults();
        _openGradesAfterCreate = true;
        CreateEvalWizardStep = 1;
        EvalDialogError = null;
        IsCreateEvalDialogOpen = false;
        IsCreateEvalWizardOpen = true;
    }

    private void PrepareNewEvaluationDefaults()
    {
        NewEvalType = EvaluationTypes.FirstOrDefault();
        NewEvalTitle = string.Empty;
        NewEvalDate = DateTime.Today;
        NewEvalMaxScore = SelectedCourse?.MaxPerPeriod > 0
            ? SelectedCourse.MaxPerPeriod
            : EvaluationMaxScore > 0 ? EvaluationMaxScore : 20;
        if (_activeCotationPeriod?.StartDate is DateOnly start
            && _activeCotationPeriod.EndDate is DateOnly end)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (today < start) NewEvalDate = start.ToDateTime(TimeOnly.MinValue);
            else if (today > end) NewEvalDate = end.ToDateTime(TimeOnly.MinValue);
        }
    }

    [RelayCommand]
    private void CancelCreateEvaluation()
    {
        IsCreateEvalDialogOpen = false;
        IsCreateEvalWizardOpen = false;
        CreateEvalWizardStep = 1;
        _openGradesAfterCreate = false;
        EvalDialogError = null;
    }

    [RelayCommand]
    private void CreateEvalWizardBack()
    {
        EvalDialogError = null;
        if (CreateEvalWizardStep > 1)
        {
            CreateEvalWizardStep--;
        }
    }

    [RelayCommand]
    private async Task CreateEvalWizardNextAsync()
    {
        EvalDialogError = null;
        if (CreateEvalWizardStep == 1)
        {
            if (NewEvalType is null)
            {
                EvalDialogError = "Sélectionnez un type d'évaluation.";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewEvalTitle))
            {
                NewEvalTitle = NewEvalType.Name;
            }

            CreateEvalWizardStep = 2;
            return;
        }

        if (CreateEvalWizardStep == 2)
        {
            if (string.IsNullOrWhiteSpace(NewEvalTitle))
            {
                EvalDialogError = "Saisissez un libellé.";
                return;
            }

            if (NewEvalMaxScore <= 0)
            {
                EvalDialogError = "Le maximum de l'évaluation doit être supérieur à 0.";
                return;
            }

            CreateEvalWizardStep = 3;
            _openGradesAfterCreate = true;
            await ConfirmCreateEvaluationAsync();
            if (!string.IsNullOrWhiteSpace(EvalDialogError))
            {
                CreateEvalWizardStep = 2;
            }
        }
    }

    [RelayCommand]
    private async Task ConfirmCreateEvaluationAsync()
    {
        if (SelectedYear is null || SelectedPeriod is null || SelectedLocal is null || SelectedCourse is null)
        {
            EvalDialogError = "Contexte d'affectation incomplet.";
            return;
        }

        if (NewEvalType is null)
        {
            EvalDialogError = "Sélectionnez un type d'évaluation.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewEvalTitle))
        {
            EvalDialogError = "Saisissez un libellé.";
            return;
        }

        if (NewEvalMaxScore <= 0)
        {
            EvalDialogError = "Le maximum de l'évaluation doit être supérieur à 0.";
            return;
        }

        IsBusy = true;
        try
        {
            var beforeIds = ManagedEvaluations.Select(e => e.Id).ToHashSet();
            var created = await _gradeApiService.CreateEvaluationAsync(new CreateEvaluationRequest(
                SelectedYear.Id,
                SelectedPeriod.Id,
                SelectedCourse.CourseId,
                SelectedLocal.Id,
                NewEvalType.Id,
                null,
                NewEvalTitle.Trim(),
                1,
                NewEvalMaxScore,
                DateOnly.FromDateTime(NewEvalDate.Date)));

            IsCreateEvalDialogOpen = false;
            IsCreateEvalWizardOpen = false;
            CreateEvalWizardStep = 1;
            await ReloadManagedEvaluationsAsync();
            await RefreshAssignmentProgressAsync();
            var item = ManagedEvaluations.FirstOrDefault(e => e.Id == created.Id);
            var shouldOpen = _openGradesAfterCreate
                             || (item is not null && beforeIds.Contains(created.Id));
            _openGradesAfterCreate = false;

            if (item is not null && shouldOpen)
            {
                StatusMessage = beforeIds.Contains(created.Id)
                    ? $"Évaluation « {created.Title} » déjà existante — ouverture."
                    : $"Évaluation « {created.Title} » créée — saisie des notes.";
                await OpenManagedEvaluationAsync(item);
            }
            else
            {
                StatusMessage = $"Évaluation « {created.Title} » créée.";
            }
        }
        catch (Exception ex)
        {
            EvalDialogError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectWizardEvalType(EvaluationTypeDto? type)
    {
        if (type is null)
        {
            return;
        }

        NewEvalType = type;
        if (string.IsNullOrWhiteSpace(NewEvalTitle) || EvaluationTypes.Any(t => t.Name == NewEvalTitle))
        {
            NewEvalTitle = type.Name;
        }
    }

    [RelayCommand]
    private void BeginDuplicateEvaluation(CotationEvaluationListItem? item)
    {
        if (item is null || !CanManageEvaluations)
        {
            return;
        }

        NewEvalType = EvaluationTypes.FirstOrDefault(t => t.Id == item.Source.EvaluationTypeId)
                      ?? EvaluationTypes.FirstOrDefault();
        NewEvalTitle = $"{item.Title} (copie)";
        NewEvalDate = DateTime.Today;
        NewEvalMaxScore = item.MaxScore > 0 ? item.MaxScore : 20;
        if (_activeCotationPeriod?.StartDate is DateOnly start
            && _activeCotationPeriod.EndDate is DateOnly end)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (today < start) NewEvalDate = start.ToDateTime(TimeOnly.MinValue);
            else if (today > end) NewEvalDate = end.ToDateTime(TimeOnly.MinValue);
        }

        _openGradesAfterCreate = false;
        EvalDialogError = null;
        IsCreateEvalWizardOpen = false;
        IsCreateEvalDialogOpen = true;
    }

    [RelayCommand]
    private void BeginEditEvaluation(CotationEvaluationListItem? item)
    {
        if (item is null || !item.CanEdit)
        {
            return;
        }

        EditingEvaluation = item;
        NewEvalTitle = item.Title;
        NewEvalDate = item.Source.EvaluationDate.ToDateTime(TimeOnly.MinValue);
        NewEvalMaxScore = item.MaxScore > 0 ? item.MaxScore : 20;
        EvalDialogError = null;
        IsEditEvalDialogOpen = true;
    }

    [RelayCommand]
    private void CancelEditEvaluation()
    {
        IsEditEvalDialogOpen = false;
        EditingEvaluation = null;
        EvalDialogError = null;
    }

    [RelayCommand]
    private async Task ConfirmEditEvaluationAsync()
    {
        if (EditingEvaluation is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewEvalTitle))
        {
            EvalDialogError = "Saisissez un libellé.";
            return;
        }

        if (NewEvalMaxScore <= 0)
        {
            EvalDialogError = "Le maximum de l'évaluation doit être supérieur à 0.";
            return;
        }

        IsBusy = true;
        try
        {
            await _gradeApiService.UpdateEvaluationAsync(
                EditingEvaluation.Id,
                new UpdateEvaluationRequest(
                    NewEvalTitle.Trim(),
                    DateOnly.FromDateTime(NewEvalDate.Date),
                    NewEvalMaxScore));
            IsEditEvalDialogOpen = false;
            EditingEvaluation = null;
            await ReloadManagedEvaluationsAsync();
            await RefreshAssignmentProgressAsync();
            StatusMessage = "Évaluation mise à jour.";
        }
        catch (Exception ex)
        {
            EvalDialogError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteManagedEvaluationAsync(CotationEvaluationListItem? item)
    {
        if (item is null || !item.CanDelete)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"Supprimer l'évaluation « {item.Title} » ?",
            "Confirmation",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _gradeApiService.DeleteEvaluationAsync(item.Id);
            await ReloadManagedEvaluationsAsync();
            await RefreshAssignmentProgressAsync();
            StatusMessage = "Évaluation supprimée.";
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
    private async Task RetryManagedEvaluationsAsync() => await ReloadManagedEvaluationsAsync();

    private async Task ReloadManagedEvaluationsAsync()
    {
        IsManagedEvaluationsLoading = true;
        ManagedEvaluationsError = null;
        ManagedEvaluations.Clear();
        NotifyManagedEvaluationsState();

        try
        {
            if (SelectedLocal is null || SelectedPeriod is null)
            {
                return;
            }

            var courseId = _managedCourseId != Guid.Empty
                ? _managedCourseId
                : SelectedCourse?.CourseId ?? Guid.Empty;
            var assignmentId = _managedAssignmentId ?? SelectedCourse?.AssignmentId;
            var courseName = _managedCourseName ?? SelectedCourse?.CourseName;

            // Même appel que ReloadSavedEvaluationsAsync (ancienne page).
            var evaluations = await _gradeApiService.GetEvaluationsAsync(SelectedLocal.Id, SelectedPeriod.Id);
            ReplaceSavedEvaluations(evaluations);

            var matched = FilterSavedForManagedCourse(courseId, assignmentId, courseName).ToList();

            // Périodes homonymes (même année) si l'Id ouvert ne correspond plus aux données existantes.
            if (matched.Count == 0 && SelectedYear is not null)
            {
                await ReloadSavedEvaluationsIncludingHomonymPeriodsAsync();
                matched = FilterSavedForManagedCourse(courseId, assignmentId, courseName).ToList();
            }

            foreach (var item in matched.OrderByDescending(s => s.Evaluation.EvaluationDate)
                         .ThenBy(s => s.Evaluation.Title))
            {
                ManagedEvaluations.Add(new CotationEvaluationListItem(item.Evaluation, CanManageEvaluations));
            }
        }
        catch (Exception ex)
        {
            ManagedEvaluationsError = ex.Message;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsManagedEvaluationsLoading = false;
            NotifyManagedEvaluationsState();
            OnPropertyChanged(nameof(ManagerStudentCount));
            if (EvaluationManagerTabIndex == 1)
            {
                await ReloadCourseNotesGridAsync();
            }
            else
            {
                _ = ReloadCourseNotesGridAsync();
            }

            if (IsGlobalCotationOpen)
            {
                _ = EnsurePedagogicalSheetLoadedAsync(force: true);
            }
        }
    }

    private void ReplaceSavedEvaluations(IEnumerable<EvaluationDto> evaluations)
    {
        SavedEvaluations.Clear();
        foreach (var evaluation in evaluations
                     .OrderByDescending(e => e.EvaluationDate)
                     .ThenBy(e => e.CourseName)
                     .ThenBy(e => e.Title))
        {
            SavedEvaluations.Add(new SavedEvaluationListItem(evaluation));
        }
    }

    private IEnumerable<SavedEvaluationListItem> FilterSavedForManagedCourse(
        Guid courseId,
        Guid? assignmentId,
        string? courseName)
    {
        if (courseId == Guid.Empty
            && assignmentId is null
            && string.IsNullOrWhiteSpace(courseName))
        {
            return SavedEvaluations;
        }

        return SavedEvaluations.Where(s =>
        {
            var e = s.Evaluation;
            if (courseId != Guid.Empty && e.CourseId == courseId)
            {
                return true;
            }

            if (assignmentId is Guid aid && aid != Guid.Empty && e.CourseAssignmentId == aid)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(courseName)
                   && string.Equals(e.CourseName, courseName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private async Task ReloadSavedEvaluationsIncludingHomonymPeriodsAsync()
    {
        if (SelectedLocal is null || SelectedPeriod is null || SelectedYear is null)
        {
            return;
        }

        var lookups = await _schoolApiService.GetLookupsAsync();
        var periodIds = lookups.AcademicPeriods
            .Where(p => p.AcademicYearId == SelectedYear.Id
                        && string.Equals(p.Name, SelectedPeriod.Name, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .Distinct()
            .ToList();

        if (periodIds.Count == 0 || (periodIds.Count == 1 && periodIds[0] == SelectedPeriod.Id))
        {
            return;
        }

        if (!periodIds.Contains(SelectedPeriod.Id))
        {
            periodIds.Insert(0, SelectedPeriod.Id);
        }

        var byId = new Dictionary<Guid, EvaluationDto>();
        foreach (var periodId in periodIds)
        {
            var batch = await _gradeApiService.GetEvaluationsAsync(SelectedLocal.Id, periodId);
            foreach (var evaluation in batch)
            {
                byId[evaluation.Id] = evaluation;
            }
        }

        ReplaceSavedEvaluations(byId.Values);
    }

    private void NotifyManagedEvaluationsState()
    {
        OnPropertyChanged(nameof(HasManagedEvaluations));
        OnPropertyChanged(nameof(ShowManagedEvaluationsLoading));
        OnPropertyChanged(nameof(ShowManagedEvaluationsEmpty));
        OnPropertyChanged(nameof(ShowManagedEvaluationsError));
        OnPropertyChanged(nameof(ShowManagedEvaluationsGrid));
        OnPropertyChanged(nameof(ShowManagerTabPlaceholder));
        OnPropertyChanged(nameof(ShowCreateEvaluationInManager));
        OnPropertyChanged(nameof(ManagerKpiEvaluations));
        OnPropertyChanged(nameof(ManagerKpiStudents));
        OnPropertyChanged(nameof(ManagerKpiGradesEntered));
        OnPropertyChanged(nameof(ManagerKpiAverage));
        OnPropertyChanged(nameof(ManagerStudentBadge));
        OnPropertyChanged(nameof(BannerPeriodRange));
        OnPropertyChanged(nameof(ManagerStudentCount));
    }

    private async Task PrepareAssignmentContextAsync(CotationAssignmentCard card)
    {
        if (_session is null || SelectedYear is null)
        {
            throw new InvalidOperationException("Session cotation inactive.");
        }

        _managedCourseId = card.CourseId;
        _managedAssignmentId = card.AssignmentId;
        _managedCourseName = card.CourseName;

        var local = ClassLocals.FirstOrDefault(c => c.Id == card.ClassRoomId)
            ?? throw new InvalidOperationException("Classe introuvable dans la session.");

        _suppressCascade = true;
        try
        {
            SelectedLocal = local;
            SelectedPeriod = null;
            SelectedEvaluationType = null;
            SelectedCourse = null;
            SelectedSavedEvaluation = null;
            CurrentEvaluation = null;
            GradeEntries.Clear();
            IsGridLoaded = false;
        }
        finally
        {
            _suppressCascade = false;
        }

        await LoadActivePeriodAndCoursesAsync();

        var course = AssignedCourses.FirstOrDefault(c => c.CourseId == card.CourseId)
            ?? AssignedCourses.FirstOrDefault(c =>
                string.Equals(c.CourseName, card.CourseName, StringComparison.OrdinalIgnoreCase));
        if (course is null)
        {
            throw new InvalidOperationException($"Cours « {card.CourseName} » introuvable pour cette classe.");
        }

        _suppressCascade = true;
        try
        {
            SelectedCourse = course;
            _managedCourseId = course.CourseId;
            _managedAssignmentId = course.AssignmentId ?? card.AssignmentId;
            _managedCourseName = course.CourseName;
            EvaluationMaxScore = course.MaxPerPeriod > 0 ? course.MaxPerPeriod : 20;
            TeacherDisplayName = string.IsNullOrWhiteSpace(card.TeacherDisplayName)
                ? TeacherDisplayName
                : card.TeacherDisplayName;
        }
        finally
        {
            _suppressCascade = false;
        }
    }

    private async Task ApplySessionAsync(CotationSessionDto session)
    {
        _suppressCascade = true;
        try
        {
            _session = session;
            SelectedYear = SessionYears.FirstOrDefault(y => y.Id == session.AcademicYearId)
                ?? AcademicYearRefreshBridge.SelectedYear
                ?? SessionYear;
            SessionYear = SelectedYear;
            TeacherEmployeeNumber = string.IsNullOrWhiteSpace(session.EmployeeNumber)
                ? TeacherEmployeeNumber
                : session.EmployeeNumber;
            TeacherDisplayName = session.TeacherDisplayName;
            SessionAccessLabel = session.AccessScope switch
            {
                CotationAccessScope.Full => "Accès complet (Direction / Admin)",
                CotationAccessScope.Prefet => "Préfet des études — enseignant sélectionné",
                CotationAccessScope.ClassHolder => "Titulaire — classes affectées",
                _ => "Enseignant — affectations personnelles"
            };

            CotationClasses.Clear();
            ClassLocals.Clear();
            foreach (var c in session.Classes)
            {
                CotationClasses.Add(c);
                ClassLocals.Add(ToClassLocal(c, session.AcademicYearId));
            }

            _sessionEvaluationTypes.Clear();
            _sessionEvaluationTypes.AddRange(session.EvaluationTypes);
            RefreshAvailableEvaluationTypes();

            AssignmentCards.Clear();
            foreach (var a in session.Assignments)
            {
                AssignmentCards.Add(new CotationAssignmentCard(a));
            }

            RebuildAssignmentClassGroups();

            ClearDependentSelections(clearClass: true);
            IsEvaluationManagerOpen = false;
            IsGradeGridOpen = false;
            IsSessionOpen = true;
            IsParametersExpanded = false;
            StatusMessage = null;
            NotifyBanner();
        }
        finally
        {
            _suppressCascade = false;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ToggleAssignmentClassGroup(CotationClassGroup? group)
    {
        if (group is null)
        {
            return;
        }

        group.IsExpanded = !group.IsExpanded;
    }

    private void RebuildAssignmentClassGroups()
    {
        var expandedIds = AssignmentClassGroups
            .Where(g => g.IsExpanded)
            .Select(g => g.ClassRoomId)
            .ToHashSet();

        AssignmentClassGroups.Clear();

        // Ordre des classes = ordre actuel des affectations ; cours = ordre actuel dans chaque classe.
        foreach (var group in AssignmentCards.GroupBy(c => c.ClassRoomId))
        {
            var first = group.First();
            var classInfo = CotationClasses.FirstOrDefault(c => c.ClassRoomId == first.ClassRoomId);
            var cycleLabel = ResolveAssignmentCycleLabel(classInfo, first.SectionName);
            var classGroup = new CotationClassGroup(
                first.ClassRoomId,
                first.ClassName,
                cycleLabel,
                first.StudentCount);

            foreach (var course in group)
            {
                classGroup.Courses.Add(course);
            }

            classGroup.IsExpanded = expandedIds.Contains(classGroup.ClassRoomId);
            classGroup.NotifySummaryChanged();
            AssignmentClassGroups.Add(classGroup);
        }
    }

    private async Task RefreshAssignmentProgressAsync()
    {
        if (_session is null || SelectedYear is null)
        {
            return;
        }

        try
        {
            var assignments = await _gradeApiService.GetCotationAssignmentsAsync(
                SelectedYear.Id,
                _session.TeacherId);

            var byId = assignments.ToDictionary(a => a.AssignmentId);
            foreach (var card in AssignmentCards)
            {
                if (byId.TryGetValue(card.AssignmentId, out var dto))
                {
                    card.ApplyProgress(dto);
                }
            }

            // Nouvelles affectations éventuelles
            foreach (var dto in assignments)
            {
                if (AssignmentCards.All(c => c.AssignmentId != dto.AssignmentId))
                {
                    AssignmentCards.Add(new CotationAssignmentCard(dto));
                }
            }

            RebuildAssignmentClassGroups();
        }
        catch
        {
            // Rafraîchissement opportuniste : ne bloque pas la navigation.
        }
    }

    private static string ResolveAssignmentCycleLabel(CotationClassDto? classInfo, string sectionName)
    {
        if (classInfo?.Program is SchoolProgram program)
        {
            return program switch
            {
                SchoolProgram.Maternelle => "Maternelle",
                SchoolProgram.Primaire => "Primaire",
                SchoolProgram.CTEB => "CTEB",
                SchoolProgram.Humanites => "Humanités",
                SchoolProgram.HumanitesProfessionnelles => "Humanités professionnelles",
                SchoolProgram.FilieresSpecialisees => "Filières spécialisées",
                _ => string.IsNullOrWhiteSpace(sectionName) ? "—" : sectionName
            };
        }

        return string.IsNullOrWhiteSpace(sectionName) ? "—" : sectionName;
    }

    private void ClearWorkspaceSelections()
    {
        _suppressCascade = true;
        try
        {
            SelectedPedagogicalClass = null;
            SelectedLocal = null;
            SelectedPeriod = null;
            SelectedEvaluationType = null;
            SelectedCourse = null;
            SelectedSavedEvaluation = null;
            CurrentEvaluation = null;
        }
        finally
        {
            _suppressCascade = false;
        }
    }

    private void ClearDependentSelections(bool clearClass)
    {
        _suppressCascade = true;
        try
        {
            if (clearClass)
            {
                SelectedLocal = null;
                SelectedPedagogicalClass = null;
            }

            SelectedPeriod = null;
            SelectedEvaluationType = null;
            SelectedCourse = null;
            SelectedSavedEvaluation = null;
            CurrentEvaluation = null;
            CotationPeriods.Clear();
            AssignedCourses.Clear();
            SavedEvaluations.Clear();
            GradeEntries.Clear();
            IsGridLoaded = false;
            RefreshStatistics();
        }
        finally
        {
            _suppressCascade = false;
        }
    }

    private async Task OnCotationClassChangedAsync()
    {
        if (_suppressCascade || !IsSessionOpen || _session is null || (!IsEvaluationManagerOpen && !IsGradeGridOpen))
        {
            return;
        }

        await LoadActivePeriodAndCoursesAsync();
        if (IsEvaluationManagerOpen)
        {
            await ReloadManagedEvaluationsAsync();
        }

        NotifyBanner();
        NotifyCommands();
    }

    private async Task LoadActivePeriodAndCoursesAsync()
    {
        if (_session is null || SelectedLocal is null || SelectedYear is null)
        {
            return;
        }

        ClearDependentSelections(clearClass: false);
        SummarySectionName = "—";
        _activeCotationPeriod = null;
        BannerPeriodKind = "—";

        var classInfo = CotationClasses.FirstOrDefault(c => c.ClassRoomId == SelectedLocal.Id);
        SummarySectionName = classInfo?.SectionName ?? "—";

        var periods = await _gradeApiService.GetCotationPeriodsAsync(SelectedYear.Id, SelectedLocal.Id);
        CotationPeriods.Clear();
        foreach (var p in periods)
        {
            CotationPeriods.Add(p);
        }

        Lookups = BuildSyntheticLookups(SelectedYear.Id, periods);
        HasActivePeriod = CotationPeriods.Count > 0;
        PeriodSelectionLocked = true;
        NoOpenPeriodMessage = HasActivePeriod
            ? null
            : "Aucune sous-période n'est ouverte pour ce cycle. Contactez l'administration pédagogique.";

        if (CotationPeriods.Count >= 1)
        {
            var active = CotationPeriods[0];
            _activeCotationPeriod = active;
            _suppressCascade = true;
            try
            {
                SelectedPeriod = new AcademicPeriodLookupDto(
                    active.Id,
                    active.Name,
                    SelectedYear.Id,
                    active.OrderIndex);
            }
            finally
            {
                _suppressCascade = false;
            }

            ActivePeriodBanner = active.Name;
            BannerPeriodKind = active.KindLabel;
            var range = active.StartDate is not null && active.EndDate is not null
                ? $"{active.StartDate:dd/MM/yyyy} → {active.EndDate:dd/MM/yyyy}"
                : string.Empty;
            ActivePeriodDetails = string.IsNullOrEmpty(range)
                ? $"{active.KindLabel} — Ouverte"
                : $"{active.KindLabel} — {range}";
        }
        else
        {
            _suppressCascade = true;
            try
            {
                SelectedPeriod = null;
            }
            finally
            {
                _suppressCascade = false;
            }

            ActivePeriodBanner = "Aucune période ouverte";
            ActivePeriodDetails = string.Empty;
        }

        OnPropertyChanged(nameof(CanEnterGrades));
        OnPropertyChanged(nameof(CanManageEvaluations));
        OnPropertyChanged(nameof(CanEditGrades));
        BeginCreateEvaluationCommand.NotifyCanExecuteChanged();

        AssignedCourses.Clear();
        foreach (var a in _session.Assignments.Where(x => x.ClassRoomId == SelectedLocal.Id)
                     .OrderBy(x => x.CourseName))
        {
            AssignedCourses.Add(new CourseConfigurationItemDto(
                a.AssignmentId,
                a.CourseId,
                a.CourseName,
                a.CourseName,
                null,
                null,
                a.TeacherId,
                a.TeacherDisplayName,
                true,
                a.MaxScore,
                a.WeeklyHours));
        }

        RefreshAvailableEvaluationTypes();
        NotifyBanner();
        NotifyCommands();
    }

    /// <summary>
    /// Sous-période « Travaux / Période » : le type Examen n'est pas proposé.
    /// Sous-période « Examen » : tous les types restent disponibles.
    /// </summary>
    private void RefreshAvailableEvaluationTypes()
    {
        var selectedId = SelectedEvaluationType?.Id;
        var newId = NewEvalType?.Id;

        var filtered = _sessionEvaluationTypes.AsEnumerable();
        if (_activeCotationPeriod?.Kind == AcademicSubPeriodKind.Travail)
        {
            filtered = filtered.Where(t => !IsExamenEvaluationType(t));
        }

        EvaluationTypes.Clear();
        foreach (var type in filtered)
        {
            EvaluationTypes.Add(type);
        }

        SelectedEvaluationType = selectedId is Guid sid
            ? EvaluationTypes.FirstOrDefault(t => t.Id == sid)
            : null;
        NewEvalType = newId is Guid nid
            ? EvaluationTypes.FirstOrDefault(t => t.Id == nid) ?? EvaluationTypes.FirstOrDefault()
            : NewEvalType is not null
                ? EvaluationTypes.FirstOrDefault(t => t.Id == NewEvalType.Id) ?? EvaluationTypes.FirstOrDefault()
                : NewEvalType;
    }

    private static bool IsExamenEvaluationType(EvaluationTypeDto type) =>
        string.Equals(type.Code, "EXAMEN", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type.Name, "Examen", StringComparison.OrdinalIgnoreCase);

    private void OnCotationPeriodChanged()
    {
        if (_suppressCascade || !IsSessionOpen || (!IsGradeGridOpen && !IsEvaluationManagerOpen))
        {
            return;
        }

        _suppressCascade = true;
        try
        {
            SelectedEvaluationType = null;
            SelectedSavedEvaluation = null;
            CurrentEvaluation = null;
            GradeEntries.Clear();
            IsGridLoaded = false;
            RefreshStatistics();
        }
        finally
        {
            _suppressCascade = false;
        }

        if (IsEvaluationManagerOpen)
        {
            _ = ReloadManagedEvaluationsAsync();
        }
        else
        {
            _ = ReloadSavedEvaluationsAsync();
        }

        NotifyBanner();
        NotifyCommands();
    }

    private void OnCotationEvaluationTypeChanged()
    {
        if (_suppressCascade || !IsSessionOpen || !IsGradeGridOpen)
        {
            return;
        }

        GradeEntries.Clear();
        IsGridLoaded = false;
        CurrentEvaluation = null;
        RefreshStatistics();
        NotifyBanner();
        NotifyCommands();
        _ = TryAutoLoadStudentsAsync();
    }

    private void OnCotationCourseChanged()
    {
        if (_suppressCascade || !IsSessionOpen || (!IsGradeGridOpen && !IsEvaluationManagerOpen))
        {
            return;
        }

        if (SelectedCourse is not null)
        {
            _managedCourseId = SelectedCourse.CourseId;
            _managedAssignmentId = SelectedCourse.AssignmentId;
            _managedCourseName = SelectedCourse.CourseName;
            if (SelectedCourse.MaxPerPeriod > 0)
            {
                EvaluationMaxScore = SelectedCourse.MaxPerPeriod;
            }
        }

        GradeEntries.Clear();
        IsGridLoaded = false;
        CurrentEvaluation = null;
        RefreshStatistics();
        NotifyBanner();
        NotifyCommands();

        if (IsEvaluationManagerOpen)
        {
            _ = ReloadManagedEvaluationsAsync();
        }
        else
        {
            _ = TryAutoLoadStudentsAsync();
        }
    }

    private static ClassLocalDto ToClassLocal(CotationClassDto c, Guid yearId) =>
        new(
            c.ClassRoomId,
            c.PedagogicalClassId ?? Guid.Empty,
            yearId,
            c.PedagogicalClassName ?? c.DisplayName,
            c.DisplayName,
            c.DisplayName,
            c.DisplayName,
            null,
            c.SectionName,
            true);

    private static SchoolLookupsDto BuildSyntheticLookups(
        Guid yearId,
        IReadOnlyList<CotationPeriodDto> periods) =>
        new(
            [],
            periods.Select(p => new AcademicPeriodLookupDto(p.Id, p.Name, yearId, p.OrderIndex)).ToList(),
            [],
            [],
            [],
            []);

    private void NotifyBanner()
    {
        OnPropertyChanged(nameof(BannerTeacher));
        OnPropertyChanged(nameof(BannerClass));
        OnPropertyChanged(nameof(BannerSection));
        OnPropertyChanged(nameof(BannerCourse));
        OnPropertyChanged(nameof(BannerPeriod));
        OnPropertyChanged(nameof(BannerPeriodKind));
        OnPropertyChanged(nameof(BannerEvaluation));
        OnPropertyChanged(nameof(BannerStudents));
        OnPropertyChanged(nameof(BannerGraded));
        OnPropertyChanged(nameof(BannerRemaining));
        OnPropertyChanged(nameof(BannerAverage));
        OnPropertyChanged(nameof(BannerPeriodRange));
        OnPropertyChanged(nameof(ManagerStudentBadge));
        OnPropertyChanged(nameof(ManagerStudentCount));
    }
}

public partial class CotationAssignmentCard : ObservableObject
{
    public CotationAssignmentCard(CotationAssignmentDto dto)
    {
        AssignmentId = dto.AssignmentId;
        ClassRoomId = dto.ClassRoomId;
        CourseId = dto.CourseId;
        ClassName = dto.ClassDisplayName;
        SectionName = dto.SectionName;
        CourseName = dto.CourseName;
        TeacherDisplayName = dto.TeacherDisplayName;
        StudentCount = dto.StudentCount;
        MaxScore = dto.MaxScore;
        ApplyProgress(dto);
    }

    public Guid AssignmentId { get; }
    public Guid ClassRoomId { get; }
    public Guid CourseId { get; }
    public string ClassName { get; }
    public string SectionName { get; }
    public string CourseName { get; }
    public string TeacherDisplayName { get; }
    public int StudentCount { get; }
    public int MaxScore { get; }

    [ObservableProperty] private int _evaluationCount;
    [ObservableProperty] private string _evaluationsText = "0";
    [ObservableProperty] private string _lastActivityTitle = "Aucune activité";
    [ObservableProperty] private string? _lastActivityDateText;
    [ObservableProperty] private bool _hasLastActivityDate;
    [ObservableProperty] private string _actionLabel = "COMMENCER";
    [ObservableProperty] private bool _hasOpenPeriod = true;

    public void ApplyProgress(CotationAssignmentDto dto)
    {
        EvaluationCount = dto.EvaluationCount;
        EvaluationsText = dto.EvaluationCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        HasOpenPeriod = dto.HasOpenPeriod;

        if (dto.EvaluationCount > 0
            && !string.IsNullOrWhiteSpace(dto.LastEvaluationTitle)
            && dto.LastEvaluationDate is DateOnly date)
        {
            LastActivityTitle = dto.LastEvaluationTitle;
            LastActivityDateText = date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.CurrentCulture);
            HasLastActivityDate = true;
        }
        else
        {
            LastActivityTitle = "Aucune activité";
            LastActivityDateText = null;
            HasLastActivityDate = false;
        }

        ActionLabel = !dto.HasOpenPeriod
            ? "CONSULTER"
            : dto.EvaluationCount <= 0
                ? "COMMENCER"
                : "CONTINUER";
    }
}

public partial class CotationClassGroup : ObservableObject
{
    public CotationClassGroup(Guid classRoomId, string className, string cycleLabel, int studentCount)
    {
        ClassRoomId = classRoomId;
        ClassName = className;
        CycleLabel = cycleLabel;
        StudentCount = studentCount;
    }

    public Guid ClassRoomId { get; }
    public string ClassName { get; }
    public string CycleLabel { get; }
    public int StudentCount { get; }
    public ObservableCollection<CotationAssignmentCard> Courses { get; } = [];

    public int CourseCount => Courses.Count;

    public int GradedCourseCount => Courses.Count(c => c.EvaluationCount > 0);

    public string StudentCountLabel =>
        StudentCount <= 1 ? $"{StudentCount} élève" : $"{StudentCount} élèves";

    public string CourseCountLabel =>
        CourseCount <= 1 ? $"{CourseCount} cours" : $"{CourseCount} cours";

    public string GradedCoursesLabel =>
        $"Cours cotés {GradedCourseCount} / {CourseCount}";

    public string SummaryText =>
        $"{CycleLabel} • {StudentCountLabel} • {CourseCountLabel} • {GradedCoursesLabel}";

    public void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(CourseCount));
        OnPropertyChanged(nameof(GradedCourseCount));
        OnPropertyChanged(nameof(CourseCountLabel));
        OnPropertyChanged(nameof(GradedCoursesLabel));
        OnPropertyChanged(nameof(SummaryText));
    }

    [ObservableProperty]
    private bool _isExpanded;
}

public sealed class CotationEvaluationListItem
{
    public CotationEvaluationListItem(EvaluationDto dto, bool periodOpen)
    {
        Source = dto;
        Id = dto.Id;
        DateText = dto.EvaluationDate.ToString("dd/MM/yyyy");
        TypeName = dto.EvaluationTypeName;
        Title = dto.Title;
        MaxScore = dto.MaxScore;
        MaxScoreLabel = $"/{dto.MaxScore}";
        GradedCount = dto.GradedCount;
        StudentCount = dto.StudentCount;
        ProgressText = $"{dto.GradedCount} / {dto.StudentCount}";
        CreatedDateText = DateText;
        ModifiedDateText = DateText;
        StatusLabel = dto.GradedCount <= 0
            ? "Non commencée"
            : dto.GradedCount >= dto.StudentCount && dto.StudentCount > 0
                ? "Terminée"
                : "En cours";
        StatusTone = dto.GradedCount <= 0
            ? "Pending"
            : dto.GradedCount >= dto.StudentCount && dto.StudentCount > 0
                ? "Done"
                : "Progress";
        CanEdit = periodOpen;
        CanDelete = periodOpen && dto.GradedCount == 0;
        CanDuplicate = periodOpen;
        CanEnterGrades = periodOpen || dto.GradedCount > 0;
    }

    public EvaluationDto Source { get; }
    public Guid Id { get; }
    public string DateText { get; }
    public string TypeName { get; }
    public string Title { get; }
    public int MaxScore { get; }
    public string MaxScoreLabel { get; }
    public int GradedCount { get; }
    public int StudentCount { get; }
    public string ProgressText { get; }
    public string CreatedDateText { get; }
    public string ModifiedDateText { get; }
    public string StatusLabel { get; }
    public string StatusTone { get; }
    public bool CanEdit { get; }
    public bool CanDelete { get; }
    public bool CanDuplicate { get; }
    public bool CanEnterGrades { get; }
}
