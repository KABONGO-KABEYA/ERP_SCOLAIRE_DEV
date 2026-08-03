using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Setup.DTOs;
using SchoolManagement.Application.Setup.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route(ApiRoutes.Setup)]
public sealed class SetupController : ControllerBase
{
    private readonly IInitialSetupService _setupService;

    public SetupController(IInitialSetupService setupService)
    {
        _setupService = setupService;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<InitialSetupStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _setupService.GetStatusAsync(cancellationToken);
        return Ok(ApiResponse<InitialSetupStatusDto>.Ok(status));
    }

    [HttpPost("complete")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CompleteInitialSetupResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(
        [FromBody] CompleteInitialSetupRequest request,
        CancellationToken cancellationToken)
    {
        var status = await _setupService.GetStatusAsync(cancellationToken);
        if (!status.NeedsSetup)
        {
            return Conflict(ApiResponse<object>.Fail("La configuration initiale est déjà terminée."));
        }

        var result = await _setupService.CompleteAsync(request, cancellationToken);
        return Ok(ApiResponse<CompleteInitialSetupResultDto>.Ok(result, "Établissement configuré."));
    }
}
