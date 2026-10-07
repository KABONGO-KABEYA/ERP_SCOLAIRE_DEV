using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Hosting;
using SchoolManagement.Application.ServerIdentity;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// Health ultra-léger pour la découverte locale (mDNS / scan / last-IP).
/// Aucune authentification ; identité servie depuis le snapshot au démarrage (pas de SQL par requête).
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class LocalDiscoveryHealthController : ControllerBase
{
    private readonly IServerIdentityProvider _identity;
    private readonly StartupReadiness _readiness;

    public LocalDiscoveryHealthController(IServerIdentityProvider identity, StartupReadiness readiness)
    {
        _identity = identity;
        _readiness = readiness;
    }

    [HttpGet]
    [Produces("application/json")]
    public IActionResult Get()
    {
        if (!_readiness.IsReady)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "starting" });

        var id = _identity.Current;
        var schoolDisplay = string.IsNullOrWhiteSpace(id.SchoolName) ? "École" : id.SchoolName;

        object? licenseJson = id.LicenseId.HasValue ? id.LicenseId.Value.ToString("D") : null;

        return Ok(new
        {
            status = "ok",
            server = id.ServerRole,
            school = schoolDisplay,
            time = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            version = id.SoftwareVersion,
            apiVersion = id.ApiVersion,
            protocolVersion = id.ProtocolVersion,
            schemaVersion = id.SchemaVersion,
            identity = new
            {
                serverInstanceId = id.ServerInstanceId.ToString("D"),
                schoolId = id.SchoolId?.ToString("D"),
                schoolName = schoolDisplay,
                licenseId = licenseJson,
                publicKeyFingerprint = id.PublicKeyFingerprint,
                keyVersion = id.KeyVersion
            },
            serverSignature = (string?)null
        });
    }
}
