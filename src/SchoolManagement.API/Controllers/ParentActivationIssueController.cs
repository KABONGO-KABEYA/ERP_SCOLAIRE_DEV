using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.ParentActivation;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/parent/activation")]
public sealed class ParentActivationIssueController : ControllerBase
{
    private readonly IParentActivationService _activation;
    private readonly ICurrentUserService _currentUser;

    public ParentActivationIssueController(
        IParentActivationService activation,
        ICurrentUserService currentUser)
    {
        _activation = activation;
        _currentUser = currentUser;
    }

    [HttpPost("issue")]
    [Authorize(Policy = Permissions.AdminFull)]
    [ProducesResponseType(typeof(ApiResponse<IssueParentActivationTokenResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Issue(
        [FromBody] IssueParentActivationTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.SchoolId.HasValue || !_currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _activation.IssueTokenAsync(
            _currentUser.SchoolId.Value,
            _currentUser.UserId.Value,
            request,
            cancellationToken);

        return Ok(ApiResponse<IssueParentActivationTokenResponse>.Ok(result));
    }
}
