using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class RolePermissionMatrixItem : ObservableObject
{
    public RolePermissionMatrixItem(PermissionCatalogItemDto catalog, bool isChecked, bool isReadOnly)
    {
        Code = catalog.Code;
        DisplayName = catalog.DisplayName;
        Module = catalog.Module;
        IsReadOnly = isReadOnly;
        _isChecked = isChecked;
    }

    public string Code { get; }
    public string DisplayName { get; }
    public string Module { get; }
    public bool IsReadOnly { get; }

    [ObservableProperty] private bool _isChecked;
}

public partial class SecurityRolesViewModel : ViewModelBase
{
    private readonly ISecurityAdminApiService _security;

    public SecurityRolesViewModel(ISecurityAdminApiService security)
    {
        _security = security;
        _ = LoadAsync();
    }

    public ObservableCollection<SecurityRoleDto> Roles { get; } = [];
    public ObservableCollection<RolePermissionMatrixItem> PermissionMatrix { get; } = [];

    [ObservableProperty] private SecurityRoleDto? _selectedRole;
    [ObservableProperty] private string _newRoleCode = string.Empty;
    [ObservableProperty] private string _newRoleName = string.Empty;
    [ObservableProperty] private string _editRoleName = string.Empty;
    [ObservableProperty] private string? _editRoleDescription;
    [ObservableProperty] private bool _permissionsReadOnly;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public bool CanDeleteRole => SelectedRole is not null && !SelectedRole.IsSystem;

    partial void OnSelectedRoleChanged(SecurityRoleDto? value)
    {
        OnPropertyChanged(nameof(CanDeleteRole));
        if (value is null)
        {
            PermissionMatrix.Clear();
            EditRoleName = string.Empty;
            EditRoleDescription = null;
            PermissionsReadOnly = false;
            return;
        }

        EditRoleName = value.Name;
        EditRoleDescription = value.Description;
        _ = LoadRolePermissionsAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var roles = await _security.GetRolesAsync();
            var selectedId = SelectedRole?.Id;
            Roles.Clear();
            foreach (var r in roles.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
                Roles.Add(r);
            SelectedRole = selectedId is null
                ? Roles.FirstOrDefault()
                : Roles.FirstOrDefault(r => r.Id == selectedId);
            StatusMessage = null;
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadRolePermissionsAsync()
    {
        if (SelectedRole is null) return;
        IsBusy = true;
        try
        {
            var catalog = await _security.GetPermissionCatalogAsync();
            var rolePerms = await _security.GetRolePermissionsAsync(SelectedRole.Id);
            PermissionsReadOnly = rolePerms.PermissionsReadOnly;
            var granted = rolePerms.PermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

            PermissionMatrix.Clear();
            foreach (var item in catalog.Where(c => c.IsActive).OrderBy(c => c.Module).ThenBy(c => c.DisplayName))
                PermissionMatrix.Add(new RolePermissionMatrixItem(item, granted.Contains(item.Code), rolePerms.PermissionsReadOnly));
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CreateRoleAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRoleCode) || string.IsNullOrWhiteSpace(NewRoleName))
        {
            StatusMessage = "Code et nom requis.";
            return;
        }

        IsBusy = true;
        try
        {
            var created = await _security.CreateRoleAsync(new CreateSecurityRoleRequest(
                NewRoleCode.Trim(),
                NewRoleName.Trim(),
                null));
            NewRoleCode = string.Empty;
            NewRoleName = string.Empty;
            StatusMessage = "Rôle créé.";
            await LoadAsync();
            SelectedRole = Roles.FirstOrDefault(r => r.Id == created.Id);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveRoleAsync()
    {
        if (SelectedRole is null) return;
        IsBusy = true;
        try
        {
            await _security.UpdateRoleAsync(SelectedRole.Id, new UpdateSecurityRoleRequest(
                EditRoleName.Trim(),
                EditRoleDescription,
                SelectedRole.IsAssignable,
                SelectedRole.SortOrder));
            StatusMessage = "Rôle mis à jour.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteRoleAsync()
    {
        if (SelectedRole is null || SelectedRole.IsSystem) return;
        IsBusy = true;
        try
        {
            await _security.DeleteRoleAsync(SelectedRole.Id);
            StatusMessage = "Rôle supprimé.";
            SelectedRole = null;
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SavePermissionsAsync()
    {
        if (SelectedRole is null || PermissionsReadOnly) return;
        IsBusy = true;
        try
        {
            var codes = PermissionMatrix.Where(m => m.IsChecked).Select(m => m.Code).ToList();
            await _security.SetRolePermissionsAsync(SelectedRole.Id, new SetRolePermissionsRequest(codes));
            StatusMessage = "Permissions enregistrées.";
            await LoadRolePermissionsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
