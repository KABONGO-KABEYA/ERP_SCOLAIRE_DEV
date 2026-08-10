using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Bootstrap.API.Contracts;
using SchoolManagement.Bootstrap.API.Filters;
using SchoolManagement.Bootstrap.API.Persistence;

namespace SchoolManagement.Bootstrap.API.Controllers;

[ApiController]
[Route("registry/schools")]
[RequireBootstrapRelayKey]
public sealed class RegistryController : ControllerBase
{
    private readonly IBootstrapSchoolRegistryRepository _registry;

    public RegistryController(IBootstrapSchoolRegistryRepository registry)
    {
        _registry = registry;
    }

    /// <summary>Enregistre ou met à jour une école (+ credential optionnel) — idempotent sur <c>SchoolId</c>.</summary>
    [HttpPost("upsert")]
    [ProducesResponseType(typeof(RegistrySchoolUpsertHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Upsert(
        [FromBody] RegistrySchoolUpsertHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Corps de requête requis." });
        }

        try
        {
            var entry = await _registry.UpsertSchoolAsync(
                new BootstrapSchoolRegistryUpsertRequest
                {
                    SchoolId = request.SchoolId,
                    SchoolName = request.SchoolName,
                    ActivationBaseUrl = request.ActivationBaseUrl,
                    CloudBaseUrl = request.CloudBaseUrl,
                    PublicKeyFingerprint = request.PublicKeyFingerprint,
                    KeyVersion = request.KeyVersion,
                    ServerInstanceId = request.ServerInstanceId,
                    LicenseId = request.LicenseId,
                    Credential = MapCredential(request.Credential),
                },
                cancellationToken);

            var active = await _registry.GetActiveCredentialAsync(entry.SchoolId, cancellationToken);

            return Ok(new RegistrySchoolUpsertHttpResponse
            {
                SchoolId = entry.SchoolId,
                SchoolName = entry.SchoolName,
                IsActive = entry.IsActive,
                UpdatedAtUtc = entry.UpdatedAtUtc,
                ActiveCredentialId = active?.Id,
                ActiveCredentialVersion = active?.CredentialVersion,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Révoque le credential Active et active le nouveau.</summary>
    [HttpPost("{schoolId:guid}/credentials/rotate")]
    [ProducesResponseType(typeof(RegistryCredentialRotateHttpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Rotate(
        Guid schoolId,
        [FromBody] RegistryCredentialRotateHttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Credential is null)
        {
            return BadRequest(new { error = "Credential requis." });
        }

        try
        {
            var (revoked, active) = await _registry.RotateCredentialAsync(
                schoolId,
                MapCredential(request.Credential)!,
                request.Reason,
                cancellationToken);

            return Ok(new RegistryCredentialRotateHttpResponse
            {
                SchoolId = schoolId,
                RevokedCredentialId = revoked.Id,
                RevokedReason = revoked.RevokedReason ?? string.Empty,
                ActiveCredentialId = active.Id,
                ActiveCredentialVersion = active.CredentialVersion,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // École introuvable / aucun Active → 404 métier registre.
            if (ex.Message.Contains("introuvable", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Aucun credential Active", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = ex.Message });
            }

            return BadRequest(new { error = ex.Message });
        }
    }

    private static BootstrapCredentialUpsert? MapCredential(RegistryCredentialHttpBody? body)
    {
        if (body is null)
        {
            return null;
        }

        return new BootstrapCredentialUpsert
        {
            CredentialId = body.CredentialId,
            CredentialVersion = body.CredentialVersion,
            SecretHash = body.SecretHash,
            TokenType = string.IsNullOrWhiteSpace(body.TokenType)
                ? Persistence.Entities.EstablishmentTokenTypes.SchoolEstablishment
                : body.TokenType,
            CreatedBy = body.CreatedBy,
        };
    }
}
