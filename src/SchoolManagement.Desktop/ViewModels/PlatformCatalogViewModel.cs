using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public enum CatalogNodeKind
{
    None,
    Module,
    Function,
    Page,
    Action
}

public sealed partial class CatalogTreeNodeViewModel : ObservableObject
{
    public CatalogTreeNodeViewModel(
        CatalogNodeKind kind,
        Guid id,
        string code,
        string title,
        Guid? parentId,
        int sortOrder,
        bool isActive,
        string? description = null,
        string? icon = null,
        string? desktopViewKey = null,
        string? requiredPermissionCode = null,
        IReadOnlyList<CatalogTreeNodeViewModel>? children = null)
    {
        Kind = kind;
        Id = id;
        Code = code;
        Title = title;
        ParentId = parentId;
        SortOrder = sortOrder;
        IsActive = isActive;
        Description = description;
        Icon = icon;
        DesktopViewKey = desktopViewKey;
        RequiredPermissionCode = requiredPermissionCode;
        if (children is not null)
        {
            foreach (var c in children)
            {
                Children.Add(c);
            }
        }
    }

    public CatalogNodeKind Kind { get; }
    public Guid Id { get; }
    public string Code { get; }
    public string Title { get; }
    public Guid? ParentId { get; }
    public int SortOrder { get; }
    public bool IsActive { get; }
    public string? Description { get; }
    public string? Icon { get; }
    public string? DesktopViewKey { get; }
    public string? RequiredPermissionCode { get; }
    public ObservableCollection<CatalogTreeNodeViewModel> Children { get; } = [];

    public string Display => $"{Code} — {Title}";
}

