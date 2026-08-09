using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Security;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Security}")]
public sealed class SecurityAdminController : ControllerBase
{
    private readonly ISecurityUserAdminService _users;
    private readonly ISecurityRoleAdminService _roles;
    private readonly ISecurityExceptionAdminService _exceptions;
    private readonly ISecurityAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public SecurityAdminController(
        ISecurityUserAdminService users,
        ISecurityRoleAdminService roles,
        ISecurityExceptionAdminService exceptions,
        ISecurityAuditService audit,
        ICurrentUserService currentUser)
    {
        _users = users;
        _roles = roles;
        _exceptions = exceptions;
        _audit = audit;
        _currentUser = currentUser;
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();

    private static Guid? TryGetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    // —— Users ——

    [HttpGet("users")]
    [Authorize(Policy = Permissions.SecurityUsersManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SecurityUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var result = await _users.GetUsersAsync(RequireSchoolId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SecurityUserDto>>.Ok(result));
    }

    [HttpGet("users/personnel-candidates")]
    [Authorize(Policy = Permissions.SecurityUsersManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SecurityPersonnelCandidateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPersonnelCandidates(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _users.SearchPersonnelCandidatesAsync(RequireSchoolId(), search, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SecurityPersonnelCandidateDto>>.Ok(result));
    }

    [HttpPost("users")]
    [Authorize(Policy = Permissions.SecurityUsersManage)]
    [ProducesResponseType(typeof(ApiResponse<SecurityUserDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateUser([FromBody] CreateSecurityUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _users.CreateAsync(
            RequireSchoolId(),
            request,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Created(string.Empty, ApiResponse<SecurityUserDto>.Ok(result, "Utilisateur créé."));
    }

    [HttpPut("users/{id:guid}")]
    [Authorize(Policy = Permissions.SecurityUsersManage)]
    [ProducesResponseType(typeof(ApiResponse<SecurityUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUser(
        Guid id,
        [FromBody] UpdateSecurityUserRequest request,
        CancellationToken cancellationToken)
    {
        var isPlatformSuperAdmin = User.HasClaim(ClaimTypesCustom.PlatformSuperAdmin, "true")
                                   || _currentUser.HasPermission(Permissions.PlatformSuperAdmin);
        var result = await _users.UpdateAsync(
            RequireSchoolId(),
            id,
            request,
            TryGetUserId(User),
            _currentUser.UserName,
            isPlatformSuperAdmin,
            cancellationToken);
        return Ok(ApiResponse<SecurityUserDto>.Ok(result, "Utilisateur mis à jour."));
    }

    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Policy = Permissions.SecurityUsersManage)]
    [ProducesResponseType(typeof(ApiResponse<SecurityUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetUserRoles(
        Guid id,
        [FromBody] SetSecurityUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _users.SetRolesAsync(
            RequireSchoolId(),
            id,
            request,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Ok(ApiResponse<SecurityUserDto>.Ok(result, "Rôles mis à jour."));
    }

    [HttpPost("users/{id:guid}/reset-password")]
    [Authorize(Policy = Permissions.SecurityUsersManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _users.ResetPasswordAsync(
            RequireSchoolId(),
            id,
            request,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Mot de passe réinitialisé."));
    }

    [HttpGet("users/{id:guid}/effective-permissions")]
    [Authorize(Policy = Permissions.SecurityUsersManage)]
    [ProducesResponseType(typeof(ApiResponse<EffectivePermissionExplanationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEffectivePermissions(Guid id, CancellationToken cancellationToken)
    {
        var result = await _users.GetEffectivePermissionsAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<EffectivePermissionExplanationDto>.Ok(result));
    }

    // —— Roles ——

    [HttpGet("roles")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SecurityRoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(Permissions.SecurityRolesManage)
            && !_currentUser.HasPermission(Permissions.SecurityUsersManage)
            && !_currentUser.HasPermission(Permissions.AdminFull))
        {
            return Forbid();
        }

        var result = await _roles.GetRolesAsync(RequireSchoolId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SecurityRoleDto>>.Ok(result));
    }

    [HttpPost("roles")]
    [Authorize(Policy = Permissions.SecurityRolesManage)]
    [ProducesResponseType(typeof(ApiResponse<SecurityRoleDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateRole([FromBody] CreateSecurityRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roles.CreateAsync(
            RequireSchoolId(),
            request,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Created(string.Empty, ApiResponse<SecurityRoleDto>.Ok(result, "Rôle créé."));
    }

    [HttpPut("roles/{id:guid}")]
    [Authorize(Policy = Permissions.SecurityRolesManage)]
    [ProducesResponseType(typeof(ApiResponse<SecurityRoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRole(
        Guid id,
        [FromBody] UpdateSecurityRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _roles.UpdateAsync(
            RequireSchoolId(),
            id,
            request,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Ok(ApiResponse<SecurityRoleDto>.Ok(result, "Rôle mis à jour."));
    }

    [HttpDelete("roles/{id:guid}")]
    [Authorize(Policy = Permissions.SecurityRolesManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken cancellationToken)
    {
        await _roles.DeleteAsync(
            RequireSchoolId(),
            id,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Rôle supprimé."));
    }

    [HttpGet("roles/{id:guid}/permissions")]
    [Authorize(Policy = Permissions.SecurityRolesManage)]
    [ProducesResponseType(typeof(ApiResponse<RolePermissionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRolePermissions(Guid id, CancellationToken cancellationToken)
    {
        var result = await _roles.GetPermissionsAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<RolePermissionsDto>.Ok(result));
    }

    [HttpPut("roles/{id:guid}/permissions")]
    [Authorize(Policy = Permissions.SecurityRolesManage)]
    [ProducesResponseType(typeof(ApiResponse<RolePermissionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetRolePermissions(
        Guid id,
        [FromBody] SetRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _roles.SetPermissionsAsync(
            RequireSchoolId(),
            id,
            request,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Ok(ApiResponse<RolePermissionsDto>.Ok(result, "Permissions du rôle mises à jour."));
    }

    [HttpGet("permissions/catalog")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PermissionCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissionCatalog(CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(Permissions.SecurityRolesManage)
            && !_currentUser.HasPermission(Permissions.SecurityExceptionsManage)
            && !_currentUser.HasPermission(Permissions.AdminFull))
        {
            return Forbid();
        }

        var result = await _roles.GetPermissionCatalogAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PermissionCatalogItemDto>>.Ok(result));
    }

    // —— Exceptions ——

    [HttpGet("exceptions")]
    [Authorize(Policy = Permissions.SecurityExceptionsManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SecurityExceptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExceptions([FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        var result = await _exceptions.GetAsync(RequireSchoolId(), userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SecurityExceptionDto>>.Ok(result));
    }

    [HttpPost("exceptions")]
    [Authorize(Policy = Permissions.SecurityExceptionsManage)]
    [ProducesResponseType(typeof(ApiResponse<SecurityExceptionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateException(
        [FromBody] CreateSecurityExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _exceptions.CreateAsync(
            RequireSchoolId(),
            request,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Created(string.Empty, ApiResponse<SecurityExceptionDto>.Ok(result, "Exception créée."));
    }

    [HttpPut("exceptions/{id:guid}")]
    [Authorize(Policy = Permissions.SecurityExceptionsManage)]
    [ProducesResponseType(typeof(ApiResponse<SecurityExceptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateException(
        Guid id,
        [FromBody] UpdateSecurityExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _exceptions.UpdateAsync(
            RequireSchoolId(),
            id,
            request,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Ok(ApiResponse<SecurityExceptionDto>.Ok(result, "Exception mise à jour."));
    }

    [HttpPost("exceptions/{id:guid}/close")]
    [Authorize(Policy = Permissions.SecurityExceptionsManage)]
    [ProducesResponseType(typeof(ApiResponse<SecurityExceptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseException(Guid id, CancellationToken cancellationToken)
    {
        var result = await _exceptions.CloseAsync(
            RequireSchoolId(),
            id,
            TryGetUserId(User),
            _currentUser.UserName,
            cancellationToken);
        return Ok(ApiResponse<SecurityExceptionDto>.Ok(result, "Exception clôturée."));
    }

    // —— Audit ——

    [HttpGet("audit")]
    [Authorize(Policy = Permissions.SecurityAuditRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SecurityAuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAudit(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? actionType,
        [FromQuery] Guid? actorUserId,
        [FromQuery] string? targetUserName,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await _audit.QueryAsync(
            new SecurityAuditQuery(
                fromUtc,
                toUtc,
                actionType,
                actorUserId,
                TargetUserName: targetUserName,
                SchoolId: RequireSchoolId(),
                Skip: skip,
                Take: take),
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SecurityAuditLogDto>>.Ok(result));
    }

    [HttpGet("audit/{id:guid}")]
    [Authorize(Policy = Permissions.SecurityAuditRead)]
    [ProducesResponseType(typeof(ApiResponse<SecurityAuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAudit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _audit.GetByIdAsync(id, cancellationToken);
        if (result is null || result.SchoolId != RequireSchoolId())
        {
            return NotFound(ApiResponse<object>.Fail("Entrée d'audit introuvable."));
        }

        return Ok(ApiResponse<SecurityAuditLogDto>.Ok(result));
    }
}
