using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SchoolManagement.Bootstrap.API.Contracts;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Security;
using SchoolManagement.Bootstrap.API.Services;

namespace SchoolManagement.Bootstrap.API.Controllers;

[ApiController]
[Route("api/v1/releases")]
public sealed class ReleasesController : ControllerBase
{
    private readonly IUpdateReleaseCatalog _catalog;
    private readonly BootstrapOptions _options;

    public ReleasesController(IUpdateReleaseCatalog catalog, IOptions<BootstrapOptions> options)
    {
        _catalog = catalog;
        _options = options.Value;
    }

    [HttpPost]
    [RequireReleasePublishKey]
    [ProducesResponseType(typeof(UpdateReleaseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUpdateReleaseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _catalog.CreateDraftAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.ReleaseId }, created);
        }
        catch (CatalogException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Manifeste de la meilleure release Published du channel.
    /// <paramref name="schoolId"/> est un filtre de ciblage uniquement — pas une preuve d'identité
    /// ni une autorisation de déploiement (identité école = Lot 2 / Update Agent).
    /// Accessible sans clé de publication. Ne retourne jamais Draft ni Blocked.
    /// <paramref name="currentVersion"/> est ignoré (pas de moteur de compatibilité dans ce lot).
    /// </summary>
    [HttpGet("check")]
    [ProducesResponseType(typeof(UpdateReleaseCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Check(
        [FromQuery] string? channel,
        [FromQuery] Guid? schoolId,
        [FromQuery] string? currentVersion,
        [FromQuery] string? artifactType,
        CancellationToken cancellationToken)
    {
        _ = currentVersion;
        try
        {
            var manifest = await _catalog.CheckAsync(channel, schoolId, artifactType, cancellationToken);
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UpdateReleaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var hasKey = ReleasePublishKeyAuthorizationFilter.TryGetHeader(
            Request.Headers,
            ReleasePublishKeyAuthorizationFilter.HeaderName,
            out var provided);
        var configured = _options.ReleasePublishApiKey;

        var includeNonPublished = false;
        if (hasKey)
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "Publication catalogue non configurée (Bootstrap:ReleasePublishApiKey)." });
            }

            if (!ReleasePublishKeyAuthorizationFilter.FixedEquals(provided!, configured))
            {
                return Unauthorized(new { error = "Clé de publication catalogue invalide." });
            }

            includeNonPublished = true;
        }

        try
        {
            var release = await _catalog.GetByIdAsync(id, includeNonPublished, cancellationToken);
            if (release is null)
            {
                return NotFound(new { error = "Release introuvable." });
            }

            return Ok(release);
        }
        catch (CatalogException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/status")]
    [RequireReleasePublishKey]
    [ProducesResponseType(typeof(UpdateReleaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] UpdateReleaseStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _catalog.ChangeStatusAsync(id, request, cancellationToken);
            return Ok(updated);
        }
        catch (CatalogException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }
}
