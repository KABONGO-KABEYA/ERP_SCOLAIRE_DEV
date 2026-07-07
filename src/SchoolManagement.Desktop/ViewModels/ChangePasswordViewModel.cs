using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Auth.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly AuthApiService _authApiService;
    private readonly bool _isMandatory;

    public ChangePasswordViewModel(AuthApiService authApiService, IAuthSessionService authSession)
    {
        _authApiService = authApiService;
        _isMandatory = authSession.CurrentUser?.MustChangePassword == true;
    }

    public bool IsMandatory => _isMandatory;

    public string Title => IsMandatory
        ? "Changement de mot de passe obligatoire"
        : "Changer le mot de passe";

    public string Description => IsMandatory
        ? "Votre compte exige un nouveau mot de passe avant de continuer."
        : "Saisissez votre mot de passe actuel puis le nouveau.";

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public event Action? PasswordChanged;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(CurrentPassword) ||
            string.IsNullOrWhiteSpace(NewPassword) ||
            string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ErrorMessage = "Tous les champs sont obligatoires.";
            return;
        }

        if (NewPassword.Length < 8)
        {
            ErrorMessage = "Le nouveau mot de passe doit contenir au moins 8 caractères.";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "La confirmation ne correspond pas au nouveau mot de passe.";
            return;
        }

        IsBusy = true;
        try
        {
            await _authApiService.ChangePasswordAsync(new ChangePasswordRequest(CurrentPassword, NewPassword));
            PasswordChanged?.Invoke();
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
