using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class SecurityRolePickItem : ObservableObject
{
    public SecurityRolePickItem(SecurityRoleDto role) => Role = role;

    public SecurityRoleDto Role { get; }

    public Guid Id => Role.Id;

    public string Code => Role.Code;

    public string Name => Role.Name;

    public string DisplayPrimary => Role.Name;

    public string DisplaySecondary => Role.Code;

    [ObservableProperty] private bool _isSelected;
}

/// <summary>Ligne de consultation du catalogue des permissions effectives (lecture seule).</summary>
public sealed class UserPermissionPickItem
{
    public required Guid PermissionId { get; init; }
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public required string Module { get; init; }
    public string? HelpText { get; init; }
    public bool IsEffective { get; init; }
    public IReadOnlyList<PermissionOriginDetailDto> Origins { get; init; } = [];

    public PermissionOriginKind? PrimaryOriginKind
    {
        get
        {
            if (Origins.Count == 0) return null;
            if (Origins.Any(o => o.Kind == PermissionOriginKind.Deny)) return PermissionOriginKind.Deny;
            if (Origins.Any(o => o.Kind == PermissionOriginKind.Grant)) return PermissionOriginKind.Grant;
            if (Origins.Any(o => o.Kind == PermissionOriginKind.Role)) return PermissionOriginKind.Role;
            if (Origins.Any(o => o.Kind == PermissionOriginKind.Dependency)) return PermissionOriginKind.Dependency;
            return Origins[0].Kind;
        }
    }

    public string OriginBadgeLabel => PrimaryOriginKind switch
    {
        PermissionOriginKind.Role => "Rôle",
        PermissionOriginKind.Grant => "Grant",
        PermissionOriginKind.Deny => "Deny",
        PermissionOriginKind.Dependency => "Dependency",
        _ => "—"
    };

    public string OriginBadgeBackground => PrimaryOriginKind switch
    {
        PermissionOriginKind.Role => "#DBEAFE",
        PermissionOriginKind.Grant => "#DCFCE7",
        PermissionOriginKind.Deny => "#FEE2E2",
        PermissionOriginKind.Dependency => "#FEF3C7",
        _ => "#F3F4F6"
    };

    public string OriginBadgeForeground => PrimaryOriginKind switch
    {
        PermissionOriginKind.Role => "#1D4ED8",
        PermissionOriginKind.Grant => "#166534",
        PermissionOriginKind.Deny => "#B91C1C",
        PermissionOriginKind.Dependency => "#B45309",
        _ => "#6B7280"
    };

    public string DetailSummary
    {
        get
        {
            if (Origins.Count == 0)
                return IsEffective ? "Accordée (origine non détaillée)." : "Non accordée.";

            return string.Join(" · ", Origins.Select(DescribeOrigin).Distinct());
        }
    }

    public string DetailOriginLabel => OriginBadgeLabel;

