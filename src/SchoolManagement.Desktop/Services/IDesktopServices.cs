namespace SchoolManagement.Desktop.Services;

public interface INavigationService
{
    object? CurrentViewModel { get; }

    event Action? CurrentViewModelChanged;

    void NavigateTo<TViewModel>() where TViewModel : class;

    void NavigateTo(Type viewModelType);

    void NavigateTo(object viewModel);

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
