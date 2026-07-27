using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Shared.Constants;
using System.Collections.ObjectModel;

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
            new ModuleNavItem("Élèves", "AccountGroup", typeof(StudentsViewModel)),
            new ModuleNavItem("Cartes élèves", "CardAccountDetails", typeof(StudentCardsViewModel)),
            new ModuleNavItem("Académique", "School", typeof(AcademicViewModel)),
            new ModuleNavItem("Notes", "ClipboardText", typeof(GradesViewModel)),
            new ModuleNavItem("Financier", "Cash", typeof(FinanceHubViewModel)),
            new ModuleNavItem("Documents", "FileDocument", typeof(DocumentsViewModel)),
            new ModuleNavItem("Statistiques", "ChartBar", typeof(StatisticsViewModel))
        ];
        _navigationService.NavigateTo<DashboardViewModel>();
        SyncSelectedModuleFromNavigation();
        AcademicYearRefreshBridge.CurrentYearChanged += OnAcademicYearBridgeChanged;
        _ = LoadCurrentAcademicYearAsync();
    }

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

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IAuthSessionService _authSession;
    private readonly ISchoolApiService _schoolApiService;

    public DashboardViewModel(IApiClient apiClient, IAuthSessionService authSession, ISchoolApiService schoolApiService)
    {
        _apiClient = apiClient;
        _authSession = authSession;
        _schoolApiService = schoolApiService;
        AcademicYearRefreshBridge.CurrentYearChanged += OnAcademicYearLabelRefreshRequested;
        _ = RefreshApiStatusAsync();
        _ = LoadAcademicYearLabelAsync();
    }

    private void OnAcademicYearLabelRefreshRequested()
    {
        var year = AcademicYearRefreshBridge.SelectedYear;
        CurrentAcademicYearLabel = year is null
            ? "Rentrée scolaire non configurée"
            : $"Rentrée scolaire {year.Label}";
    }

    public string WelcomeMessage => $"Bienvenue, {_authSession.CurrentUser?.FullName ?? "utilisateur"}";

    [ObservableProperty]
    private string _currentAcademicYearLabel = "Rentrée scolaire —";

    private Task LoadAcademicYearLabelAsync()
    {
        OnAcademicYearLabelRefreshRequested();
        return Task.CompletedTask;
    }

    [ObservableProperty]
    private string _apiStatus = "Vérification...";

    [RelayCommand]
    private async Task RefreshApiStatusAsync()
    {
        ApiStatus = await _apiClient.CheckHealthAsync() ? "API connectée" : "API hors ligne";
    }
}
