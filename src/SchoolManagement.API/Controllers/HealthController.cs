using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SchoolManagement.API.Hosting;
using SchoolManagement.API.Options;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route($"{ApiRoutes.Base}/[controller]")]
public class HealthController : ControllerBase
{
    private readonly DeploymentOptions _deployment;
    private readonly StartupReadiness _readiness;

    public HealthController(IOptions<DeploymentOptions> deployment, StartupReadiness readiness)
    {
        _deployment = deployment.Value;
        _readiness = readiness;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Get()
    {
        if (!_readiness.IsReady)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail(
                "Démarrage en cours (schéma / seed).",
                ["starting"]));
        }

        var role = string.IsNullOrWhiteSpace(_deployment.Role) ? "Local" : _deployment.Role;
        return Ok(ApiResponse<object>.Ok(new
        {
            Status = "Healthy",
            Application = AppConstants.ApplicationName,
            Version = "1.0.0",
            Timestamp = DateTime.UtcNow,
            DeploymentRole = role,
            ReadOnly = _deployment.IsCloudReadOnly
        }));
    }
}
