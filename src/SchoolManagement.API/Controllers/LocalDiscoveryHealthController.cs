using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SchoolManagement.API.Options;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// Health ultra-léger pour la découverte locale (mDNS / scan / last-IP).
/// Aucune authentification, aucun accès base de données.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class LocalDiscoveryHealthController : ControllerBase
{
    private readonly DeploymentOptions _deployment;
    private readonly IConfiguration _configuration;

    public LocalDiscoveryHealthController(
        IOptions<DeploymentOptions> deployment,
        IConfiguration configuration)
    {
        _deployment = deployment.Value;
        _configuration = configuration;
    }

    [HttpGet]
    [Produces("application/json")]
    public IActionResult Get()
    {
        var role = string.IsNullOrWhiteSpace(_deployment.Role) ? "Local" : _deployment.Role;
        var isCloud = role.Equals("Cloud", StringComparison.OrdinalIgnoreCase);
        var school = _configuration["School:Name"]
                     ?? _configuration["School:DisplayName"]
                     ?? _configuration["Deployment:SchoolName"]
                     ?? "École";

        return Ok(new
        {
            status = "ok",
            server = isCloud ? "cloud" : "local",
            school,
            version = _configuration["App:Version"] ?? "1.0.0",
            time = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
        });
    }
}
