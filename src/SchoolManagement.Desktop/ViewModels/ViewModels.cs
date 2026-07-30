using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Shared.Constants;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace SchoolManagement.Desktop.ViewModels;

public partial class ViewModelBase : ObservableObject
{
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IAuthSessionService _authSession;
    private readonly AuthApiService _authApiService;

    public MainWindowViewModel(
        IThemeService themeService,
        ShellViewModel shellViewModel,
        IAuthSessionService authSession,
        AuthApiService authApiService)
    {
        _themeService = themeService;
        _authSession = authSession;
        _authApiService = authApiService;
        Shell = shellViewModel;
    }

    public string ApplicationTitle => AppConstants.ApplicationName;

    public string UserDisplayName => _authSession.CurrentUser?.FullName ?? "Utilisateur";

    public ShellViewModel Shell { get; }

    [RelayCommand]
    private void ToggleTheme() => _themeService.SetTheme(!_themeService.IsDarkTheme);

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authApiService.LogoutAsync();
        System.Windows.Application.Current.Shutdown();
    }
}

public partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly ISchoolApiService _schoolApiService;
    private bool _syncingSelection;
    private bool _suppressYearChange;

    public ShellViewModel(INavigationService navigationService, ISchoolApiService schoolApiService)
    {
        _navigationService = navigationService;
        _schoolApiService = schoolApiService;
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
        Modules =
        [
            new ModuleNavItem("Tableau de bord", "ViewDashboard", typeof(DashboardViewModel)),
            new ModuleNavItem("Paramètres", "Cog", typeof(SettingsViewModel)),
            new ModuleNavItem("Personnel", "AccountTie", typeof(PersonnelHubViewModel)),
            new ModuleNavItem("Élèves", "AccountGroup", typeof(StudentsViewModel)),
            new ModuleNavItem("Cartes élèves", "CardAccountDetails", typeof(StudentCardsViewModel)),
            new ModuleNavItem("Académique", "School", typeof(AcademicViewModel)),
            new ModuleNavItem("Calendrier pédagogique", "CalendarClock", typeof(PedagogicalPeriodsViewModel)),
            new ModuleNavItem("Cotation", "ClipboardEdit", typeof(GradesViewModel)),
            new ModuleNavItem("Financier", "Cash", typeof(FinanceHubViewModel)),
            new ModuleNavItem("Documents", "FileDocument", typeof(DocumentsViewModel)),
            new ModuleNavItem("Statistiques", "ChartBar", typeof(StatisticsViewModel))
        ];
        _navigationService.NavigateTo<DashboardViewModel>();
        SyncSelectedModuleFromNavigation();
        AcademicYearRefreshBridge.CurrentYearChanged += OnAcademicYearBridgeChanged;
        _ = LoadCurrentAcademicYearAsync();
        _ = LoadSchoolNameAsync();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            CurrentDateLabel = now.ToString("ddd d MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
            CurrentTimeLabel = now.ToString("HH:mm");
        };
        _clockTimer.Start();
        CurrentDateLabel = DateTime.Now.ToString("ddd d MMM yyyy", new System.Globalization.CultureInfo("fr-FR"));
        CurrentTimeLabel = DateTime.Now.ToString("HH:mm");
    }

    private readonly DispatcherTimer _clockTimer;

    private void OnAcademicYearBridgeChanged()
    {
        _suppressYearChange = true;
        try
        {
            SelectedWorkingAcademicYear = AcademicYearRefreshBridge.SelectedYear;
            CurrentAcademicYearLabel = SelectedWorkingAcademicYear is null
                ? "Année scolaire non configurée"
                : $"Année scolaire {SelectedWorkingAcademicYear.Label}";
        }
        finally
        {
            _suppressYearChange = false;
        }
    }

    [ObservableProperty]
    private string _currentAcademicYearLabel = "Année scolaire —";

    [ObservableProperty]
    private string _schoolName = "Établissement scolaire";

    [ObservableProperty]
    private string _currentDateLabel = "";

    [ObservableProperty]
    private string _currentTimeLabel = "";

    [ObservableProperty]
    private AcademicYearDto? _selectedWorkingAcademicYear;

    public ObservableCollection<AcademicYearDto> WorkingAcademicYears => AcademicYearRefreshBridge.Years;

    private async Task LoadCurrentAcademicYearAsync()
    {
        try
        {
            var years = await _schoolApiService.GetAcademicYearsAsync();
            AcademicYearRefreshBridge.ReplaceYears(years);
            OnPropertyChanged(nameof(WorkingAcademicYears));
            // ReplaceYears déclenche CurrentYearChanged → OnAcademicYearBridgeChanged.
        }
        catch
        {
            CurrentAcademicYearLabel = "Année scolaire —";
        }
    }

    private async Task LoadSchoolNameAsync()
    {
        try
        {
            var school = await _schoolApiService.GetCurrentSchoolAsync();
            if (school is not null && !string.IsNullOrWhiteSpace(school.Name))
                SchoolName = school.Name;
        }
        catch
        {
            // affichage non bloquant
        }
    }

    public void RefreshCurrentAcademicYear() => _ = LoadCurrentAcademicYearAsync();

    partial void OnSelectedWorkingAcademicYearChanged(AcademicYearDto? value)
    {
        if (_suppressYearChange || value is null)
            return;

        _ = SwitchWorkingAcademicYearAsync(value);
    }

    private async Task SwitchWorkingAcademicYearAsync(AcademicYearDto year)
    {
        try
        {
            if (AcademicYearRefreshBridge.SelectedYear?.Id == year.Id && year.IsCurrent)
                return;

            await _schoolApiService.SetCurrentAcademicYearAsync(year.Id);
            var years = await _schoolApiService.GetAcademicYearsAsync();
            AcademicYearRefreshBridge.ReplaceYears(years, preferSelectedId: year.Id);
            OnPropertyChanged(nameof(WorkingAcademicYears));
        }
        catch
        {
            // Restaure la sélection précédente si l'API échoue.
            _suppressYearChange = true;
            try
            {
                SelectedWorkingAcademicYear = AcademicYearRefreshBridge.SelectedYear;
            }
            finally
            {
                _suppressYearChange = false;
            }
        }
    }

    public IReadOnlyList<ModuleNavItem> Modules { get; }

    [ObservableProperty]
    private ModuleNavItem? _selectedModule;

    public object? CurrentViewModel => _navigationService.CurrentViewModel;

    partial void OnSelectedModuleChanged(ModuleNavItem? value)
    {
        if (_syncingSelection || value?.ViewModelType is null)
        {
            return;
        }

        if (_navigationService.CurrentViewModel?.GetType() != value.ViewModelType)
        {
            _navigationService.NavigateTo(value.ViewModelType);
        }
    }

    private void OnCurrentViewModelChanged()
    {
        SyncSelectedModuleFromNavigation();
        OnPropertyChanged(nameof(CurrentViewModel));
    }

    private void SyncSelectedModuleFromNavigation()
    {
        var currentType = _navigationService.CurrentViewModel?.GetType();
        var match = Modules.FirstOrDefault(m => m.ViewModelType == currentType);
        if (match is null || SelectedModule == match)
        {
            return;
        }

        _syncingSelection = true;
        SelectedModule = match;
        _syncingSelection = false;
    }
}

public sealed record ModuleNavItem(string Title, string IconKind, Type? ViewModelType);

