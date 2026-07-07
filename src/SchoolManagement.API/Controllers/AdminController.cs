using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Admin.DTOs;
using SchoolManagement.Application.Admin.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Admin}")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ICurrentUserService _currentUser;

    public AdminController(IAdminService adminService, ICurrentUserService currentUser)
    {
        _adminService = adminService;
        _currentUser = currentUser;
    }

    [HttpGet("users")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserAccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var users = await _adminService.GetUsersAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<UserAccountDto>>.Ok(users));
    }

    [HttpGet("roles")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var roles = await _adminService.GetRolesAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RoleDto>>.Ok(roles));
    }

    [HttpPost("users")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<UserAccountDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var user = await _adminService.CreateUserAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<UserAccountDto>.Ok(user, "Utilisateur créé."));
    }

    [HttpPut("users/{id:guid}")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<UserAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var user = await _adminService.UpdateUserAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<UserAccountDto>.Ok(user, "Utilisateur mis à jour."));
    }

    [HttpPut("users/{id:guid}/roles")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<UserAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetUserRoles(Guid id, [FromBody] SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var user = await _adminService.SetUserRolesAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<UserAccountDto>.Ok(user, "Rôles mis à jour."));
    }
}
