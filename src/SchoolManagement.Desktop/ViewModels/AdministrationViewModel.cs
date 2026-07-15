using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Admin.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class AdministrationViewModel : ViewModelBase
{
    private readonly IAdminApiService _adminApiService;

    public AdministrationViewModel(IAdminApiService adminApiService)
    {
        _adminApiService = adminApiService;
        _ = LoadAsync();
    }

    public ObservableCollection<UserAccountDto> Users { get; } = [];
    public ObservableCollection<RoleDto> Roles { get; } = [];

    [ObservableProperty] private UserAccountDto? _selectedUser;
    [ObservableProperty] private RoleDto? _selectedRole;
    [ObservableProperty] private string _newUserName = string.Empty;
    [ObservableProperty] private string _newEmail = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _newFirstName = string.Empty;
    [ObservableProperty] private string _newLastName = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var users = await _adminApiService.GetUsersAsync();
            Users.Clear();
            foreach (var u in users) Users.Add(u);

            var roles = await _adminApiService.GetRolesAsync();
            Roles.Clear();
            foreach (var r in roles) Roles.Add(r);
            SelectedRole = Roles.FirstOrDefault();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CreateUserAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUserName) || string.IsNullOrWhiteSpace(NewPassword)
            || string.IsNullOrWhiteSpace(NewFirstName) || string.IsNullOrWhiteSpace(NewLastName))
        {
            StatusMessage = "Complétez les champs obligatoires.";
            return;
        }

        IsBusy = true;
        try
        {
            var roleIds = SelectedRole is not null ? new List<Guid> { SelectedRole.Id } : new List<Guid>();
            await _adminApiService.CreateUserAsync(new CreateUserRequest(
                NewUserName, NewEmail, NewPassword, NewFirstName, NewLastName, roleIds, null));

            NewUserName = string.Empty;
            NewEmail = string.Empty;
            NewPassword = string.Empty;
            NewFirstName = string.Empty;
            NewLastName = string.Empty;
            StatusMessage = "Utilisateur créé.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleUserActiveAsync()
    {
        if (SelectedUser is null) return;
        IsBusy = true;
        try
        {
            var wasActive = SelectedUser.IsActive;
            var parts = SelectedUser.FullName.Split(' ', 2);
            await _adminApiService.UpdateUserAsync(SelectedUser.Id, new UpdateUserRequest(
                SelectedUser.Email,
                parts.Length > 1 ? parts[1] : parts[0],
                parts[0],
                !wasActive));

            StatusMessage = wasActive ? "Utilisateur désactivé." : "Utilisateur activé.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AssignRoleAsync()
    {
        if (SelectedUser is null || SelectedRole is null) return;
        IsBusy = true;
        try
        {
            await _adminApiService.SetUserRolesAsync(SelectedUser.Id, new SetUserRolesRequest([SelectedRole.Id]));
            StatusMessage = $"Rôle {SelectedRole.Name} assigné.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
