using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Navigation;
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
    private readonly SecurityNavigationApiService _navigationApi;
    private readonly IDesktopNavigationLocalCache _navigationCache;
    private readonly IDesktopViewRegistry _viewRegistry;
    private bool _syncingSelection;
    private bool _suppressYearChange;

    public ShellViewModel(
        INavigationService navigationService,
        ISchoolApiService schoolApiService,
        SecurityNavigationApiService navigationApi,
        IDesktopNavigationLocalCache navigationCache,
        IDesktopViewRegistry viewRegistry)
    {
        _navigationService = navigationService;
        _schoolApiService = schoolApiService;
        _navigationApi = navigationApi;
        _navigationCache = navigationCache;
        _viewRegistry = viewRegistry;
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
        Modules = new ObservableCollection<ModuleNavItem>();
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

    /// <summary>
    /// Charge la navigation depuis l'API (et met en cache) ou depuis le cache local.
    /// Retourne false si aucune navigation n'est disponible.
    /// </summary>
    public async Task<bool> InitializeNavigationAsync(CancellationToken cancellationToken = default)
    {
        NavigationError = null;
        try
        {
            var tree = await _navigationApi.GetDesktopNavigationAsync(cancellationToken);
            await _navigationCache.SaveAsync(tree, cancellationToken);
            ApplyNavigationTree(tree);
            return Modules.Count > 0;
        }
        catch (Exception ex)
        {
            var cached = await _navigationCache.TryLoadAsync(cancellationToken);
            if (cached is not null)
            {
                ApplyNavigationTree(cached);
                if (Modules.Count > 0)
                {
                    NavigationError = null;
                    return true;
                }
            }

            NavigationError =
                "Impossible de charger la navigation. "
                + "L'API de navigation est indisponible et aucun menu n'a encore été mis en cache localement. "
                + $"Détail : {ex.Message}";
            Modules.Clear();
            return false;
        }
    }

    private void ApplyNavigationTree(Application.Security.DTOs.NavigationTreeDto tree)
    {
        var built = DesktopNavigationMenuBuilder.Build(
            tree,
            _viewRegistry,
            key => System.Diagnostics.Debug.WriteLine($"DesktopViewKey non résolue: {key}"));

        Modules.Clear();
        foreach (var module in built)
        {
            Modules.Add(module);
        }

        var first = Modules.FirstOrDefault();
        if (first?.ViewModelType is not null)
        {
            _navigationService.NavigateTo(first.ViewModelType);
            SyncSelectedModuleFromNavigation();
        }
    }

    [ObservableProperty]
    private string? _navigationError;

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

    public ObservableCollection<ModuleNavItem> Modules { get; }

    [ObservableProperty]
    private ModuleNavItem? _selectedModule;

    public object? CurrentViewModel => _navigationService.CurrentViewModel;

    partial void OnSelectedModuleChanged(ModuleNavItem? value)
    {
        if (_syncingSelection || value?.ViewModelType is null)
        {
            return;
        }

        var currentType = _navigationService.CurrentViewModel?.GetType();
        if (currentType == value.ViewModelType)
        {
            return;
        }

        _navigationService.NavigateTo(value.ViewModelType);
    }

    private void OnCurrentViewModelChanged()
    {
        SyncSelectedModuleFromNavigation();
        OnPropertyChanged(nameof(CurrentViewModel));
    }

    private void SyncSelectedModuleFromNavigation()
    {
        var currentType = _navigationService.CurrentViewModel?.GetType();
        var match = Modules.FirstOrDefault(m => m.ViewModelType == currentType)
            ?? Modules.FirstOrDefault(m =>
                m.Pages.Any(p =>
                    _viewRegistry.TryResolve(p.DesktopViewKey, out var target)
                    && target is DirectDesktopViewTarget direct
                    && direct.ViewModelType == currentType));
        if (match is null || SelectedModule == match)
        {
            return;
        }

        _syncingSelection = true;
        SelectedModule = match;
        _syncingSelection = false;
    }

    /// <summary>
    /// Ouvre une page catalogue mappée en ViewModel direct (ex. écrans Sécurité sous le hub Paramètres).
    /// </summary>
    public void NavigateToDirectCatalogPage(Type viewModelType, ModuleNavItem? owningModule)
    {
        _syncingSelection = true;
        try
        {
            if (owningModule is not null)
            {
                SelectedModule = owningModule;
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        _navigationService.NavigateTo(viewModelType);
    }

    public void NavigateToViewModelType(Type viewModelType)
    {
        if (_navigationService.CurrentViewModel?.GetType() == viewModelType)
        {
            return;
        }

        _navigationService.NavigateTo(viewModelType);
    }

    public bool IsDirectCatalogViewModel(Type? viewModelType)
    {
        if (viewModelType is null)
        {
            return false;
        }

        return Modules.Any(m =>
            m.Pages.Any(p =>
                _viewRegistry.TryResolve(p.DesktopViewKey, out var target)
                && target is DirectDesktopViewTarget direct
                && direct.ViewModelType == viewModelType));
    }
}
