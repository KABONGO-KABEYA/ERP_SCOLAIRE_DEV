using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class StudentsViewModel : ViewModelBase
{
    private readonly IStudentApiService _studentApiService;
    private readonly ISchoolApiService _schoolApiService;
    private readonly IEnrollmentWizardApiService _wizardApiService;
    private readonly INavigationService _navigationService;
    private readonly IStudentDossierPathResolver _dossierPathResolver;
    private readonly IStudentListPrintService _studentListPrintService;
    private readonly List<EnrollmentClassOptionDto> _structureClassRooms = [];
    private readonly List<ClassRoomLookupDto> _lookupClassRooms = [];
    private CancellationTokenSource? _searchCts;

    public StudentsViewModel(
        IStudentApiService studentApiService,
        ISchoolApiService schoolApiService,
        IEnrollmentWizardApiService wizardApiService,
        INavigationService navigationService,
        IStudentDossierPathResolver dossierPathResolver,
        IStudentListPrintService studentListPrintService)
    {
        _studentApiService = studentApiService;
        _schoolApiService = schoolApiService;
        _wizardApiService = wizardApiService;
        _navigationService = navigationService;
        _dossierPathResolver = dossierPathResolver;
        _studentListPrintService = studentListPrintService;
        StatusMessage = "Utilisez les filtres puis cliquez sur « Afficher » pour lister les élèves.";
        _ = LoadFilterOptionsAsync();
    }

    public ObservableCollection<StudentDto> Students { get; } = [];

    public ObservableCollection<AcademicYearDto> AcademicYears { get; } = [];

    public ObservableCollection<SectionDto> Sections { get; } = [];

    public ObservableCollection<PedagogicalClassFilterItem> PedagogicalClasses { get; } = [];

    public ObservableCollection<ClassRoomFilterItem> ClassRooms { get; } = [];

    public ObservableCollection<string> StudyOptions { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private StudentDto? _selectedStudent;

    [ObservableProperty]
    private AcademicYearDto? _selectedAcademicYear;

    [ObservableProperty]
    private SectionDto? _selectedSection;

    [ObservableProperty]
    private PedagogicalClassFilterItem? _selectedPedagogicalClass;

    [ObservableProperty]
    private ClassRoomFilterItem? _selectedClassRoom;

    [ObservableProperty]
    private string? _selectedStudyOption;

    [ObservableProperty]
    private bool _isFiltersExpanded = true;

    [ObservableProperty]
    private int _studentsFoundCount;

    [ObservableProperty]
    private bool _includeInscrits = true;

    [ObservableProperty]
    private bool _includeExcluded;

    [ObservableProperty]
    private bool _includeAbandoned;

    public string FiltersHeaderText => $"Filtres de recherche ({StudentsFoundCount})";

    public string FiltersToggleLabel => IsFiltersExpanded ? "Masquer les filtres" : "Afficher les filtres";

    partial void OnIsFiltersExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(FiltersToggleLabel));
    }

    partial void OnStudentsFoundCountChanged(int value) => OnPropertyChanged(nameof(FiltersHeaderText));

    partial void OnSearchTextChanged(string value) => QueueSearch();

    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    private readonly List<PedagogicalClassFilterItem> _allPedagogicalClasses = [];
    private readonly HashSet<string> _allStudyOptions = new(StringComparer.OrdinalIgnoreCase);

    partial void OnSelectedSectionChanged(SectionDto? value)
    {
        SelectedPedagogicalClass = null;
        SelectedStudyOption = null;
        RefreshPedagogicalClassOptions();
        RefreshStudyOptions();
        RefreshClassRoomOptions();
    }

    partial void OnSelectedPedagogicalClassChanged(PedagogicalClassFilterItem? value)
    {
        RefreshStudyOptions();
        RefreshClassRoomOptions();
    }

    partial void OnSelectedStudyOptionChanged(string? value) => RefreshClassRoomOptions();

    partial void OnSelectedAcademicYearChanged(AcademicYearDto? value) => RefreshClassRoomOptions();

    [RelayCommand]
    private async Task LoadFilterOptionsAsync()
    {
        try
        {
            AcademicYears.Clear();
            foreach (var year in await _schoolApiService.GetAcademicYearsAsync())
            {
                AcademicYears.Add(year);
            }

            SelectedAcademicYear ??= AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();

            var structure = await _wizardApiService.GetStructureOptionsAsync();
            _structureClassRooms.Clear();
            _structureClassRooms.AddRange(structure.Classes);

            var lookups = await _schoolApiService.GetLookupsAsync();
            _lookupClassRooms.Clear();
            _lookupClassRooms.AddRange(lookups.ClassRooms);

            Sections.Clear();
            foreach (var section in structure.Sections.OrderBy(s => s.Name))
            {
                Sections.Add(section);
            }

            PedagogicalClasses.Clear();
            _allPedagogicalClasses.Clear();
            var pedagogicalClasses = await _schoolApiService.GetPedagogicalClassesAsync(enabledOnly: true);
            foreach (var cls in pedagogicalClasses.Where(c => c.IsEnabled).OrderBy(c => c.DisplayName))
            {
                var sectionId = ResolveSectionIdForProgram(cls.Program, Sections);
                if (sectionId == Guid.Empty)
                {
                    continue;
                }

                _allPedagogicalClasses.Add(new PedagogicalClassFilterItem(
                    cls.Id,
                    cls.DisplayName,
                    sectionId,
                    cls.StudyOption,
                    cls.Program));
            }

            _allStudyOptions.Clear();
            foreach (var option in _allPedagogicalClasses
                         .Select(c => c.StudyOption)
                         .Where(o => !string.IsNullOrWhiteSpace(o)))
            {
                _allStudyOptions.Add(option!);
            }

            RefreshPedagogicalClassOptions();
            RefreshStudyOptions();
            RefreshClassRoomOptions();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync() => await ExecuteSearchAsync();

    private void QueueSearch()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = DebouncedSearchAsync(token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            if (!token.IsCancellationRequested)
            {
                await ExecuteSearchAsync();
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }

    private async Task ExecuteSearchAsync()
    {
        if (!IncludeInscrits && !IncludeExcluded && !IncludeAbandoned)
        {
            StatusMessage = "Sélectionnez au moins un type d'élève à afficher (inscrits, exclus ou abandonnés).";
            Students.Clear();
            StudentsFoundCount = 0;
            return;
        }

        if (SelectedAcademicYear is null
            && SelectedSection is null
            && SelectedPedagogicalClass is null
            && SelectedClassRoom is null
            && string.IsNullOrWhiteSpace(SelectedStudyOption)
            && string.IsNullOrWhiteSpace(SearchText)
            && !IncludeExcluded
            && !IncludeAbandoned)
        {
            StatusMessage = "Sélectionnez au moins un filtre, un critère de recherche ou cochez « Exclus » / « Abandonnés ».";
            Students.Clear();
            StudentsFoundCount = 0;
            return;
        }

        await SearchAsync(applyFilters: true);
    }

    [RelayCommand]
    private Task SearchAsync() => SearchAsync(applyFilters: true);

    private async Task SearchAsync(bool applyFilters)
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _studentApiService.SearchAsync(new StudentSearchRequest(
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                SelectedAcademicYear?.Id,
                SelectedSection?.Id,
                SelectedPedagogicalClass?.Id,
                SelectedClassRoom?.Id,
                SelectedStudyOption,
                ApplyFilters: applyFilters,
                IncludeAll: false,
                IncludeInscrits: IncludeInscrits,
                IncludeExcluded: IncludeExcluded,
                IncludeAbandoned: IncludeAbandoned,
                Page: 1,
                PageSize: 50));

            Students.Clear();
            foreach (var student in result.Items)
            {
                Students.Add(student);
            }

            StudentsFoundCount = result.TotalCount;
            StatusMessage = result.TotalCount == 0
                ? "Aucun élève trouvé pour les critères sélectionnés."
                : $"{result.TotalCount} élève(s) trouvé(s).";
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
    private void ClearFilters()
    {
        _searchCts?.Cancel();
        _searchCts = null;
        SearchText = string.Empty;
        SelectedSection = null;
        SelectedPedagogicalClass = null;
        SelectedClassRoom = null;
        SelectedStudyOption = null;
        SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();
        IncludeInscrits = true;
        IncludeExcluded = false;
        IncludeAbandoned = false;
        RefreshPedagogicalClassOptions();
        RefreshStudyOptions();
        RefreshClassRoomOptions();
        Students.Clear();
        StudentsFoundCount = 0;
        StatusMessage = "Utilisez les filtres puis cliquez sur « Afficher » pour lister les élèves.";
    }

    [RelayCommand]
    private void OpenEnrollmentWizard()
    {
        EnrollmentWizardNavigationBridge.Request(EnrollmentWizardEntryMode.NouvelleInscription);
        _navigationService.NavigateTo<EnrollmentWizardViewModel>();
    }

    [RelayCommand]
    private void OpenReinscriptionWizard()
    {
        EnrollmentWizardNavigationBridge.Request(EnrollmentWizardEntryMode.Reinscription);
        _navigationService.NavigateTo<EnrollmentWizardViewModel>();
    }

    [RelayCommand]
    private async Task ArchiveSelectedAsync()
    {
        if (SelectedStudent is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _studentApiService.ArchiveAsync(SelectedStudent.Id);
            StatusMessage = $"Élève {SelectedStudent.LastName} archivé.";
            await SearchAsync(applyFilters: true);
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
    private async Task ShowStudentProfileAsync(StudentDto? student)
    {
        if (student is null)
        {
            return;
        }

        try
        {
            var profile = await _studentApiService.GetProfileAsync(student.Id);
            var window = new Views.StudentProfileWindow(profile);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task EditStudentAsync(StudentDto? student)
    {
        if (student is null || !student.IsEnrolledCurrentYear)
        {
            StatusMessage = "Modification disponible uniquement pour les élèves inscrits sur l'année courante.";
            return;
        }

        try
        {
            var viewModel = App.Services!.GetRequiredService<StudentDossierEditViewModel>();
            var window = new Views.StudentDossierEditWindow(viewModel, student.Id)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (window.ShowDialog() == true)
            {
                StatusMessage = viewModel.StatusMessage ?? $"Dossier de {student.FullName} mis à jour.";
                await SearchAsync(applyFilters: true);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ShowStudentDossierFilesAsync(StudentDto? student)
    {
        if (student is null)
        {
            return;
        }

        try
        {
            var files = await _studentApiService.ListDossierFilesAsync(student.Id);
            if (files.Count == 0)
            {
                StatusMessage = $"Aucun fichier trouvé dans le dossier de {student.FullName}.";
                return;
            }

            var window = new Views.StudentDossierFilesWindow(student.FullName, files, _dossierPathResolver)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task WithdrawStudentAsync(StudentDto? student)
    {
        if (student is null || !student.IsEnrolledCurrentYear)
        {
            StatusMessage = "Exclusion ou abandon disponible uniquement pour les élèves inscrits sur l'année courante.";
            return;
        }

        IsBusy = true;
        try
        {
            var reasons = await _studentApiService.GetWithdrawalReasonsAsync();
            var dialog = new Views.StudentWithdrawalWindow(student.FullName, reasons)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true || dialog.Result is not { Confirmed: true } result)
            {
                return;
            }

            await _studentApiService.WithdrawFromCurrentYearAsync(
                student.Id,
                new WithdrawFromCurrentYearRequest(result.WithdrawalType, result.ReasonCode, result.CustomReason));

            var actionLabel = result.WithdrawalType == StudentWithdrawalType.Exclusion ? "exclu" : "déclaré en abandon";
            StatusMessage = $"Élève {student.LastName} {actionLabel} pour l'année scolaire courante.";
            await SearchAsync(applyFilters: true);
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
    private async Task PrintListAsync()
    {
        if (!IncludeInscrits && !IncludeExcluded && !IncludeAbandoned)
        {
            StatusMessage = "Sélectionnez au moins un type d'élève à imprimer.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _studentApiService.SearchAsync(new StudentSearchRequest(
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                SelectedAcademicYear?.Id,
                SelectedSection?.Id,
                SelectedPedagogicalClass?.Id,
                SelectedClassRoom?.Id,
                SelectedStudyOption,
                ApplyFilters: true,
                IncludeAll: false,
                IncludeInscrits: IncludeInscrits,
                IncludeExcluded: IncludeExcluded,
                IncludeAbandoned: IncludeAbandoned,
                Page: 1,
                PageSize: 5000));

            if (result.TotalCount == 0)
            {
                StatusMessage = "Aucun élève à imprimer pour les critères sélectionnés.";
                return;
            }

            _studentListPrintService.Print(
                result.Items,
                "Liste des élèves",
                BuildPrintSubtitle(result.TotalCount));
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

    private string BuildPrintSubtitle(int totalCount)
    {
        var parts = new List<string>();
        if (IncludeInscrits)
        {
            parts.Add("inscrits");
        }

        if (IncludeExcluded)
        {
            parts.Add("exclus");
        }

        if (IncludeAbandoned)
        {
            parts.Add("abandonnés");
        }

        var scope = string.Join(", ", parts);
        var year = SelectedAcademicYear?.Label ?? "année courante";
        var filters = new List<string> { $"Élèves {scope}", $"Année : {year}" };

        if (SelectedSection is not null)
        {
            filters.Add($"Section : {SelectedSection.Name}");
        }

        if (SelectedPedagogicalClass is not null)
        {
            filters.Add($"Classe : {SelectedPedagogicalClass.DisplayName}");
        }

        if (SelectedClassRoom is not null)
        {
            filters.Add($"Locale : {SelectedClassRoom.DisplayName}");
        }

        if (!string.IsNullOrWhiteSpace(SelectedStudyOption))
        {
            filters.Add($"Option : {SelectedStudyOption}");
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filters.Add($"Recherche : {SearchText.Trim()}");
        }

        return $"{string.Join(" — ", filters)} — {totalCount} élève(s)";
    }

    [RelayCommand]
    private async Task ExcludeStudentAsync(StudentDto? student) => await WithdrawStudentAsync(student);

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
            PedagogicalClasses.Add(item);
        }
    }

    private void RefreshStudyOptions()
    {
        StudyOptions.Clear();
        IEnumerable<PedagogicalClassFilterItem> classes = _allPedagogicalClasses;
        if (SelectedSection is not null)
        {
            classes = classes.Where(c => c.SectionId == SelectedSection.Id);
        }

        if (SelectedPedagogicalClass is not null)
        {
            classes = classes.Where(c => c.Id == SelectedPedagogicalClass.Id);
        }

        foreach (var option in classes
                     .Select(c => c.StudyOption)
                     .Where(o => !string.IsNullOrWhiteSpace(o))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(o => o))
        {
            StudyOptions.Add(option!);
        }
    }

    private void RefreshClassRoomOptions()
    {
        ClassRooms.Clear();
        SelectedClassRoom = null;

        var currentYearId = AcademicYears.FirstOrDefault(y => y.IsCurrent)?.Id;
        var useStructure = SelectedAcademicYear is null || SelectedAcademicYear.Id == currentYearId;

        if (useStructure)
        {
            IEnumerable<EnrollmentClassOptionDto> classes = _structureClassRooms;
            if (SelectedSection is not null)
            {
                classes = classes.Where(c => c.SectionId == SelectedSection.Id);
            }

            if (SelectedPedagogicalClass is not null)
            {
                classes = classes.Where(c => c.PedagogicalClassId == SelectedPedagogicalClass.Id);
            }

            if (!string.IsNullOrWhiteSpace(SelectedStudyOption))
            {
                classes = classes.Where(c =>
                    string.Equals(c.StudyOption, SelectedStudyOption, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var room in classes.OrderBy(c => c.FullDisplayName))
            {
                ClassRooms.Add(new ClassRoomFilterItem(room.ClassRoomId, room.LocalName ?? room.FullDisplayName));
            }

            return;
        }

        var rooms = _lookupClassRooms.AsEnumerable();
        if (SelectedAcademicYear is not null)
        {
            rooms = rooms.Where(r => r.AcademicYearId == SelectedAcademicYear.Id);
        }

        foreach (var room in rooms.OrderBy(r => r.Name))
        {
            ClassRooms.Add(new ClassRoomFilterItem(room.Id, room.Name));
        }
    }

    private static Guid ResolveSectionIdForProgram(SchoolProgram program, IEnumerable<SectionDto> sections)
    {
        var code = program switch
        {
            SchoolProgram.Maternelle => "MAT",
            SchoolProgram.Primaire => "PRI",
            SchoolProgram.CTEB => "CTEB",
            SchoolProgram.Humanites => "HUM",
            SchoolProgram.HumanitesProfessionnelles => "HPRO",
            SchoolProgram.FilieresSpecialisees => "FS",
            _ => "PRI"
        };

        return sections.FirstOrDefault(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty;
    }
}

public sealed record PedagogicalClassFilterItem(
    Guid Id,
    string DisplayName,
    Guid SectionId,
    string? StudyOption,
    SchoolProgram Program);

public sealed record ClassRoomFilterItem(Guid Id, string DisplayName);
