using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.API.Filters;
using SchoolManagement.Application.ParentActivation;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[AllowAnonymous]
[BootstrapRelayOnly]
[Route("api/v1/activation")]
public sealed class SchoolActivationController : ControllerBase
{
    private readonly IParentActivationService _activation;

    public SchoolActivationController(IParentActivationService activation)
    {
        _activation = activation;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] ActivationStartRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _activation.StartAsync(request, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] ActivationCompleteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var binding = await _activation.CompleteAsync(request, cancellationToken);
            return Ok(binding);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
