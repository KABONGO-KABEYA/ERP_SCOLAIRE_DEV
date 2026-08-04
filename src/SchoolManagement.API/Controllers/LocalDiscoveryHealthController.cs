using Microsoft.AspNetCore.Mvc;
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

    public LocalDiscoveryHealthController(IServerIdentityProvider identity)
    {
        _identity = identity;
    }

    [HttpGet]
    [Produces("application/json")]
    public IActionResult Get()
    {
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
