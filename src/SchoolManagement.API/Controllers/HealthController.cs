using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SchoolManagement.API.Options;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route($"{ApiRoutes.Base}/[controller]")]
public class HealthController : ControllerBase
{
    private readonly DeploymentOptions _deployment;

    public HealthController(IOptions<DeploymentOptions> deployment)
    {
        _deployment = deployment.Value;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
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
