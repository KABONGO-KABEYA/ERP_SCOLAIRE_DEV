namespace SchoolManagement.Application.Security;

using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Domain.Enums;

public interface ISecurityAuditService
{
    Task WriteAsync(
        string actionType,
        string summary,
        Guid? schoolId = null,
        Guid? actorUserId = null,
        string? actorUserName = null,
        SecurityAuditActorKind actorKind = SecurityAuditActorKind.SchoolAdmin,
        string? targetEntityType = null,
        Guid? targetEntityId = null,
        string? targetUserName = null,
        string? oldValuesJson = null,
        string? newValuesJson = null,
        string? ipAddress = null,
        string? userAgent = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityAuditLogDto>> QueryAsync(
        SecurityAuditQuery query,
        CancellationToken cancellationToken = default);

    Task<SecurityAuditLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISecurityUserAdminService
{
    Task<IReadOnlyList<SecurityUserDto>> GetUsersAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityPersonnelCandidateDto>> SearchPersonnelCandidatesAsync(
        Guid schoolId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<SecurityUserDto> CreateAsync(
        Guid schoolId,
        CreateSecurityUserRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<SecurityUserDto> UpdateAsync(
        Guid schoolId,
        Guid userId,
        UpdateSecurityUserRequest request,
        Guid? actorUserId,
        string? actorUserName,
        bool actorIsPlatformSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<SecurityUserDto> SetRolesAsync(
        Guid schoolId,
        Guid userId,
        SetSecurityUserRolesRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        Guid schoolId,
        Guid userId,
        ResetPasswordRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<EffectivePermissionExplanationDto> GetEffectivePermissionsAsync(
        Guid schoolId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface ISecurityRoleAdminService
{
    Task<IReadOnlyList<SecurityRoleDto>> GetRolesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<SecurityRoleDto> CreateAsync(
        Guid schoolId,
        CreateSecurityRoleRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<SecurityRoleDto> UpdateAsync(
        Guid schoolId,
        Guid roleId,
        UpdateSecurityRoleRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid schoolId,
        Guid roleId,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<RolePermissionsDto> GetPermissionsAsync(Guid schoolId, Guid roleId, CancellationToken cancellationToken = default);

    Task<RolePermissionsDto> SetPermissionsAsync(
        Guid schoolId,
        Guid roleId,
        SetRolePermissionsRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionCatalogItemDto>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default);
}

public interface ISecurityExceptionAdminService
{
    Task<IReadOnlyList<SecurityExceptionDto>> GetAsync(
        Guid schoolId,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<SecurityExceptionDto> CreateAsync(
        Guid schoolId,
        CreateSecurityExceptionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<SecurityExceptionDto> UpdateAsync(
        Guid schoolId,
        Guid exceptionId,
        UpdateSecurityExceptionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<SecurityExceptionDto> CloseAsync(
        Guid schoolId,
        Guid exceptionId,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);
}

public interface ISecurityCatalogAdminService
{
    Task<CatalogTreeDto> GetTreeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityModuleDto>> GetModulesAsync(CancellationToken cancellationToken = default);

    Task<SecurityModuleDto> UpsertModuleAsync(
        Guid? id,
        UpsertSecurityModuleRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityFunctionDto>> GetFunctionsAsync(Guid? moduleId, CancellationToken cancellationToken = default);

    Task<SecurityFunctionDto> UpsertFunctionAsync(
        Guid? id,
        UpsertSecurityFunctionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityPageDto>> GetPagesAsync(Guid? functionId, CancellationToken cancellationToken = default);

    Task<SecurityPageDto> UpsertPageAsync(
        Guid? id,
        UpsertSecurityPageRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityActionDto>> GetActionsAsync(Guid? pageId, CancellationToken cancellationToken = default);

    Task<SecurityActionDto> UpsertActionAsync(
        Guid? id,
        UpsertSecurityActionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityPermissionAdminDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    Task<SecurityPermissionAdminDto> UpsertPermissionAsync(
        Guid? id,
        UpsertSecurityPermissionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionDependencyDto>> GetDependenciesAsync(CancellationToken cancellationToken = default);

    Task<PermissionDependencyDto> AddDependencyAsync(
        CreatePermissionDependencyRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);

    Task RemoveDependencyAsync(
        Guid dependencyId,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default);
}
