using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthApiService _authApiService;

    public LoginViewModel(AuthApiService authApiService)
    {
        _authApiService = authApiService;
    }

    [ObservableProperty]
    private string _userName = "admin";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public event Action? LoginSucceeded;

    [RelayCommand]
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
}
