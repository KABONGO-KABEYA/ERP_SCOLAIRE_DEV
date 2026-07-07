using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MaterialDesignThemes.Wpf;

namespace SchoolManagement.Desktop.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object? CurrentViewModel { get; private set; }

    public event Action? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        NavigateTo(viewModel);
    }

    public void NavigateTo(Type viewModelType)
    {
        var viewModel = _serviceProvider.GetRequiredService(viewModelType);
        NavigateTo(viewModel);
    }

    public void NavigateTo(object viewModel)
    {
        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke();
    }
}

public sealed class ThemeService : IThemeService
{
    public bool IsDarkTheme { get; private set; }

    public void SetTheme(bool isDark)
    {
        IsDarkTheme = isDark;
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(theme);
    }
}
