using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Bootstrap.API.Contracts;
using SchoolManagement.Bootstrap.API.Security;
using SchoolManagement.Bootstrap.API.Services;

namespace SchoolManagement.Bootstrap.API.Controllers;

[ApiController]
[Route("api/v1/agent")]
public sealed class AgentController : ControllerBase
{
    private readonly IUpdateAgentCredentialService _agents;
    private readonly IUpdateReleaseCatalog _catalog;

    public AgentController(IUpdateAgentCredentialService agents, IUpdateReleaseCatalog catalog)
    {
        _agents = agents;
        _catalog = catalog;
    }

    [HttpPost("credentials")]
    [RequireAgentProvisionKey]
    [ProducesResponseType(typeof(UpdateAgentCredentialSecretResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUpdateAgentCredentialRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _agents.CreateAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (AgentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpGet("credentials")]
    [RequireAgentProvisionKey]
    [ProducesResponseType(typeof(IReadOnlyList<UpdateAgentCredentialListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> List(
        [FromQuery] Guid schoolId,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = await _agents.ListAsync(schoolId, cancellationToken);
            return Ok(items);
        }
        catch (AgentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("credentials/{id:guid}/rotate")]
    [RequireAgentProvisionKey]
    [ProducesResponseType(typeof(UpdateAgentCredentialSecretResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Rotate(
        Guid id,
        [FromBody] UpdateAgentRevokeRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var rotated = await _agents.RotateAsync(id, request?.Reason, cancellationToken);
            return Ok(rotated);
        }
        catch (AgentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("credentials/{id:guid}/revoke")]
    [RequireAgentProvisionKey]
    [ProducesResponseType(typeof(UpdateAgentCredentialListItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Revoke(
        Guid id,
        [FromBody] UpdateAgentRevokeRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var revoked = await _agents.RevokeAsync(id, request?.Reason, cancellationToken);
            return Ok(revoked);
        }
        catch (AgentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("token")]
    [ProducesResponseType(typeof(UpdateAgentTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Token(
        [FromBody] UpdateAgentTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var issued = await _agents.IssueTokenAsync(request, cancellationToken);
            return Ok(issued);
        }
        catch (AgentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check catalogue authentifié. Le SchoolId vient exclusivement du JWT / credential
    /// (<c>sub</c>). Aucun query <c>schoolId</c> n'est utilisé comme identité ni comme filtre.
    /// </summary>
    [HttpGet("releases/check")]
    [RequireUpdateAgentJwt]
    [ProducesResponseType(typeof(UpdateAgentReleaseCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckReleases(
        [FromQuery] string? channel,
        [FromQuery] string? currentVersion,
        CancellationToken cancellationToken)
    {
        _ = currentVersion;
        if (HttpContext.Items[UpdateAgentAuthContext.HttpContextItemKey] is not UpdateAgentAuthContext auth)
        {
            return Unauthorized(new { error = "Jeton agent invalide." });
        }

        try
        {
            var manifest = await _catalog.CheckForAgentAsync(channel, auth.SchoolId, cancellationToken);
            if (manifest is null)
            {
                return NoContent();
            }

            return Ok(manifest);
        }
        catch (CatalogException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }
}
