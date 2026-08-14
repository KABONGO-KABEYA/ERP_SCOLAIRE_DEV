namespace SchoolManagement.Application.Security.DTOs;

using SchoolManagement.Domain.Enums;

/// <summary>Origine d’une permission dans l’aperçu effectif.</summary>
public enum PermissionOriginKind
{
    Role = 1,
    Grant = 2,
    Deny = 3,
    Dependency = 4
}

public sealed record PermissionOriginDetailDto(
    PermissionOriginKind Kind,
    string? RoleCode = null,
    Guid? ExceptionId = null,
    string? SourcePermissionCode = null,
    string? Note = null);

public sealed record PermissionExplanationDto(
    string Code,
    string DisplayName,
    string? HelpText,
    bool IsEffective,
    IReadOnlyList<PermissionOriginDetailDto> Origins);

public sealed record EffectivePermissionExplanationDto(
    IReadOnlyList<string> Roles,
    bool IsPlatformSuperAdmin,
    IReadOnlyList<PermissionExplanationDto> Permissions);

public sealed record SecurityUserDto(
    Guid Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    bool IsActive,
    bool MustChangePassword,
    bool IsPlatformSuperAdmin,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<string> RoleLabels = null!,
    DateTime? LastLoginAt = null,
    /// <summary>Compte parent / tuteur (externe) — opposé aux agents internes.</summary>
    bool IsExternalParent = false);

public sealed record CreateSecurityUserRequest(
    Guid TeacherId,
    string UserName,
    string Password,
    bool MustChangePassword = true);

public sealed record SecurityPersonnelCandidateDto(
    Guid TeacherId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string FullName,
    string? FunctionName,
    string? Email,
    string StatusLabel,
    bool IsActive);

public sealed record UpdateSecurityUserRequest(
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    bool? IsPlatformSuperAdmin = null);

public sealed record SetSecurityUserRolesRequest(IReadOnlyList<Guid> RoleIds);

public sealed record ResetPasswordRequest(string NewPassword, bool MustChangePassword = true);

public sealed record SecurityRoleDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsAssignable,
    int SortOrder,
    bool PermissionsReadOnly,
    int PermissionCount);

public sealed record CreateSecurityRoleRequest(
    string Code,
    string Name,
    string? Description,
    bool IsAssignable = true,
    int SortOrder = 100);

public sealed record UpdateSecurityRoleRequest(
    string Name,
    string? Description,
    bool IsAssignable,
    int SortOrder);

public sealed record PermissionCatalogItemDto(
    Guid Id,
    string Code,
    string DisplayName,
    string? HelpText,
    string Module,
    bool IsActive,
    IReadOnlyList<string> PrerequisiteCodes);

public sealed record RolePermissionsDto(
    Guid RoleId,
    string RoleCode,
    bool PermissionsReadOnly,
    IReadOnlyList<string> PermissionCodes);

public sealed record SetRolePermissionsRequest(IReadOnlyList<string> PermissionCodes);

public sealed record SecurityExceptionDto(
    Guid Id,
    Guid UserId,
    string UserName,
    Guid PermissionId,
    string PermissionCode,
    string PermissionDisplayName,
    PermissionExceptionEffect Effect,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Reason,
    bool IsCurrentlyActive);

public sealed record CreateSecurityExceptionRequest(
    Guid UserId,
    Guid PermissionId,
    PermissionExceptionEffect Effect,
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Reason);

public sealed record UpdateSecurityExceptionRequest(
    DateTime ValidFrom,
    DateTime? ValidTo,
    string? Reason);

public sealed record SecurityAuditLogDto(
    Guid Id,
    DateTime OccurredAtUtc,
    Guid? SchoolId,
    Guid? ActorUserId,
    string ActorUserName,
    SecurityAuditActorKind ActorKind,
    string ActionType,
    string? TargetEntityType,
    Guid? TargetEntityId,
    string? TargetUserName,
    string Summary,
    string? OldValuesJson,
    string? NewValuesJson,
    string? IpAddress,
    Guid? CorrelationId);

public sealed record SecurityAuditQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? ActionType = null,
    Guid? ActorUserId = null,
    Guid? TargetEntityId = null,
    string? TargetUserName = null,
    string? TargetEntityType = null,
    Guid? SchoolId = null,
    int Skip = 0,
    int Take = 100);

public sealed record SecurityModuleDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? Icon,
    int SortOrder,
    bool IsActive);

public sealed record UpsertSecurityModuleRequest(
    string Code,
    string Name,
    string? Description,
    string? Icon,
    int SortOrder,
    bool IsActive = true);

public sealed record SecurityFunctionDto(
    Guid Id,
    Guid ModuleId,
    string Code,
    string Name,
    string? Description,
    string? Icon,
    int SortOrder,
    bool IsActive);

public sealed record UpsertSecurityFunctionRequest(
    Guid ModuleId,
    string Code,
    string Name,
    string? Description,
    string? Icon,
    int SortOrder,
    bool IsActive = true);

public sealed record SecurityPageDto(
    Guid Id,
    Guid FunctionId,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    string? RequiredPermissionCode,
    string? DesktopViewKey,
    string? WebRoute,
    string? MobileScreenKey,
    bool IsAvailableOnDesktop,
    bool IsAvailableOnWeb,
    bool IsAvailableOnMobile);

public sealed record UpsertSecurityPageRequest(
    Guid FunctionId,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    string? RequiredPermissionCode,
    string? DesktopViewKey,
    string? WebRoute,
    string? MobileScreenKey,
    bool IsAvailableOnDesktop = true,
    bool IsAvailableOnWeb = false,
    bool IsAvailableOnMobile = false);

public sealed record SecurityActionDto(
    Guid Id,
    Guid PageId,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    bool IsAvailableOnDesktop,
    bool IsAvailableOnWeb,
    bool IsAvailableOnMobile);

public sealed record UpsertSecurityActionRequest(
    Guid PageId,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive = true,
    bool IsAvailableOnDesktop = true,
    bool IsAvailableOnWeb = false,
    bool IsAvailableOnMobile = false);

public sealed record SecurityPermissionAdminDto(
    Guid Id,
    string Code,
    string DisplayName,
    string Module,
    string Description,
    string? HelpText,
    bool IsActive,
    Guid? SecurityActionId);

public sealed record UpsertSecurityPermissionRequest(
    string Code,
    string DisplayName,
    string Module,
    string Description,
    string? HelpText,
    bool IsActive,
    Guid? SecurityActionId,
    PermissionAction Action = PermissionAction.Read);

public sealed record PermissionDependencyDto(
    Guid Id,
    Guid PermissionId,
    string PermissionCode,
    Guid RequiresPermissionId,
    string RequiresPermissionCode,
    bool IsActive);

public sealed record CreatePermissionDependencyRequest(
    Guid PermissionId,
    Guid RequiresPermissionId);

public sealed record CatalogTreeDto(
    IReadOnlyList<CatalogTreeModuleDto> Modules);

public sealed record CatalogTreeModuleDto(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<CatalogTreeFunctionDto> Functions);

public sealed record CatalogTreeFunctionDto(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<CatalogTreePageDto> Pages);

public sealed record CatalogTreePageDto(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    string? DesktopViewKey,
    string? RequiredPermissionCode,
    IReadOnlyList<CatalogTreeActionDto> Actions);

public sealed record CatalogTreeActionDto(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<string> PermissionCodes);
