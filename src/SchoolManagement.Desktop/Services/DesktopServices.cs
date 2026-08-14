using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MaterialDesignThemes.Wpf;

namespace SchoolManagement.Desktop.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<object> _backStack = new();

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object? CurrentViewModel { get; private set; }

    public bool CanNavigateBack => _backStack.Count > 0;

    public event Action? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>(bool recordBack = false) where TViewModel : class
    {
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        NavigateTo(viewModel, recordBack);
    }

    public void NavigateTo(Type viewModelType, bool recordBack = false)
    {
        var viewModel = _serviceProvider.GetRequiredService(viewModelType);
        NavigateTo(viewModel, recordBack);
    }

    public void NavigateTo(object viewModel, bool recordBack = false)
    {
        if (!recordBack)
        {
            _backStack.Clear();
        }
        else if (CurrentViewModel is not null)
        {
            _backStack.Push(CurrentViewModel);
        }

        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke();
    }

    public bool NavigateBack()
    {
        if (_backStack.Count == 0)
        {
            return false;
        }

        CurrentViewModel = _backStack.Pop();
        CurrentViewModelChanged?.Invoke();
        return true;
    }

    public void Clear()
    {
        _backStack.Clear();
        if (CurrentViewModel is null)
        {
            return;
        }

        CurrentViewModel = null;
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
