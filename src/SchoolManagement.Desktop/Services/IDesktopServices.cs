namespace SchoolManagement.Desktop.Services;

public interface INavigationService
{
    object? CurrentViewModel { get; }

    bool CanNavigateBack { get; }

    event Action? CurrentViewModelChanged;

    void NavigateTo<TViewModel>(bool recordBack = false) where TViewModel : class;

    void NavigateTo(Type viewModelType, bool recordBack = false);

    void NavigateTo(object viewModel, bool recordBack = false);

    bool NavigateBack();

    /// <summary>Efface la vue courante (ex. déconnexion) sans fermer l'application.</summary>
    void Clear();
}

public interface IThemeService
{
    bool IsDarkTheme { get; }

    void SetTheme(bool isDark);
}

public interface IApiClient
{
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
