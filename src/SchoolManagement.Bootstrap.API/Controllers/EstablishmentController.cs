using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Bootstrap.API.Establishment;

namespace SchoolManagement.Bootstrap.API.Controllers;

[ApiController]
[Route("establishment")]
public sealed class EstablishmentController : ControllerBase
{
    private readonly EstablishmentService _service;

    public EstablishmentController(EstablishmentService service)
    {
        _service = service;
    }

    [HttpPost("start")]
    [ProducesResponseType(typeof(EstablishmentSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(
        [FromBody] EstablishmentStartRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _service.StartAsync(request, cancellationToken);
            return Ok(session);
        }
        catch (EstablishmentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(
        [FromBody] EstablishmentCompleteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var binding = await _service.CompleteAsync(request, cancellationToken);
            return Ok(binding);
        }
        catch (EstablishmentException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }
}
