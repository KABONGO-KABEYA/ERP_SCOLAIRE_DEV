using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Admin.DTOs;
using SchoolManagement.Application.CourseConfiguration.DTOs;
using SchoolManagement.Application.Schools;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;

namespace SchoolManagement.Desktop.ViewModels;

public partial class CourseConfigurationViewModel : ViewModelBase
{
    private readonly ICourseConfigurationApiService _courseConfigurationApi;
    private readonly ISchoolApiService _schoolApiService;
    private readonly IAcademicApiService _academicApiService;
    private readonly IAdminApiService _adminApiService;
    private string? _configurationSnapshot;
    private int _availableCoursesLoadVersion;
    private int _filtersChangeVersion;
    private bool _isInitializingFilters;
    private bool _isRefreshingClassRooms;

    private readonly List<PedagogicalClassFilterItem> _allPedagogicalClasses = [];
    private readonly Dictionary<Guid, PedagogicalClassDto> _pedagogicalClassMap = [];

    public CourseConfigurationViewModel(
        ICourseConfigurationApiService courseConfigurationApi,
        ISchoolApiService schoolApiService,
        IAcademicApiService academicApiService,
        IAdminApiService adminApiService)
    {
        _courseConfigurationApi = courseConfigurationApi;
        _schoolApiService = schoolApiService;
        _academicApiService = academicApiService;
        _adminApiService = adminApiService;
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
    }

    private void OnGlobalAcademicYearChanged()
    {
        if (_isInitializingFilters)
            return;

        _ = OnGlobalAcademicYearChangedAsync();
    }

    private async Task OnGlobalAcademicYearChangedAsync()
    {
        _isInitializingFilters = true;
        try
        {
            await RefreshClassRoomsAsync();
            EnsureSelectedPedagogicalClassIsValid();
            if (SelectedClassRoom is null)
                SelectedClassRoom = ClassRooms.FirstOrDefault();
        }
        finally
        {
            _isInitializingFilters = false;
        }

        OnPropertyChanged(nameof(CanEditConfiguration));
        OnPropertyChanged(nameof(WorkingAcademicYearLabel));
        await OnFiltersChangedAsync();
    }

    public string WorkingAcademicYearLabel =>
        AcademicYearRefreshBridge.SelectedYear?.Label ?? "Aucune année scolaire (barre du haut)";

    public ObservableCollection<PedagogicalClassDto> PedagogicalClasses { get; } = [];
    public ObservableCollection<ClassRoomDto> ClassRooms { get; } = [];
    public ObservableCollection<TeacherOptionViewModel> Teachers { get; } = [];
    public ObservableCollection<AvailableBranchGroupViewModel> AvailableBranchGroups { get; } = [];
    public ObservableCollection<AssignedCourseItemViewModel> AssignedCourses { get; } = [];
    public ObservableCollection<AssignedBranchGroupViewModel> AssignedBranchGroups { get; } = [];
    public ObservableCollection<SectionDto> Sections { get; } = [];
    public ObservableCollection<BranchOptionDto> BranchOptions { get; } = [];

    [ObservableProperty] private SectionDto? _selectedSection;
    [ObservableProperty] private PedagogicalClassDto? _selectedPedagogicalClass;
    [ObservableProperty] private ClassRoomDto? _selectedClassRoom;
    [ObservableProperty] private string _availableSearchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isConfigured;
    [ObservableProperty] private bool _isPrimaryLevel;
    [ObservableProperty] private bool _useSameTeacherForAll;
    [ObservableProperty] private TeacherOptionViewModel? _sharedTeacher;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private FeeStatusMessageKind _statusMessageKind = FeeStatusMessageKind.None;
    [ObservableProperty] private bool _isNewCoursePanelVisible;
    [ObservableProperty] private string _newCourseName = string.Empty;
    [ObservableProperty] private BranchOptionDto? _selectedNewCourseBranch;
    [ObservableProperty] private string _newCourseMaxScore = "20";

    public bool CanEditConfiguration =>
        AcademicYearRefreshBridge.SelectedYearId is not null
        && SelectedPedagogicalClass is not null
        && SelectedClassRoom is not null;

    partial void OnSelectedSectionChanged(SectionDto? value)
    {
        RefreshPedagogicalClassOptions();
        EnsureSelectedPedagogicalClassIsValid();

        if (_isInitializingFilters)
        {
            return;
        }

        _ = OnPedagogicalClassChangedAsync();
    }

