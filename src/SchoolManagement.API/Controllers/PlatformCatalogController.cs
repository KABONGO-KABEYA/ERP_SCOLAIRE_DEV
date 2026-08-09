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
[Route($"{ApiRoutes.Base}/platform")]
public sealed class PlatformCatalogController : ControllerBase
{
    private readonly ISecurityCatalogAdminService _catalog;
    private readonly ISecurityAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public PlatformCatalogController(
        ISecurityCatalogAdminService catalog,
        ISecurityAuditService audit,
        ICurrentUserService currentUser)
    {
        _catalog = catalog;
        _audit = audit;
        _currentUser = currentUser;
    }

    private void EnsurePlatformSuperAdmin()
    {
        var claim = User.HasClaim(ClaimTypesCustom.PlatformSuperAdmin, "true");
        var perm = _currentUser.HasPermission(Permissions.PlatformSuperAdmin)
                   || _currentUser.HasPermission(Permissions.PlatformCatalogManage);
        if (!claim && !perm)
        {
            throw new UnauthorizedAccessException("Accès Super Admin plateforme requis.");
        }
    }

    private static Guid? TryGetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    [HttpGet("catalog/tree")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    [ProducesResponseType(typeof(ApiResponse<CatalogTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.GetTreeAsync(cancellationToken);
        return Ok(ApiResponse<CatalogTreeDto>.Ok(result));
    }

    [HttpGet("catalog/modules")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> GetModules(CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        return Ok(ApiResponse<IReadOnlyList<SecurityModuleDto>>.Ok(await _catalog.GetModulesAsync(cancellationToken)));
    }

    [HttpPost("catalog/modules")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> CreateModule([FromBody] UpsertSecurityModuleRequest request, CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertModuleAsync(null, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Created(string.Empty, ApiResponse<SecurityModuleDto>.Ok(result));
    }

    [HttpPut("catalog/modules/{id:guid}")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> UpdateModule(
        Guid id,
        [FromBody] UpsertSecurityModuleRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertModuleAsync(id, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Ok(ApiResponse<SecurityModuleDto>.Ok(result));
    }

    [HttpGet("catalog/functions")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> GetFunctions([FromQuery] Guid? moduleId, CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        return Ok(ApiResponse<IReadOnlyList<SecurityFunctionDto>>.Ok(
            await _catalog.GetFunctionsAsync(moduleId, cancellationToken)));
    }

    [HttpPost("catalog/functions")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> CreateFunction([FromBody] UpsertSecurityFunctionRequest request, CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertFunctionAsync(null, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Created(string.Empty, ApiResponse<SecurityFunctionDto>.Ok(result));
    }

    [HttpPut("catalog/functions/{id:guid}")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> UpdateFunction(
        Guid id,
        [FromBody] UpsertSecurityFunctionRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertFunctionAsync(id, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Ok(ApiResponse<SecurityFunctionDto>.Ok(result));
    }

    [HttpGet("catalog/pages")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> GetPages([FromQuery] Guid? functionId, CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        return Ok(ApiResponse<IReadOnlyList<SecurityPageDto>>.Ok(await _catalog.GetPagesAsync(functionId, cancellationToken)));
    }

    [HttpPost("catalog/pages")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> CreatePage([FromBody] UpsertSecurityPageRequest request, CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertPageAsync(null, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Created(string.Empty, ApiResponse<SecurityPageDto>.Ok(result));
    }

    [HttpPut("catalog/pages/{id:guid}")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> UpdatePage(
        Guid id,
        [FromBody] UpsertSecurityPageRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertPageAsync(id, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Ok(ApiResponse<SecurityPageDto>.Ok(result));
    }

    [HttpGet("catalog/actions")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> GetActions([FromQuery] Guid? pageId, CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        return Ok(ApiResponse<IReadOnlyList<SecurityActionDto>>.Ok(await _catalog.GetActionsAsync(pageId, cancellationToken)));
    }

    [HttpPost("catalog/actions")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> CreateAction([FromBody] UpsertSecurityActionRequest request, CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertActionAsync(null, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Created(string.Empty, ApiResponse<SecurityActionDto>.Ok(result));
    }

    [HttpPut("catalog/actions/{id:guid}")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> UpdateAction(
        Guid id,
        [FromBody] UpsertSecurityActionRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertActionAsync(id, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Ok(ApiResponse<SecurityActionDto>.Ok(result));
    }

    [HttpGet("catalog/permissions")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        return Ok(ApiResponse<IReadOnlyList<SecurityPermissionAdminDto>>.Ok(
            await _catalog.GetPermissionsAsync(cancellationToken)));
    }

    [HttpPost("catalog/permissions")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> CreatePermission(
        [FromBody] UpsertSecurityPermissionRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertPermissionAsync(null, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Created(string.Empty, ApiResponse<SecurityPermissionAdminDto>.Ok(result));
    }

    [HttpPut("catalog/permissions/{id:guid}")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> UpdatePermission(
        Guid id,
        [FromBody] UpsertSecurityPermissionRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.UpsertPermissionAsync(id, request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Ok(ApiResponse<SecurityPermissionAdminDto>.Ok(result));
    }

    [HttpGet("catalog/dependencies")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> GetDependencies(CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        return Ok(ApiResponse<IReadOnlyList<PermissionDependencyDto>>.Ok(
            await _catalog.GetDependenciesAsync(cancellationToken)));
    }

    [HttpPost("catalog/dependencies")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> AddDependency(
        [FromBody] CreatePermissionDependencyRequest request,
        CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        var result = await _catalog.AddDependencyAsync(request, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Created(string.Empty, ApiResponse<PermissionDependencyDto>.Ok(result));
    }

    [HttpDelete("catalog/dependencies/{id:guid}")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> RemoveDependency(Guid id, CancellationToken cancellationToken)
    {
        EnsurePlatformSuperAdmin();
        await _catalog.RemoveDependencyAsync(id, TryGetUserId(User), _currentUser.UserName, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Dépendance supprimée."));
    }

    [HttpGet("audit")]
    [Authorize(Policy = Permissions.PlatformCatalogManage)]
    public async Task<IActionResult> QueryPlatformAudit(
        [FromQuery] Guid? schoolId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? actionType,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        EnsurePlatformSuperAdmin();
        var result = await _audit.QueryAsync(
            new SecurityAuditQuery(fromUtc, toUtc, actionType, SchoolId: schoolId, Skip: skip, Take: take),
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SecurityAuditLogDto>>.Ok(result));
    }
}
