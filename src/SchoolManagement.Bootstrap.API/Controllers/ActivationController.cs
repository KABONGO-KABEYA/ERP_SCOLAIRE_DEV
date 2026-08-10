using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.ParentActivation;
using SchoolManagement.Bootstrap.API.Services;

namespace SchoolManagement.Bootstrap.API.Controllers;

[ApiController]
[Route("activation")]
public sealed class ActivationController : ControllerBase
{
    private readonly BootstrapOrchestrator _orchestrator;

    public ActivationController(BootstrapOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] ActivationStartRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Phase 7 — garde-fou Application partagé (token_type / typ).
            ParentActivationTokenTypeGuard.EnsureNotSchoolEstablishmentToken(request?.Token);
            var session = await _orchestrator.StartAsync(request!, cancellationToken);
            return Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] ActivationCompleteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var binding = await _orchestrator.CompleteAsync(request, cancellationToken);
            return Ok(binding);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