    partial void OnSelectedPedagogicalClassChanged(PedagogicalClassDto? value)
    {
        if (_isInitializingFilters)
        {
            return;
        }

        _ = OnPedagogicalClassChangedAsync();
    }

    private async Task OnPedagogicalClassChangedAsync()
    {
        await RefreshClassRoomsAsync();
        await OnFiltersChangedAsync();
    }

    partial void OnSelectedClassRoomChanged(ClassRoomDto? value)
    {
        if (_isInitializingFilters || _isRefreshingClassRooms)
        {
            return;
        }

        _ = OnFiltersChangedAsync();
    }

    partial void OnAvailableSearchTextChanged(string value) => RefreshAvailableVisibility();

    partial void OnUseSameTeacherForAllChanged(bool value)
    {
        if (value)
        {
            SharedTeacher = Teachers.FirstOrDefault(t => t.Id == SharedTeacher?.Id) ?? TeacherOptionViewModel.Unassigned;
            ApplySharedTeacher();
        }
    }

    partial void OnSharedTeacherChanged(TeacherOptionViewModel? value)
    {
        if (UseSameTeacherForAll)
        {
            ApplySharedTeacher();
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ClearStatus();

            _isInitializingFilters = true;
            try
            {
                await ReloadOrganizedSectionsAsync();
                await ReloadPedagogicalClassesAsync();
                RefreshPedagogicalClassOptions();
                SelectedSection ??= Sections.FirstOrDefault();
                EnsureSelectedPedagogicalClassIsValid();
                await RefreshClassRoomsAsync();
                SelectedClassRoom ??= ClassRooms.FirstOrDefault();
            }
            finally
            {
                _isInitializingFilters = false;
            }

            if (AcademicYearRefreshBridge.SelectedYearId is null)
            {
                SetStatus("Sélectionnez une année scolaire dans la barre du haut.", FeeStatusMessageKind.Warning);
            }
            else if (Sections.Count == 0)
            {
                SetStatus("Aucune section organisée. Activez des classes dans Structure pédagogique.", FeeStatusMessageKind.Warning);
            }

            var teachers = await _adminApiService.GetTeachersAsync();
            Teachers.Clear();
            Teachers.Add(TeacherOptionViewModel.Unassigned);
            foreach (var teacher in teachers.Where(t => t.IsActive).OrderBy(t => t.LastName).ThenBy(t => t.FirstName))
            {
                Teachers.Add(TeacherOptionViewModel.From(teacher));
            }

            RefreshAssignedCourseTeachers();

            var branches = await _courseConfigurationApi.GetBranchesAsync();
            BranchOptions.Clear();
            foreach (var branch in branches)
            {
                BranchOptions.Add(branch);
            }

            await LoadAvailableCoursesAsync();
            await LoadConfigurationIfReadyAsync();

            if (SelectedPedagogicalClass is not null && AvailableBranchGroups.Count == 0)
            {
                SetStatus(
                    "Aucun cours par défaut pour cette classe. Vérifiez que le curriculum (PedagogicalClassCourse) est initialisé.",
                    FeeStatusMessageKind.Warning);
            }
        }
        catch (HttpRequestException ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        finally
        {
            IsBusy = false;
        }

        OnPropertyChanged(nameof(CanEditConfiguration));
        AddSelectedCoursesCommand.NotifyCanExecuteChanged();
        RemoveSelectedCoursesCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RefreshConfigurationCommand.NotifyCanExecuteChanged();
        ConfirmCreateCourseCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanEditConfiguration))]
    private async Task RefreshConfigurationAsync() => await LoadConfigurationIfReadyAsync();

    [RelayCommand(CanExecute = nameof(CanEditConfiguration))]
    private void AddSelectedCourses()
    {
        var selected = AvailableBranchGroups
            .SelectMany(g => g.Courses)
            .Where(c => c.IsSelected && c.IsVisible && !c.IsAlreadyAssigned)
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Code) ? c.CourseId.ToString() : c.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (selected.Count == 0)
        {
            SetStatus("Sélectionnez au moins un cours disponible.", FeeStatusMessageKind.Warning);
            return;
        }

        foreach (var course in selected)
        {
            if (AssignedCourses.Any(a => a.CourseId == course.CourseId))
            {
                continue;
            }

            var teacherId = UseSameTeacherForAll ? SharedTeacher?.Id : null;
            AssignedCourses.Add(new AssignedCourseItemViewModel(
                null,
                course.CourseId,
                course.Code,
                course.Name,
                course.BranchId,
                course.BranchName,
                teacherId,
                Teachers,
                true,
                course.MaxPerPeriod,
                0,
                OnAssignedCourseChanged));
            course.IsSelected = false;
        }

        AssignedCourses.Sort();
        RebuildAssignedBranchGroups();
        RefreshAvailableVisibility();
        SetStatus($"{selected.Count} cours ajouté(s).", FeeStatusMessageKind.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditConfiguration))]
    private void RemoveSelectedCourses()
    {
        var selected = AssignedCourses.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            SetStatus("Sélectionnez au moins un cours à retirer.", FeeStatusMessageKind.Warning);
            return;
        }

        foreach (var item in selected)
        {
            AssignedCourses.Remove(item);
        }

        RebuildAssignedBranchGroups();
        RefreshAvailableVisibility();
        SetStatus($"{selected.Count} cours retiré(s).", FeeStatusMessageKind.Success);
    }

    [RelayCommand(CanExecute = nameof(CanEditConfiguration))]
    private async Task SaveAsync()
    {
        if (!CanEditConfiguration
            || AcademicYearRefreshBridge.SelectedYearId is not Guid yearId
            || SelectedPedagogicalClass is null
            || SelectedClassRoom is null)
        {
            return;
        }

        var yearLabel = AcademicYearRefreshBridge.SelectedYear?.Label ?? yearId.ToString();
        if (AssignedCourses.Any(c => c.Maximum <= 0 || c.Maximum > 1000))
        {
            SetStatus("Chaque Max/P doit être compris entre 1 et 1000.", FeeStatusMessageKind.Warning);
            return;
        }

        if (AssignedCourses.Any(c => c.WeeklyHours < 0 || c.WeeklyHours > 60))
        {
            SetStatus("Chaque nombre d'heures doit être compris entre 0 et 60.", FeeStatusMessageKind.Warning);
            return;
        }

        if (AssignedCourses.Count == 0)
        {
            SetStatus("Ajoutez au moins un cours avant d'enregistrer.", FeeStatusMessageKind.Warning);
            return;
        }

        var confirmationMessage =
            $"Voulez-vous enregistrer la configuration de {AssignedCourses.Count} cours pour " +
            $"{SelectedPedagogicalClass.DisplayName} — salle {SelectedClassRoom.Name} ({yearLabel}) ?\n\n" +
            "Les affectations seront enregistrées (CourseAssignment : cours, enseignant, heures, statut actif).";

        if (MessageBox.Show(
                confirmationMessage,
                "Confirmer l'enregistrement",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ClearStatus();

            var request = new SaveCourseConfigurationRequest(
                yearId,
                SelectedPedagogicalClass.Id,
                SelectedClassRoom.Id,
                AssignedCourses.Select(c => new SaveCourseConfigurationItemRequest(
                    c.CourseId,
                    c.TeacherId,
                    c.IsActive,
                    c.Maximum,
                    c.WeeklyHours)).ToList());

            var result = await _courseConfigurationApi.SaveConfigurationAsync(request);
            ApplyConfiguration(result);
            CaptureSnapshot();

            var successMessage =
                $"Configuration enregistrée : {AssignedCourses.Count} cours affecté(s) pour {SelectedClassRoom.Name} " +
                $"(CourseAssignment — année {yearLabel}).";
            SetStatus(successMessage, FeeStatusMessageKind.Success);
            MessageBox.Show(
                successMessage,
                "Enregistrement réussi",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
            MessageBox.Show(
                ex.Message,
                "Erreur d'enregistrement",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditConfiguration))]
    private async Task CancelAsync()
    {
        if (string.IsNullOrWhiteSpace(_configurationSnapshot))
        {
            await LoadConfigurationIfReadyAsync();
            SetStatus("Modifications annulées.", FeeStatusMessageKind.None);
            return;
        }

        RestoreSnapshot();
        ClearAvailableSelections();
        SetStatus("Modifications annulées.", FeeStatusMessageKind.None);
    }

    [RelayCommand]
    private void ShowNewCoursePanel()
    {
        if (SelectedPedagogicalClass is null)
        {
            SetStatus("Sélectionnez d'abord une classe.", FeeStatusMessageKind.Warning);
            return;
        }

        NewCourseName = string.Empty;
        NewCourseMaxScore = "20";
        SelectedNewCourseBranch = null;
        IsNewCoursePanelVisible = true;
    }

    [RelayCommand]
    private void CancelNewCoursePanel()
    {
        IsNewCoursePanelVisible = false;
        NewCourseName = string.Empty;
        NewCourseMaxScore = "20";
        SelectedNewCourseBranch = null;
    }

    [RelayCommand(CanExecute = nameof(CanCreateCourse))]
    private async Task ConfirmCreateCourseAsync()
    {
        if (SelectedPedagogicalClass is null || string.IsNullOrWhiteSpace(NewCourseName))
        {
            return;
        }

        if (!int.TryParse(NewCourseMaxScore, out var maxScore) || maxScore <= 0 || maxScore > 1000)
        {
            SetStatus("Le Max/P doit être un nombre entre 1 et 1000.", FeeStatusMessageKind.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            ClearStatus();

            var created = await _courseConfigurationApi.CreateCatalogCourseAsync(
                new CreateCatalogCourseRequest(
                    SelectedPedagogicalClass.Id,
                    NewCourseName.Trim(),
                    SelectedNewCourseBranch?.Id,
                    maxScore));

            IsNewCoursePanelVisible = false;
            NewCourseName = string.Empty;
            SelectedNewCourseBranch = null;

            await LoadAvailableCoursesAsync();
            SetStatus($"Cours « {created.Name} » créé.", FeeStatusMessageKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCreateCourse() =>
        SelectedPedagogicalClass is not null
        && !string.IsNullOrWhiteSpace(NewCourseName);

    partial void OnNewCourseNameChanged(string value) =>
        ConfirmCreateCourseCommand.NotifyCanExecuteChanged();

    partial void OnNewCourseMaxScoreChanged(string value) =>
        ConfirmCreateCourseCommand.NotifyCanExecuteChanged();

    private async Task OnFiltersChangedAsync()
    {
        var version = Interlocked.Increment(ref _filtersChangeVersion);

        OnPropertyChanged(nameof(CanEditConfiguration));
        AddSelectedCoursesCommand.NotifyCanExecuteChanged();
        RemoveSelectedCoursesCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RefreshConfigurationCommand.NotifyCanExecuteChanged();
        ConfirmCreateCourseCommand.NotifyCanExecuteChanged();

        try
        {
            await LoadAvailableCoursesAsync(version);
            if (version != _filtersChangeVersion)
            {
                return;
            }

            await LoadConfigurationIfReadyAsync();
        }
        catch (HttpRequestException ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
    }

    private async Task ReloadOrganizedSectionsAsync()
    {
        var allSections = (await _academicApiService.GetSectionsAsync()).ToList();
        var enabledClasses = await _schoolApiService.GetPedagogicalClassesAsync(enabledOnly: true);

        var organizedSectionIds = enabledClasses
            .Where(c => c.IsEnabled)
            .Select(c => ResolveSectionIdForProgram(c.Program, allSections))
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        var previousSectionId = SelectedSection?.Id;
        Sections.Clear();
        foreach (var section in allSections
                     .Where(s => organizedSectionIds.Contains(s.Id))
                     .OrderBy(s => s.Name))
        {
            Sections.Add(section);
        }

        SelectedSection = previousSectionId.HasValue
            ? Sections.FirstOrDefault(s => s.Id == previousSectionId.Value)
            : Sections.FirstOrDefault();
    }

    private async Task ReloadPedagogicalClassesAsync()
    {
        var classes = await _schoolApiService.GetPedagogicalClassesAsync(enabledOnly: true);
        _allPedagogicalClasses.Clear();
        _pedagogicalClassMap.Clear();
        var sectionList = Sections.ToList();
        foreach (var pedagogicalClass in classes.Where(c => c.IsEnabled).OrderBy(c => c.Program).ThenBy(c => c.LevelOrder))
        {
            _pedagogicalClassMap[pedagogicalClass.Id] = pedagogicalClass;
            var sectionId = ResolveSectionIdForProgram(pedagogicalClass.Program, sectionList);
            if (sectionId == Guid.Empty)
                continue;

            _allPedagogicalClasses.Add(new PedagogicalClassFilterItem(
                pedagogicalClass.Id,
                pedagogicalClass.DisplayName,
                sectionId,
                pedagogicalClass.StudyOption,
                pedagogicalClass.Program));
        }
    }

    private void RefreshPedagogicalClassOptions()
    {
        PedagogicalClasses.Clear();
        IEnumerable<PedagogicalClassFilterItem> query = _allPedagogicalClasses;
        if (SelectedSection is not null)
        {
            query = query.Where(c => c.SectionId == SelectedSection.Id);
        }

        foreach (var item in query.OrderBy(c => c.DisplayName))
        {
            if (_pedagogicalClassMap.TryGetValue(item.Id, out var pedagogicalClass))
            {
                PedagogicalClasses.Add(pedagogicalClass);
            }
        }
    }

    private void EnsureSelectedPedagogicalClassIsValid()
    {
        if (SelectedPedagogicalClass is not null
            && PedagogicalClasses.Any(c => c.Id == SelectedPedagogicalClass.Id))
        {
            return;
        }

        SelectedPedagogicalClass = PedagogicalClasses.FirstOrDefault();
    }

    private static Guid ResolveSectionIdForProgram(SchoolProgram program, IEnumerable<SectionDto> sections)
    {
        var code = PedagogicalSectionMapping.GetSectionCode(program);
        return sections.FirstOrDefault(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty;
    }

    private async Task RefreshClassRoomsAsync()
    {
        _isRefreshingClassRooms = true;
        try
        {
            ClassRooms.Clear();
            SelectedClassRoom = null;

            var yearId = AcademicYearRefreshBridge.SelectedYearId;
            if (yearId is null)
                return;

            var rooms = await _academicApiService.GetClassRoomsAsync(yearId.Value);
            foreach (var room in rooms
                         .Where(r => r.IsActive
                             && (SelectedPedagogicalClass is null || r.PedagogicalClassId == SelectedPedagogicalClass.Id))
                         .OrderBy(r => r.Name))
            {
                ClassRooms.Add(room);
            }

            SelectedClassRoom = ClassRooms.FirstOrDefault();
        }
        finally
        {
            _isRefreshingClassRooms = false;
        }
    }

    private async Task LoadAvailableCoursesAsync(int? expectedVersion = null)
    {
        if (SelectedPedagogicalClass is null)
        {
            AvailableBranchGroups.Clear();
            RefreshAvailableVisibility();
            return;
        }

        var loadVersion = Interlocked.Increment(ref _availableCoursesLoadVersion);
        var pedagogicalClassId = SelectedPedagogicalClass.Id;
        var groups = await _courseConfigurationApi.GetAvailableCoursesAsync(pedagogicalClassId);

        if (loadVersion != _availableCoursesLoadVersion)
        {
            return;
        }

        if (expectedVersion.HasValue && expectedVersion.Value != _filtersChangeVersion)
        {
            return;
        }

        AvailableBranchGroups.Clear();
        foreach (var branchGroup in BuildAvailableBranchGroups(groups))
        {
            AvailableBranchGroups.Add(branchGroup);
        }

        RefreshAvailableVisibility();
    }

    private static IEnumerable<AvailableBranchGroupViewModel> BuildAvailableBranchGroups(
        IReadOnlyList<AvailableCourseBranchGroupDto> groups)
    {
        var mergedByBranch = new Dictionary<string, List<AvailableCourseDto>>(StringComparer.OrdinalIgnoreCase);
        var seenCourseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            if (!mergedByBranch.TryGetValue(group.BranchName, out var branchCourses))
            {
                branchCourses = [];
                mergedByBranch[group.BranchName] = branchCourses;
            }

            foreach (var course in group.Courses)
            {
                var courseKey = GetCourseDedupKey(course);
                if (!seenCourseKeys.Add(courseKey))
                {
                    continue;
                }

                branchCourses.Add(course);
            }
        }

        foreach (var (branchName, branchCourses) in mergedByBranch.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (branchCourses.Count == 0)
            {
                continue;
            }

            yield return new AvailableBranchGroupViewModel(
                new AvailableCourseBranchGroupDto(branchCourses[0].BranchId, branchName, branchCourses));
        }
    }

    private static string GetCourseDedupKey(AvailableCourseDto course) =>
        !string.IsNullOrWhiteSpace(course.Code)
            ? course.Code.Trim()
            : course.CourseId.ToString();

    private async Task LoadConfigurationIfReadyAsync()
    {
        if (!CanEditConfiguration
            || AcademicYearRefreshBridge.SelectedYearId is not Guid yearId
            || SelectedPedagogicalClass is null
            || SelectedClassRoom is null)
        {
            AssignedCourses.Clear();
            AssignedBranchGroups.Clear();
            IsConfigured = false;
            return;
        }

        try
        {
            IsBusy = true;
            ClearStatus();

            var configuration = await _courseConfigurationApi.GetConfigurationAsync(
                yearId,
                SelectedPedagogicalClass.Id,
                SelectedClassRoom.Id);

            ApplyConfiguration(configuration);
            CaptureSnapshot();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, FeeStatusMessageKind.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyConfiguration(CourseConfigurationDto configuration)
    {
        IsConfigured = configuration.IsConfigured;
        IsPrimaryLevel = configuration.IsPrimaryLevel;
        UseSameTeacherForAll = false;
        SharedTeacher = TeacherOptionViewModel.Unassigned;

        AssignedCourses.Clear();
        var seenCourseIds = new HashSet<Guid>();
        foreach (var item in configuration.Items)
        {
            if (!seenCourseIds.Add(item.CourseId))
            {
                continue;
            }

            AssignedCourses.Add(new AssignedCourseItemViewModel(
                item.AssignmentId,
                item.CourseId,
                item.CourseCode,
                item.CourseName,
                item.BranchId,
                item.BranchName,
                item.TeacherId,
                Teachers,
                item.IsActive,
                item.MaxPerPeriod,
                item.WeeklyHours,
                OnAssignedCourseChanged));
        }

        AssignedCourses.Sort();
        RebuildAssignedBranchGroups();
        RefreshAvailableVisibility();
    }

    private void RebuildAssignedBranchGroups()
    {
        AssignedBranchGroups.Clear();

        var seenCourseIds = new HashSet<Guid>();
        var uniqueCourses = new List<AssignedCourseItemViewModel>();
        foreach (var course in AssignedCourses)
        {
            if (seenCourseIds.Add(course.CourseId))
            {
                uniqueCourses.Add(course);
            }
        }

        foreach (var group in uniqueCourses
                     .GroupBy(c => NormalizeBranchName(c.BranchName), StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var branchGroup = new AssignedBranchGroupViewModel
            {
                BranchName = group.Key
            };

            var seenInBranch = new HashSet<Guid>();
            foreach (var course in group.OrderBy(c => c.CourseName, StringComparer.OrdinalIgnoreCase))
            {
                if (seenInBranch.Add(course.CourseId))
                {
                    branchGroup.Courses.Add(course);
                }
            }

            if (branchGroup.Courses.Count > 0)
            {
                AssignedBranchGroups.Add(branchGroup);
            }
        }
    }

    private static string NormalizeBranchName(string? branchName) =>
        string.IsNullOrWhiteSpace(branchName) || branchName == "—" ? "Sans branche" : branchName;

    private void CaptureSnapshot()
    {
        _configurationSnapshot = JsonSerializer.Serialize(new ConfigurationSnapshot(
            IsConfigured,
            IsPrimaryLevel,
            UseSameTeacherForAll,
            SharedTeacher?.Id,
            AssignedCourses.Select(c => new SnapshotItem(
                c.AssignmentId,
                c.CourseId,
                c.CourseCode,
                c.CourseName,
                c.BranchId,
                c.BranchName,
                c.TeacherId,
                c.IsActive,
                c.Maximum,
                c.WeeklyHours)).ToList()));
    }

    private void RestoreSnapshot()
    {
        if (string.IsNullOrWhiteSpace(_configurationSnapshot))
        {
            return;
        }

        var snapshot = JsonSerializer.Deserialize<ConfigurationSnapshot>(_configurationSnapshot);
        if (snapshot is null)
        {
            return;
        }

        IsConfigured = snapshot.IsConfigured;
        IsPrimaryLevel = snapshot.IsPrimaryLevel;
        UseSameTeacherForAll = snapshot.UseSameTeacherForAll;
        SharedTeacher = Teachers.FirstOrDefault(t => t.Id == snapshot.SharedTeacherId)
            ?? TeacherOptionViewModel.Unassigned;

        AssignedCourses.Clear();
        foreach (var item in snapshot.Items)
        {
            AssignedCourses.Add(new AssignedCourseItemViewModel(
                item.AssignmentId,
                item.CourseId,
                item.CourseCode,
                item.CourseName,
                item.BranchId,
                item.BranchName,
                item.TeacherId,
                Teachers,
                item.IsActive,
                item.Maximum,
                item.WeeklyHours,
                OnAssignedCourseChanged));
        }

        AssignedCourses.Sort();
        RebuildAssignedBranchGroups();
        RefreshAvailableVisibility();
    }

    private void ClearAvailableSelections()
    {
        foreach (var group in AvailableBranchGroups)
        {
            foreach (var course in group.Courses)
            {
                course.IsSelected = false;
            }
        }
    }

    private void RefreshAvailableVisibility()
    {
        var search = AvailableSearchText.Trim();
        foreach (var group in AvailableBranchGroups)
        {
            group.ApplySearch(search, AssignedCourses.Select(c => c.CourseId).ToHashSet());
        }
    }

    private void RefreshAssignedCourseTeachers()
    {
        foreach (var course in AssignedCourses)
        {
            var teacherId = course.TeacherId;
            course.Teacher = Teachers.FirstOrDefault(t => t.Id == teacherId) ?? TeacherOptionViewModel.Unassigned;
        }

        if (SharedTeacher is not null)
        {
            SharedTeacher = Teachers.FirstOrDefault(t => t.Id == SharedTeacher.Id) ?? TeacherOptionViewModel.Unassigned;
        }
    }

    private void ApplySharedTeacher()
    {
        var teacher = SharedTeacher ?? TeacherOptionViewModel.Unassigned;
        foreach (var course in AssignedCourses)
        {
            course.Teacher = teacher;
        }
    }

    private void OnAssignedCourseChanged()
    {
        if (UseSameTeacherForAll)
        {
            UseSameTeacherForAll = false;
        }
    }

    private void SetStatus(string message, FeeStatusMessageKind kind)
    {
        StatusMessage = message;
        StatusMessageKind = kind;
    }

    private void ClearStatus()
    {
        StatusMessage = null;
        StatusMessageKind = FeeStatusMessageKind.None;
    }

    private sealed record ConfigurationSnapshot(
        bool IsConfigured,
        bool IsPrimaryLevel,
        bool UseSameTeacherForAll,
        Guid? SharedTeacherId,
        IReadOnlyList<SnapshotItem> Items);

    private sealed record SnapshotItem(
        Guid? AssignmentId,
        Guid CourseId,
        string CourseCode,
        string CourseName,
        Guid? BranchId,
        string? BranchName,
        Guid? TeacherId,
        bool IsActive,
        int Maximum,
        int WeeklyHours);
}

public partial class AvailableBranchGroupViewModel : ObservableObject
{
    public AvailableBranchGroupViewModel(AvailableCourseBranchGroupDto group)
    {
        BranchName = group.BranchName;
        var seenCourseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var course in group.Courses.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            var courseKey = !string.IsNullOrWhiteSpace(course.Code)
                ? course.Code.Trim()
                : course.CourseId.ToString();
            if (seenCourseKeys.Add(courseKey))
            {
                Courses.Add(new AvailableCourseItemViewModel(course));
            }
        }
    }

    public string BranchName { get; }
    public int CourseCount => Courses.Count(c => c.IsVisible);
    public ObservableCollection<AvailableCourseItemViewModel> Courses { get; } = [];
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isVisible = true;

    public PackIconKind ExpandIconKind => IsExpanded ? PackIconKind.ChevronDown : PackIconKind.ChevronRight;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandIconKind));

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;

    public void ApplySearch(string search, IReadOnlySet<Guid> assignedCourseIds)
    {
        var visibleCount = 0;
        var seenCourseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var course in Courses)
        {
            var courseKey = !string.IsNullOrWhiteSpace(course.Code)
                ? course.Code.Trim()
                : course.CourseId.ToString();
            if (!seenCourseKeys.Add(courseKey))
            {
                course.IsVisible = false;
                continue;
            }

            var matchesSearch = string.IsNullOrWhiteSpace(search)
                || course.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || course.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
                || BranchName.Contains(search, StringComparison.OrdinalIgnoreCase);

            course.IsAlreadyAssigned = assignedCourseIds.Contains(course.CourseId);
            course.IsVisible = matchesSearch;
            if (course.IsVisible)
            {
                visibleCount++;
            }

            if (course.IsAlreadyAssigned)
            {
                course.IsSelected = false;
            }
        }

        IsVisible = visibleCount > 0;
        OnPropertyChanged(nameof(CourseCount));
        if (!string.IsNullOrWhiteSpace(search) && visibleCount > 0)
        {
            IsExpanded = true;
        }
    }
}

public partial class AvailableCourseItemViewModel : ObservableObject
{
    public AvailableCourseItemViewModel(AvailableCourseDto course)
    {
        CourseId = course.CourseId;
        Code = course.Code;
        Name = course.Name;
        BranchId = course.BranchId;
        BranchName = course.BranchName;
        MaxPerPeriod = course.MaxPerPeriod;
    }

    public Guid CourseId { get; }
    public string Code { get; }
    public string Name { get; }
    public Guid? BranchId { get; }
    public string? BranchName { get; }
    public int MaxPerPeriod { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(Code) ? Name : $"{Name} ({Code})";

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    private bool _isAlreadyAssigned;

    public bool CanSelect => !IsAlreadyAssigned;
}

public partial class AssignedBranchGroupViewModel : ObservableObject
{
    public required string BranchName { get; init; }

    public ObservableCollection<AssignedCourseItemViewModel> Courses { get; } = [];

    public int CourseCount => Courses.Count;

    [ObservableProperty] private bool _isExpanded = true;

    public PackIconKind ExpandIconKind => IsExpanded ? PackIconKind.ChevronDown : PackIconKind.ChevronRight;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandIconKind));

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}

public partial class AssignedCourseItemViewModel : ObservableObject, IComparable<AssignedCourseItemViewModel>
{
    private readonly Action _changed;

    public AssignedCourseItemViewModel(
        Guid? assignmentId,
        Guid courseId,
        string courseCode,
        string courseName,
        Guid? branchId,
        string? branchName,
        Guid? teacherId,
        ObservableCollection<TeacherOptionViewModel> teachers,
        bool isActive,
        int maximum,
        int weeklyHours,
        Action changed)
    {
        AssignmentId = assignmentId;
        CourseId = courseId;
        CourseCode = courseCode;
        CourseName = courseName;
        BranchId = branchId;
        BranchName = branchName ?? "—";
        Teachers = teachers;
        _changed = changed;
        Teacher = teachers.FirstOrDefault(t => t.Id == teacherId) ?? TeacherOptionViewModel.Unassigned;
        _isActive = isActive;
        _maximum = maximum;
        _weeklyHours = weeklyHours;
    }

    public Guid? AssignmentId { get; }
    public Guid CourseId { get; }
    public string CourseCode { get; }
    public string CourseName { get; }
    public Guid? BranchId { get; }
    public string BranchName { get; }
    public ObservableCollection<TeacherOptionViewModel> Teachers { get; }

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TeacherId))]
    private TeacherOptionViewModel? _teacher;

    partial void OnTeacherChanged(TeacherOptionViewModel? value) => _changed();

    public Guid? TeacherId => Teacher?.Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private bool _isActive;

    partial void OnIsActiveChanged(bool value) => _changed();

    [ObservableProperty]
    private int _maximum;

    partial void OnMaximumChanged(int value) => _changed();

    [ObservableProperty]
    private int _weeklyHours;

    partial void OnWeeklyHoursChanged(int value) => _changed();

    public string StatusLabel => IsActive ? "Actif" : "Inactif";

    public int CompareTo(AssignedCourseItemViewModel? other) =>
        other is null
            ? 1
            : string.Compare(CourseName, other.CourseName, StringComparison.OrdinalIgnoreCase);
}

public sealed class TeacherOptionViewModel
{
    private TeacherOptionViewModel(Guid? id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public Guid? Id { get; }
    public string DisplayName { get; }

    public static TeacherOptionViewModel Unassigned => new(null, "(Non assigné)");

    public static TeacherOptionViewModel From(TeacherAdminDto teacher) =>
        new(teacher.Id, $"{teacher.LastName} {teacher.FirstName}".Trim());

    public override string ToString() => DisplayName;
}

public static class AssignedCourseCollectionExtensions
{
    public static void Sort(this ObservableCollection<AssignedCourseItemViewModel> courses)
    {
        var ordered = courses.OrderBy(c => c, Comparer<AssignedCourseItemViewModel>.Default).ToList();
        courses.Clear();
        foreach (var course in ordered)
        {
            courses.Add(course);
        }
    }
}
