using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.CloudSync.DTOs;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.CloudSync)]
public sealed class CloudSyncController : ControllerBase
{
    private readonly ICloudSyncFacade _facade;

    public CloudSyncController(ICloudSyncFacade facade)
    {
        _facade = facade;
    }

    [HttpGet("status")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<CloudSyncStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _facade.GetStatusAsync(cancellationToken);
        return Ok(ApiResponse<CloudSyncStatusDto>.Ok(status));
    }

    [HttpPost("synchronize")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<CloudSyncRunResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SynchronizeNow(
        [FromQuery] bool criticalOnly = false,
        [FromQuery] bool requeueDeadLetters = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _facade.SynchronizeNowAsync(criticalOnly, requeueDeadLetters, cancellationToken);
        return Ok(ApiResponse<CloudSyncRunResultDto>.Ok(result, result.Message));
    }
}