public partial class PlatformCatalogViewModel : ViewModelBase
{
    private static readonly HashSet<string> ProtectedPermissionCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        global::SchoolManagement.Shared.Constants.Permissions.PlatformSuperAdmin,
        global::SchoolManagement.Shared.Constants.Permissions.PlatformCatalogManage,
        global::SchoolManagement.Shared.Constants.Permissions.AdminFull
    };

    private static readonly HashSet<string> ProtectedModuleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SECURITY", "PLATFORM", "SETTINGS"
    };

    private readonly IPlatformCatalogApiService _catalog;

    public PlatformCatalogViewModel(IPlatformCatalogApiService catalog)
    {
        _catalog = catalog;
        _ = LoadAsync();
    }

    public ObservableCollection<CatalogTreeNodeViewModel> TreeRoots { get; } = [];
    public ObservableCollection<SecurityPermissionAdminDto> Permissions { get; } = [];
    public ObservableCollection<PermissionDependencyDto> Dependencies { get; } = [];
    public ObservableCollection<SecurityAuditLogDto> PlatformAudit { get; } = [];

    [ObservableProperty] private CatalogTreeNodeViewModel? _selectedTreeNode;
    [ObservableProperty] private SecurityPermissionAdminDto? _selectedPermission;
    [ObservableProperty] private PermissionDependencyDto? _selectedDependency;
    [ObservableProperty] private CatalogNodeKind _editorKind = CatalogNodeKind.None;
    [ObservableProperty] private bool _isCreateMode;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private string _editCode = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string? _editDescription;
    [ObservableProperty] private string? _editIcon;
    [ObservableProperty] private int _editSortOrder = 1;
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private Guid _editParentModuleId;
    [ObservableProperty] private Guid _editParentFunctionId;
    [ObservableProperty] private Guid _editParentPageId;
    [ObservableProperty] private string? _editDesktopViewKey;
    [ObservableProperty] private string? _editRequiredPermissionCode;
    [ObservableProperty] private bool _editAvailableOnDesktop = true;

    [ObservableProperty] private string _permCode = string.Empty;
    [ObservableProperty] private string _permDisplayName = string.Empty;
    [ObservableProperty] private string _permModule = string.Empty;
    [ObservableProperty] private string _permBusinessDescription = string.Empty;
    [ObservableProperty] private string? _permHelpText;
    [ObservableProperty] private bool _permIsActive = true;
    [ObservableProperty] private Guid? _permSecurityActionId;

    [ObservableProperty] private Guid? _depPermissionId;
    [ObservableProperty] private Guid? _depRequiresPermissionId;

    public bool IsCodeEditable => IsCreateMode;
    public bool IsTreeEditorVisible => EditorKind is CatalogNodeKind.Module or CatalogNodeKind.Function
        or CatalogNodeKind.Page or CatalogNodeKind.Action;
    public bool ShowModuleFields => EditorKind == CatalogNodeKind.Module;
    public bool ShowFunctionFields => EditorKind == CatalogNodeKind.Function;
    public bool ShowPageFields => EditorKind == CatalogNodeKind.Page;
    public bool ShowActionFields => EditorKind == CatalogNodeKind.Action;
    public bool IsPermissionCodeEditable => SelectedPermission is null;

    partial void OnSelectedTreeNodeChanged(CatalogTreeNodeViewModel? value)
    {
        IsCreateMode = false;
        if (value is null)
        {
            EditorKind = CatalogNodeKind.None;
            NotifyEditorVisibility();
            return;
        }

        EditorKind = value.Kind;
        EditCode = value.Code;
        EditName = value.Title;
        EditDescription = value.Description;
        EditIcon = value.Icon;
        EditSortOrder = value.SortOrder;
        EditIsActive = value.IsActive;
        EditDesktopViewKey = value.DesktopViewKey;
        EditRequiredPermissionCode = value.RequiredPermissionCode;
        NotifyEditorVisibility();

        switch (value.Kind)
        {
            case CatalogNodeKind.Module:
                EditParentModuleId = value.Id;
                break;
            case CatalogNodeKind.Function:
                EditParentModuleId = value.ParentId ?? Guid.Empty;
                EditParentFunctionId = value.Id;
                break;
            case CatalogNodeKind.Page:
                EditParentFunctionId = value.ParentId ?? Guid.Empty;
                EditParentPageId = value.Id;
                EditAvailableOnDesktop = true;
                break;
            case CatalogNodeKind.Action:
                EditParentPageId = value.ParentId ?? Guid.Empty;
                EditAvailableOnDesktop = true;
                break;
        }
    }

    partial void OnSelectedPermissionChanged(SecurityPermissionAdminDto? value)
    {
        if (value is null)
        {
            PermCode = string.Empty;
            PermDisplayName = string.Empty;
            PermModule = string.Empty;
            PermBusinessDescription = string.Empty;
            PermHelpText = null;
            PermIsActive = true;
            PermSecurityActionId = null;
        }
        else
        {
            PermCode = value.Code;
            PermDisplayName = value.DisplayName;
            PermModule = value.Module;
            PermBusinessDescription = value.Description;
            PermHelpText = value.HelpText;
            PermIsActive = value.IsActive;
            PermSecurityActionId = value.SecurityActionId;
        }

        OnPropertyChanged(nameof(IsPermissionCodeEditable));
    }

    partial void OnIsCreateModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCodeEditable));
        OnPropertyChanged(nameof(IsPermissionCodeEditable));
    }

    partial void OnEditorKindChanged(CatalogNodeKind value) => NotifyEditorVisibility();

    private void NotifyEditorVisibility()
    {
        OnPropertyChanged(nameof(IsTreeEditorVisible));
        OnPropertyChanged(nameof(ShowModuleFields));
        OnPropertyChanged(nameof(ShowFunctionFields));
        OnPropertyChanged(nameof(ShowPageFields));
        OnPropertyChanged(nameof(ShowActionFields));
        OnPropertyChanged(nameof(IsCodeEditable));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var tree = await _catalog.GetTreeAsync();
            var perms = await _catalog.GetPermissionsAsync();
            var deps = await _catalog.GetDependenciesAsync();

            TreeRoots.Clear();
            foreach (var node in BuildTree(tree))
            {
                TreeRoots.Add(node);
            }

            Permissions.Clear();
            foreach (var p in perms.OrderBy(x => x.Module).ThenBy(x => x.Code))
            {
                Permissions.Add(p);
            }

            Dependencies.Clear();
            foreach (var d in deps)
            {
                Dependencies.Add(d);
            }

            StatusMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadPlatformAuditAsync()
    {
        IsBusy = true;
        try
        {
            var rows = await _catalog.QueryPlatformAuditAsync(new SecurityAuditQuery(Take: 200));
            PlatformAudit.Clear();
            foreach (var row in rows)
            {
                PlatformAudit.Add(row);
            }

            StatusMessage = $"{PlatformAudit.Count} entrée(s) d'audit plateforme.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BeginNewModule()
    {
        SelectedTreeNode = null;
        IsCreateMode = true;
        EditorKind = CatalogNodeKind.Module;
        EditCode = string.Empty;
        EditName = string.Empty;
        EditDescription = null;
        EditIcon = "CircleOutline";
        EditSortOrder = (TreeRoots.Count + 1) * 10;
        EditIsActive = true;
        NotifyEditorVisibility();
    }

    [RelayCommand]
    private void BeginNewChild()
    {
        if (SelectedTreeNode is null)
        {
            StatusMessage = "Sélectionnez un nœud parent dans l'arbre.";
            return;
        }

        IsCreateMode = true;
        EditCode = string.Empty;
        EditName = string.Empty;
        EditDescription = null;
        EditSortOrder = 10;
        EditIsActive = true;
        EditAvailableOnDesktop = true;

        switch (SelectedTreeNode.Kind)
        {
            case CatalogNodeKind.Module:
                EditorKind = CatalogNodeKind.Function;
                EditParentModuleId = SelectedTreeNode.Id;
                EditIcon = null;
                break;
            case CatalogNodeKind.Function:
                EditorKind = CatalogNodeKind.Page;
                EditParentFunctionId = SelectedTreeNode.Id;
                EditParentModuleId = SelectedTreeNode.ParentId ?? Guid.Empty;
                EditDesktopViewKey = string.Empty;
                EditRequiredPermissionCode = string.Empty;
                break;
            case CatalogNodeKind.Page:
                EditorKind = CatalogNodeKind.Action;
                EditParentPageId = SelectedTreeNode.Id;
                EditParentFunctionId = SelectedTreeNode.ParentId ?? Guid.Empty;
                break;
            default:
                StatusMessage = "Impossible d'ajouter un enfant à une action.";
                IsCreateMode = false;
                return;
        }

        NotifyEditorVisibility();
    }

    [RelayCommand]
    private async Task SaveTreeNodeAsync()
    {
        if (EditorKind == CatalogNodeKind.None)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditName) || (IsCreateMode && string.IsNullOrWhiteSpace(EditCode)))
        {
            StatusMessage = "Code et nom sont obligatoires.";
            return;
        }

        if (!IsCreateMode && !EditIsActive)
        {
            if (EditorKind == CatalogNodeKind.Module && ProtectedModuleCodes.Contains(EditCode))
            {
                StatusMessage = $"Le module « {EditCode} » ne peut pas être désactivé depuis le catalogue.";
                EditIsActive = true;
                return;
            }
        }

        IsBusy = true;
        try
        {
            switch (EditorKind)
            {
                case CatalogNodeKind.Module:
                    await SaveModuleAsync();
                    break;
                case CatalogNodeKind.Function:
                    await SaveFunctionAsync();
                    break;
                case CatalogNodeKind.Page:
                    await SavePageAsync();
                    break;
                case CatalogNodeKind.Action:
                    await SaveActionAsync();
                    break;
            }

            StatusMessage = IsCreateMode ? "Élément créé." : "Élément mis à jour.";
            IsCreateMode = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveModuleAsync()
    {
        var request = new UpsertSecurityModuleRequest(
            EditCode.Trim(),
            EditName.Trim(),
            EditDescription,
            EditIcon,
            EditSortOrder,
            EditIsActive);

        if (IsCreateMode)
        {
            await _catalog.CreateModuleAsync(request);
        }
        else if (SelectedTreeNode is not null)
        {
            await _catalog.UpdateModuleAsync(SelectedTreeNode.Id, request with { Code = EditCode });
        }
    }

    private async Task SaveFunctionAsync()
    {
        if (EditParentModuleId == Guid.Empty)
        {
            throw new InvalidOperationException("Module parent requis.");
        }

        var request = new UpsertSecurityFunctionRequest(
            EditParentModuleId,
            EditCode.Trim(),
            EditName.Trim(),
            EditDescription,
            EditIcon,
            EditSortOrder,
            EditIsActive);

        if (IsCreateMode)
        {
            await _catalog.CreateFunctionAsync(request);
        }
        else if (SelectedTreeNode is not null)
        {
            await _catalog.UpdateFunctionAsync(SelectedTreeNode.Id, request with { Code = EditCode });
        }
    }

    private async Task SavePageAsync()
    {
        if (EditParentFunctionId == Guid.Empty)
        {
            throw new InvalidOperationException("Fonction parente requise.");
        }

        var request = new UpsertSecurityPageRequest(
            EditParentFunctionId,
            EditCode.Trim(),
            EditName.Trim(),
            EditDescription,
            EditSortOrder,
            EditIsActive,
            string.IsNullOrWhiteSpace(EditRequiredPermissionCode) ? null : EditRequiredPermissionCode.Trim(),
            string.IsNullOrWhiteSpace(EditDesktopViewKey) ? null : EditDesktopViewKey.Trim(),
            null,
            null,
            EditAvailableOnDesktop);

        if (IsCreateMode)
        {
            await _catalog.CreatePageAsync(request);
        }
        else if (SelectedTreeNode is not null)
        {
            await _catalog.UpdatePageAsync(SelectedTreeNode.Id, request with { Code = EditCode });
        }
    }

    private async Task SaveActionAsync()
    {
        if (EditParentPageId == Guid.Empty)
        {
            throw new InvalidOperationException("Page parente requise.");
        }

        var request = new UpsertSecurityActionRequest(
            EditParentPageId,
            EditCode.Trim(),
            EditName.Trim(),
            EditDescription,
            EditSortOrder,
            EditIsActive,
            EditAvailableOnDesktop);

        if (IsCreateMode)
        {
            await _catalog.CreateActionAsync(request);
        }
        else if (SelectedTreeNode is not null)
        {
            await _catalog.UpdateActionAsync(SelectedTreeNode.Id, request with { Code = EditCode });
        }
    }

    [RelayCommand]
    private void BeginNewPermission()
    {
        SelectedPermission = null;
        PermCode = string.Empty;
        PermDisplayName = string.Empty;
        PermModule = string.Empty;
        PermBusinessDescription = string.Empty;
        PermHelpText = null;
        PermIsActive = true;
        PermSecurityActionId = SelectedTreeNode?.Kind == CatalogNodeKind.Action ? SelectedTreeNode.Id : null;
        OnPropertyChanged(nameof(IsPermissionCodeEditable));
    }

    [RelayCommand]
    private async Task SavePermissionAsync()
    {
        if (string.IsNullOrWhiteSpace(PermCode) && SelectedPermission is null)
        {
            StatusMessage = "Code permission requis.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PermDisplayName) || string.IsNullOrWhiteSpace(PermBusinessDescription))
        {
            StatusMessage = "DisplayName et description métier sont obligatoires.";
            return;
        }

        if (SelectedPermission is not null && ProtectedPermissionCodes.Contains(SelectedPermission.Code) && !PermIsActive)
        {
            StatusMessage = $"La permission « {SelectedPermission.Code} » ne peut pas être désactivée.";
            PermIsActive = true;
            return;
        }

        IsBusy = true;
        try
        {
            var request = new UpsertSecurityPermissionRequest(
                SelectedPermission?.Code ?? PermCode.Trim(),
                PermDisplayName.Trim(),
                string.IsNullOrWhiteSpace(PermModule) ? "GENERAL" : PermModule.Trim(),
                PermBusinessDescription.Trim(),
                string.IsNullOrWhiteSpace(PermHelpText) ? null : PermHelpText.Trim(),
                PermIsActive,
                PermSecurityActionId,
                PermissionAction.Read);

            if (SelectedPermission is null)
            {
                await _catalog.CreatePermissionAsync(request);
                StatusMessage = "Permission créée.";
            }
            else
            {
                await _catalog.UpdatePermissionAsync(SelectedPermission.Id, request with { Code = SelectedPermission.Code });
                StatusMessage = "Permission mise à jour.";
            }

            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddDependencyAsync()
    {
        if (DepPermissionId is null || DepRequiresPermissionId is null)
        {
            StatusMessage = "Sélectionnez la permission et le prérequis.";
            return;
        }

        if (DepPermissionId == DepRequiresPermissionId)
        {
            StatusMessage = "Une permission ne peut pas dépendre d'elle-même.";
            return;
        }

        IsBusy = true;
        try
        {
            await _catalog.AddDependencyAsync(new CreatePermissionDependencyRequest(
                DepPermissionId.Value,
                DepRequiresPermissionId.Value));
            StatusMessage = "Dépendance ajoutée (validation cycles côté serveur).";
            DepPermissionId = null;
            DepRequiresPermissionId = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveDependencyAsync()
    {
        if (SelectedDependency is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _catalog.RemoveDependencyAsync(SelectedDependency.Id);
            StatusMessage = "Dépendance supprimée.";
            SelectedDependency = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IEnumerable<CatalogTreeNodeViewModel> BuildTree(CatalogTreeDto tree)
    {
        foreach (var module in tree.Modules.OrderBy(m => m.SortOrder))
        {
            var moduleNode = new CatalogTreeNodeViewModel(
                CatalogNodeKind.Module,
                module.Id,
                module.Code,
                module.Name,
                null,
                module.SortOrder,
                module.IsActive,
                children: module.Functions.OrderBy(f => f.SortOrder).Select(function =>
                {
                    var functionNode = new CatalogTreeNodeViewModel(
                        CatalogNodeKind.Function,
                        function.Id,
                        function.Code,
                        function.Name,
                        module.Id,
                        function.SortOrder,
                        function.IsActive,
                        children: function.Pages.OrderBy(p => p.SortOrder).Select(page =>
                        {
                            var pageNode = new CatalogTreeNodeViewModel(
                                CatalogNodeKind.Page,
                                page.Id,
                                page.Code,
                                page.Name,
                                function.Id,
                                page.SortOrder,
                                page.IsActive,
                                desktopViewKey: page.DesktopViewKey,
                                requiredPermissionCode: page.RequiredPermissionCode,
                                children: page.Actions.OrderBy(a => a.SortOrder).Select(action =>
                                    new CatalogTreeNodeViewModel(
                                        CatalogNodeKind.Action,
                                        action.Id,
                                        action.Code,
                                        action.Name,
                                        page.Id,
                                        action.SortOrder,
                                        action.IsActive)).ToList());
                            return pageNode;
                        }).ToList());
                    return functionNode;
                }).ToList());

            yield return moduleNode;
        }
    }
}
