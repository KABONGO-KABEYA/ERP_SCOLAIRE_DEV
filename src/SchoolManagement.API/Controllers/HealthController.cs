using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Route($"{ApiRoutes.Base}/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            Status = "Healthy",
            Application = AppConstants.ApplicationName,
            Version = "1.0.0",
            Timestamp = DateTime.UtcNow
        }));
    }
}
