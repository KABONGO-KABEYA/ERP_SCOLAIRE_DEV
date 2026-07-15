using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthApiService _authApiService;
    private readonly DispatcherTimer _clockTimer;

    public LoginViewModel(AuthApiService authApiService)
    {
        _authApiService = authApiService;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clockTimer.Tick += (_, _) => RefreshClock();
        RefreshClock();
        _clockTimer.Start();

        LoadServerInfo();
    }

    [ObservableProperty]
    private string _userName = "admin";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private bool _serverStatusOk = true;

    [ObservableProperty]
    private string _serverName = "TOUR";

    [ObservableProperty]
    private string _databaseName = "ERP_SCOLAIRE";

    [ObservableProperty]
    private string _currentDate = string.Empty;

    [ObservableProperty]
    private string _currentTime = string.Empty;

    [ObservableProperty]
    private string _appVersion = "2026.1.0";

    public event Action? LoginSucceeded;

    public event Action? ChangeSchoolRequested;

    public event Action? ForgotPasswordRequested;

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private void ChangeSchool() => ChangeSchoolRequested?.Invoke();

    [RelayCommand]
    private void ForgotPassword() => ForgotPasswordRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            await _authApiService.LoginAsync(
                new Application.Auth.DTOs.LoginRequest(UserName, Password));
            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin => !IsBusy;

    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();

    public void RefreshServerInfo() => LoadServerInfo();

    private void LoadServerInfo()
    {
        try
        {
            var bootstrap = new DatabaseConnectionBootstrap(AppContext.BaseDirectory);
            var config = bootstrap.LoadConfiguration();
            if (!string.IsNullOrWhiteSpace(config.Serveur))
            {
                ServerName = config.Serveur.Trim();
            }

            if (!string.IsNullOrWhiteSpace(config.Base))
            {
                DatabaseName = config.Base.Trim();
            }

            ServerStatusOk = true;
        }
        catch
        {
            ServerStatusOk = false;
        }
    }

    private void RefreshClock()
    {
        var now = DateTime.Now;
        CurrentDate = now.ToString("dd/MM/yyyy");
        CurrentTime = now.ToString("HH:mm");
    }
}
