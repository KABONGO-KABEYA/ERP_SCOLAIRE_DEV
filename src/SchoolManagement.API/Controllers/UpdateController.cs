using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Updates.DTOs;
using SchoolManagement.Application.Updates.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route($"{ApiRoutes.Base}/update")]
public sealed class UpdateController : ControllerBase
{
    private readonly IAppUpdateService _updateService;

    public UpdateController(IAppUpdateService updateService)
    {
        _updateService = updateService;
    }

    /// <summary>Vérifie la dernière version publiée (Desktop / Mobile).</summary>
    [HttpGet("check")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<UpdateCheckResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Check(
        [FromQuery] string platform = "desktop",
        [FromQuery] string? currentVersion = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _updateService.GetLatestAsync(platform, currentVersion, cancellationToken);
        if (result is null)
        {
            return NoContent();
        }

        return Ok(ApiResponse<UpdateCheckResponseDto>.Ok(result));
    }

    [HttpGet("versions")]
    [Authorize(Policy = Permissions.UpdatesManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ApplicationVersionAdminDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListVersions(CancellationToken cancellationToken)
    {
        var items = await _updateService.ListVersionsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ApplicationVersionAdminDto>>.Ok(items));
    }

    [HttpPost("versions")]
    [Authorize(Policy = Permissions.UpdatesManage)]
    [ProducesResponseType(typeof(ApiResponse<ApplicationVersionAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Publish(
        [FromBody] PublishApplicationVersionRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _updateService.PublishAsync(request, cancellationToken);
        return Ok(ApiResponse<ApplicationVersionAdminDto>.Ok(created, "Version publiée."));
    }

    [HttpPut("versions/{id:guid}/active")]
    [Authorize(Policy = Permissions.UpdatesManage)]
    [ProducesResponseType(typeof(ApiResponse<ApplicationVersionAdminDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromQuery] bool active = true,
        [FromQuery] bool deactivateOthers = true,
        CancellationToken cancellationToken = default)
    {
        var updated = await _updateService.SetActiveAsync(id, active, deactivateOthers, cancellationToken);
        return Ok(ApiResponse<ApplicationVersionAdminDto>.Ok(updated));
    }
}