    public string DetailRoleLabel
    {
        get
        {
            var roles = Origins
                .Where(o => o.Kind == PermissionOriginKind.Role && !string.IsNullOrWhiteSpace(o.RoleCode))
                .Select(o => o.RoleCode!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return roles.Count == 0 ? "Aucune" : string.Join(", ", roles);
        }
    }

    public string DetailExceptionLabel
    {
        get
        {
            var ids = Origins
                .Where(o => (o.Kind is PermissionOriginKind.Grant or PermissionOriginKind.Deny) && o.ExceptionId.HasValue)
                .Select(o => o.ExceptionId!.Value.ToString("D"))
                .Distinct()
                .ToList();
            return ids.Count == 0 ? "Aucune" : string.Join(", ", ids);
        }
    }

    public string DetailDependencyLabel
    {
        get
        {
            var deps = Origins
                .Where(o => o.Kind == PermissionOriginKind.Dependency && !string.IsNullOrWhiteSpace(o.SourcePermissionCode))
                .Select(o => o.SourcePermissionCode!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return deps.Count == 0 ? "Aucune" : string.Join(", ", deps);
        }
    }

    public string DetailEffectiveStatus => IsEffective ? "Accordée" : "Non accordée";

    public string DetailEffectiveStatusBrush => IsEffective ? "#166534" : "#6B7280";

    public string OriginSummary => OriginBadgeLabel;

    private static string DescribeOrigin(PermissionOriginDetailDto o) => o.Kind switch
    {
        PermissionOriginKind.Role => string.IsNullOrWhiteSpace(o.RoleCode)
            ? "Héritée d'un rôle"
            : $"Héritée du rôle {o.RoleCode}",
        PermissionOriginKind.Grant => "Accordée par exception",
        PermissionOriginKind.Deny => "Refusée par exception",
        PermissionOriginKind.Dependency => !string.IsNullOrWhiteSpace(o.Note)
            ? o.Note!
            : string.IsNullOrWhiteSpace(o.SourcePermissionCode)
                ? "Dépendance"
                : $"Prérequis de {o.SourcePermissionCode}",
        _ => o.Note ?? o.Kind.ToString()
    };
}

public partial class SecurityUsersViewModel : ViewModelBase
{
    private static readonly HashSet<string> LegacyRoleCodesHiddenWhenPreferredExists =
        new(StringComparer.OrdinalIgnoreCase) { "TEACHER" };

    private static readonly Dictionary<string, string> LegacyRolePreferredCodes =
        new(StringComparer.OrdinalIgnoreCase) { ["TEACHER"] = "ENSEIGNANT" };

    private readonly ISecurityAdminApiService _security;
    private readonly List<SecurityUserDto> _allUsers = [];
    private readonly List<SecurityRolePickItem> _allRolePicks = [];
    private readonly List<UserPermissionPickItem> _allUserPermissions = [];
    private List<UserPermissionPickItem> _filteredUserPermissions = [];
    private List<PermissionCatalogItemDto> _permissionCatalog = [];
    private const int PermissionPageSize = 25;

    public SecurityUsersViewModel(ISecurityAdminApiService security)
    {
        _security = security;
        _ = LoadAsync();
    }

    public ObservableCollection<SecurityUserDto> Users { get; } = [];
    public ObservableCollection<SecurityRolePickItem> RolePicks { get; } = [];
    public ObservableCollection<UserPermissionPickItem> UserPermissions { get; } = [];
    public ObservableCollection<SecurityExceptionDto> UserExceptions { get; } = [];
    public ObservableCollection<PermissionCatalogItemDto> ExceptionPermissionChoices { get; } = [];
    public ObservableCollection<SecurityPersonnelCandidateDto> PersonnelCandidates { get; } = [];

    public Array ExceptionEffectChoices { get; } = Enum.GetValues(typeof(PermissionExceptionEffect));

    [ObservableProperty] private SecurityUserDto? _selectedUser;
    [ObservableProperty] private UserPermissionPickItem? _selectedPermission;
    [ObservableProperty] private SecurityExceptionDto? _selectedUserException;
    [ObservableProperty] private SecurityPersonnelCandidateDto? _selectedPersonnelCandidate;
    [ObservableProperty] private string? _effectiveRolesSummary;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _roleSearchText = string.Empty;
    [ObservableProperty] private string _permissionSearchText = string.Empty;
    [ObservableProperty] private string _personnelSearchText = string.Empty;
    [ObservableProperty] private int _permissionCurrentPage = 1;
    [ObservableProperty] private int _permissionTotalCount;
    [ObservableProperty] private Guid _newExceptionPermissionId;
    [ObservableProperty] private PermissionExceptionEffect _newExceptionEffect = PermissionExceptionEffect.Grant;
    [ObservableProperty] private DateTime _newExceptionValidFrom = DateTime.Today;
    [ObservableProperty] private DateTime? _newExceptionValidTo;
    [ObservableProperty] private string? _newExceptionReason;
    [ObservableProperty] private string _newUserName = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _newPasswordConfirm = string.Empty;
    [ObservableProperty] private string _resetPassword = string.Empty;
    [ObservableProperty] private bool _isResetPasswordVisible;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public bool HasSelectedUser => SelectedUser is not null;

    public bool HasSelectedPersonnelCandidate => SelectedPersonnelCandidate is not null;

    public bool CanCreateUser =>
        SelectedPersonnelCandidate is not null
        && !string.IsNullOrWhiteSpace(NewUserName)
        && !string.IsNullOrWhiteSpace(NewPassword)
        && !string.IsNullOrWhiteSpace(NewPasswordConfirm);

    public int PermissionTotalPages => Math.Max(1, (int)Math.Ceiling(PermissionTotalCount / (double)PermissionPageSize));

    public string PermissionPaginationLabel
    {
        get
        {
            if (PermissionTotalCount == 0)
                return "0 sur 0";
            var start = ((PermissionCurrentPage - 1) * PermissionPageSize) + 1;
            var end = Math.Min(PermissionCurrentPage * PermissionPageSize, PermissionTotalCount);
            return $"{start} à {end} sur {PermissionTotalCount}";
        }
    }

    public bool CanGoPreviousPermissionPage => PermissionCurrentPage > 1;
    public bool CanGoNextPermissionPage => PermissionCurrentPage < PermissionTotalPages;

    public string SelectedUserStatusLabel => SelectedUser is null
        ? "—"
        : SelectedUser.IsActive ? "Actif" : "Inactif";

    public string SelectedUserStatusBrush => SelectedUser?.IsActive == true ? "#166534" : "#B45309";

    public string SelectedUserLastLoginLabel
    {
        get
        {
            if (SelectedUser?.LastLoginAt is null)
                return "Jamais connecté / non renseigné";
            return SelectedUser.LastLoginAt.Value.ToLocalTime().ToString("g");
        }
    }

    public string SelectedUserMustChangePasswordLabel =>
        SelectedUser?.MustChangePassword == true ? "Oui — au prochain login" : "Non";

    public string SelectedUserPlatformLabel =>
        SelectedUser?.IsPlatformSuperAdmin == true ? "Super Admin plateforme" : "Utilisateur établissement";

    public IReadOnlyList<string> SelectedUserRoleBadges =>
        SelectedUser?.RoleLabels is { Count: > 0 } labels
            ? labels
            : SelectedUser?.Roles ?? Array.Empty<string>();

    partial void OnSelectedUserChanged(SecurityUserDto? value)
    {
        IsResetPasswordVisible = false;
        ResetPassword = string.Empty;
        OnPropertyChanged(nameof(HasSelectedUser));
        OnPropertyChanged(nameof(SelectedUserStatusLabel));
        OnPropertyChanged(nameof(SelectedUserStatusBrush));
        OnPropertyChanged(nameof(SelectedUserLastLoginLabel));
        OnPropertyChanged(nameof(SelectedUserMustChangePasswordLabel));
        OnPropertyChanged(nameof(SelectedUserPlatformLabel));
        OnPropertyChanged(nameof(SelectedUserRoleBadges));
        _ = LoadUserPermissionsAsync();
        _ = LoadUserExceptionsAsync();
        SyncRolePicksFromSelectedUser();
    }

    partial void OnSearchTextChanged(string value) => ApplyUserFilter();

    partial void OnRoleSearchTextChanged(string value) => ApplyRoleFilter();

    partial void OnPermissionSearchTextChanged(string value)
    {
        PermissionCurrentPage = 1;
        ApplyPermissionFilter();
    }

    private void NotifyPermissionPagination()
    {
        OnPropertyChanged(nameof(PermissionTotalPages));
        OnPropertyChanged(nameof(PermissionPaginationLabel));
        OnPropertyChanged(nameof(CanGoPreviousPermissionPage));
        OnPropertyChanged(nameof(CanGoNextPermissionPage));
    }

    private void SyncRolePicksFromSelectedUser()
    {
        if (SelectedUser is null)
        {
            foreach (var pick in _allRolePicks) pick.IsSelected = false;
            return;
        }

        var ids = SelectedUser.RoleIds.ToHashSet();
        var codes = SelectedUser.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pick in _allRolePicks)
        {
            var selected = ids.Contains(pick.Id);
            if (!selected && LegacyRolePreferredCodes.ContainsValue(pick.Code))
            {
                // TEACHER legacy → coche le rôle métier ENSEIGNANT à l'affichage.
                selected = codes.Contains("TEACHER") || codes.Contains(pick.Code);
            }

            pick.IsSelected = selected;
        }
    }

    private void ApplyUserFilter()
    {
        var selectedId = SelectedUser?.Id;
        var term = SearchText.Trim();
        IEnumerable<SecurityUserDto> filtered = _allUsers;
        if (!string.IsNullOrWhiteSpace(term))
        {
            filtered = _allUsers.Where(u =>
                u.UserName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || u.LastName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        Users.Clear();
        foreach (var u in filtered.OrderBy(x => x.UserName))
            Users.Add(u);

        SelectedUser = selectedId is null
            ? Users.FirstOrDefault()
            : Users.FirstOrDefault(u => u.Id == selectedId) ?? Users.FirstOrDefault();
    }

    private void ApplyRoleFilter()
    {
        var term = RoleSearchText.Trim();
        RolePicks.Clear();
        IEnumerable<SecurityRolePickItem> filtered = _allRolePicks;
        if (!string.IsNullOrWhiteSpace(term))
        {
            filtered = _allRolePicks.Where(r =>
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.Code.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var r in filtered.OrderBy(x => x.Name))
            RolePicks.Add(r);
    }

    private static IEnumerable<SecurityRoleDto> FilterAssignableBusinessRoles(IEnumerable<SecurityRoleDto> roles)
    {
        var list = roles.Where(r => r.IsAssignable).ToList();
        var codes = list.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return list.Where(r =>
        {
            if (!LegacyRoleCodesHiddenWhenPreferredExists.Contains(r.Code))
                return true;
            return !LegacyRolePreferredCodes.TryGetValue(r.Code, out var preferred)
                   || !codes.Contains(preferred);
        });
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var users = await _security.GetUsersAsync();
            _allUsers.Clear();
            _allUsers.AddRange(users);

            var roles = await _security.GetRolesAsync();
            _allRolePicks.Clear();
            foreach (var r in FilterAssignableBusinessRoles(roles).OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
                _allRolePicks.Add(new SecurityRolePickItem(r));

            _permissionCatalog = (await _security.GetPermissionCatalogAsync())
                .Where(c => c.IsActive)
                .OrderBy(c => c.Module)
                .ThenBy(c => c.DisplayName)
                .ToList();

            ExceptionPermissionChoices.Clear();
            foreach (var p in _permissionCatalog)
                ExceptionPermissionChoices.Add(p);
            if (ExceptionPermissionChoices.Count > 0 && NewExceptionPermissionId == Guid.Empty)
                NewExceptionPermissionId = ExceptionPermissionChoices[0].Id;

            ApplyRoleFilter();
            ApplyUserFilter();
            StatusMessage = null;
            await SearchPersonnelCandidatesAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadUserPermissionsAsync()
    {
        _allUserPermissions.Clear();
        _filteredUserPermissions = [];
        UserPermissions.Clear();
        SelectedPermission = null;
        EffectiveRolesSummary = null;
        PermissionCurrentPage = 1;
        PermissionTotalCount = 0;
        NotifyPermissionPagination();
        if (SelectedUser is null) return;

        IsBusy = true;
        try
        {
            if (_permissionCatalog.Count == 0)
            {
                _permissionCatalog = (await _security.GetPermissionCatalogAsync())
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Module)
                    .ThenBy(c => c.DisplayName)
                    .ToList();
            }

            var explanation = await _security.GetEffectivePermissionsAsync(SelectedUser.Id);
            var byCode = explanation.Permissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
            var grantedCount = explanation.IsPlatformSuperAdmin
                ? _permissionCatalog.Count
                : explanation.Permissions.Count(p => p.IsEffective);

            EffectiveRolesSummary = explanation.IsPlatformSuperAdmin
                ? "Super-admin plateforme — toutes les permissions du catalogue sont effectives."
                : $"Rôles : {string.Join(", ", PreferRoleLabels(explanation.Roles))} — {grantedCount} permission(s) effective(s) sur {_permissionCatalog.Count}.";

            foreach (var catalog in _permissionCatalog)
            {
                byCode.TryGetValue(catalog.Code, out var explained);
                _allUserPermissions.Add(new UserPermissionPickItem
                {
                    PermissionId = catalog.Id,
                    Code = catalog.Code,
                    DisplayName = catalog.DisplayName,
                    Module = catalog.Module,
                    HelpText = catalog.HelpText,
                    IsEffective = explained?.IsEffective == true || explanation.IsPlatformSuperAdmin,
                    Origins = explained?.Origins ?? Array.Empty<PermissionOriginDetailDto>()
                });
            }

            ApplyPermissionFilter();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private void ApplyPermissionFilter()
    {
        var term = PermissionSearchText.Trim();
        IEnumerable<UserPermissionPickItem> filtered = _allUserPermissions;

        if (!string.IsNullOrWhiteSpace(term))
        {
            filtered = filtered.Where(p =>
                p.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.Module.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.DetailSummary.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.OriginBadgeLabel.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        _filteredUserPermissions = filtered
            .OrderByDescending(x => x.IsEffective)
            .ThenBy(x => x.Module)
            .ThenBy(x => x.DisplayName)
            .ToList();

        PermissionTotalCount = _filteredUserPermissions.Count;
        if (PermissionCurrentPage > PermissionTotalPages)
            PermissionCurrentPage = PermissionTotalPages;
        if (PermissionCurrentPage < 1)
            PermissionCurrentPage = 1;

        ApplyPermissionPage();
    }

    private void ApplyPermissionPage()
    {
        var selectedCode = SelectedPermission?.Code;
        var pageItems = _filteredUserPermissions
            .Skip((PermissionCurrentPage - 1) * PermissionPageSize)
            .Take(PermissionPageSize)
            .ToList();

        UserPermissions.Clear();
        foreach (var p in pageItems)
            UserPermissions.Add(p);

        SelectedPermission = selectedCode is null
            ? UserPermissions.FirstOrDefault()
            : UserPermissions.FirstOrDefault(p => p.Code.Equals(selectedCode, StringComparison.OrdinalIgnoreCase))
              ?? UserPermissions.FirstOrDefault();

        NotifyPermissionPagination();
    }

    [RelayCommand]
    private void PreviousPermissionPage()
    {
        if (!CanGoPreviousPermissionPage) return;
        PermissionCurrentPage--;
        ApplyPermissionPage();
    }

    [RelayCommand]
    private void NextPermissionPage()
    {
        if (!CanGoNextPermissionPage) return;
        PermissionCurrentPage++;
        ApplyPermissionPage();
    }

    private static IEnumerable<string> PreferRoleLabels(IEnumerable<string> roleCodes)
    {
        var codes = roleCodes.ToList();
        var set = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes)
        {
            if (code.Equals("TEACHER", StringComparison.OrdinalIgnoreCase) && set.Contains("ENSEIGNANT"))
                continue;
            yield return code.Equals("TEACHER", StringComparison.OrdinalIgnoreCase) ? "Enseignant" : code;
        }
    }

    private IReadOnlyList<Guid> SelectedRoleIds() =>
        _allRolePicks.Where(p => p.IsSelected).Select(p => p.Id).ToList();

    [RelayCommand]
    private void BeginResetPassword()
    {
        if (SelectedUser is null) return;
        IsResetPasswordVisible = true;
        ResetPassword = string.Empty;
        StatusMessage = "Saisissez le nouveau mot de passe, puis confirmez.";
    }

    [RelayCommand]
    private void CancelResetPassword()
    {
        IsResetPasswordVisible = false;
        ResetPassword = string.Empty;
        StatusMessage = null;
    }

    partial void OnSelectedPersonnelCandidateChanged(SecurityPersonnelCandidateDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedPersonnelCandidate));
        OnPropertyChanged(nameof(CanCreateUser));
        if (value is not null && string.IsNullOrWhiteSpace(NewUserName))
        {
            var suggestion = string.IsNullOrWhiteSpace(value.EmployeeNumber)
                ? $"{value.FirstName}.{value.LastName}".ToLowerInvariant()
                : value.EmployeeNumber.Trim().ToLowerInvariant();
            NewUserName = suggestion.Replace(' ', '.');
        }
    }

    partial void OnNewUserNameChanged(string value) => OnPropertyChanged(nameof(CanCreateUser));
    partial void OnNewPasswordChanged(string value) => OnPropertyChanged(nameof(CanCreateUser));
    partial void OnNewPasswordConfirmChanged(string value) => OnPropertyChanged(nameof(CanCreateUser));

    partial void OnPersonnelSearchTextChanged(string value) => _ = SearchPersonnelCandidatesAsync();

    [RelayCommand]
    private async Task SearchPersonnelCandidatesAsync()
    {
        try
        {
            var list = await _security.SearchPersonnelCandidatesAsync(PersonnelSearchText);
            var selectedId = SelectedPersonnelCandidate?.TeacherId;
            PersonnelCandidates.Clear();
            foreach (var c in list)
                PersonnelCandidates.Add(c);

            SelectedPersonnelCandidate = selectedId is null
                ? null
                : PersonnelCandidates.FirstOrDefault(c => c.TeacherId == selectedId);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateUserAsync()
    {
        if (SelectedPersonnelCandidate is null)
        {
            StatusMessage = "Sélectionnez un personnel.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewUserName) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "Identifiant et mot de passe requis.";
            return;
        }

        if (!string.Equals(NewPassword, NewPasswordConfirm, StringComparison.Ordinal))
        {
            StatusMessage = "La confirmation du mot de passe ne correspond pas.";
            return;
        }

        IsBusy = true;
        try
        {
            var created = await _security.CreateUserAsync(new CreateSecurityUserRequest(
                SelectedPersonnelCandidate.TeacherId,
                NewUserName.Trim(),
                NewPassword,
                MustChangePassword: true));

            NewUserName = string.Empty;
            NewPassword = string.Empty;
            NewPasswordConfirm = string.Empty;
            SelectedPersonnelCandidate = null;
            PersonnelSearchText = string.Empty;
            StatusMessage = $"Compte « {created.UserName} » créé. Attribuez ensuite un rôle dans l'onglet Rôles.";
            await LoadAsync();
            SelectedUser = Users.FirstOrDefault(u => u.Id == created.Id);
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
            var u = SelectedUser;
            await _security.UpdateUserAsync(u.Id, new UpdateSecurityUserRequest(
                u.Email,
                u.FirstName,
                u.LastName,
                !u.IsActive));

            StatusMessage = u.IsActive ? "Utilisateur désactivé." : "Utilisateur activé.";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AssignRolesAsync()
    {
        if (SelectedUser is null) return;
        IsBusy = true;
        try
        {
            var userId = SelectedUser.Id;
            await _security.SetUserRolesAsync(userId, new SetSecurityUserRolesRequest(SelectedRoleIds()));
            StatusMessage = "Rôles enregistrés.";
            await LoadAsync();
            SelectedUser = Users.FirstOrDefault(u => u.Id == userId);
            await LoadUserPermissionsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadUserExceptionsAsync()
    {
        UserExceptions.Clear();
        SelectedUserException = null;
        if (SelectedUser is null) return;

        try
        {
            var list = await _security.GetExceptionsAsync(SelectedUser.Id);
            foreach (var e in list.OrderByDescending(x => x.ValidFrom))
                UserExceptions.Add(e);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateUserExceptionAsync()
    {
        if (SelectedUser is null)
        {
            StatusMessage = "Sélectionnez un utilisateur.";
            return;
        }

        if (NewExceptionPermissionId == Guid.Empty)
        {
            StatusMessage = "Sélectionnez une permission.";
            return;
        }

        IsBusy = true;
        try
        {
            await _security.CreateExceptionAsync(new CreateSecurityExceptionRequest(
                SelectedUser.Id,
                NewExceptionPermissionId,
                NewExceptionEffect,
                NewExceptionValidFrom,
                NewExceptionValidTo,
                NewExceptionReason));

            NewExceptionReason = null;
            NewExceptionValidTo = null;
            StatusMessage = "Exception de permission créée.";
            await LoadUserExceptionsAsync();
            await LoadUserPermissionsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CloseUserExceptionAsync()
    {
        if (SelectedUserException is null)
        {
            StatusMessage = "Sélectionnez une exception à clôturer.";
            return;
        }

        IsBusy = true;
        try
        {
            await _security.CloseExceptionAsync(SelectedUserException.Id);
            StatusMessage = "Exception clôturée.";
            await LoadUserExceptionsAsync();
            await LoadUserPermissionsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        if (SelectedUser is null) return;
        if (string.IsNullOrWhiteSpace(ResetPassword))
        {
            StatusMessage = "Saisissez le nouveau mot de passe.";
            return;
        }

        IsBusy = true;
        try
        {
            await _security.ResetPasswordAsync(SelectedUser.Id, new ResetPasswordRequest(ResetPassword, true));
            ResetPassword = string.Empty;
            IsResetPasswordVisible = false;
            StatusMessage = "Mot de passe réinitialisé.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
