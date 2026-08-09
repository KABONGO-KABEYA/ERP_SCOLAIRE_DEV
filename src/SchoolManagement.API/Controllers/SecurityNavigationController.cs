using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Security;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;
using System.Security.Claims;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Security}")]
public sealed class SecurityNavigationController : ControllerBase
{
    private readonly ISecurityNavigationService _navigationService;

    public SecurityNavigationController(ISecurityNavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    [HttpGet("navigation")]
    [ProducesResponseType(typeof(ApiResponse<NavigationTreeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNavigation(
        [FromQuery] string channel = "Desktop",
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<NavigationChannel>(channel, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            return BadRequest(ApiResponse<object>.Fail("Canal de navigation invalide. Utilisez Desktop, Web ou Mobile."));
        }

        var tree = await _navigationService.GetNavigationAsync(userId, parsed, cancellationToken);
        return Ok(ApiResponse<NavigationTreeDto>.Ok(tree));
    }
}
