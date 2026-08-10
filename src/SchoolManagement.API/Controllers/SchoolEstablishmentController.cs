using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.SchoolEstablishment;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/school/establishment")]
public sealed class SchoolEstablishmentController : ControllerBase
{
    private readonly ISchoolEstablishmentService _establishment;
    private readonly ICurrentUserService _currentUser;

    public SchoolEstablishmentController(
        ISchoolEstablishmentService establishment,
        ICurrentUserService currentUser)
    {
        _establishment = establishment;
        _currentUser = currentUser;
    }

    /// <summary>QR / JWT établissement courant (jamais le secret brut).</summary>
    [HttpGet("qr")]
    [HttpPost("qr")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<SchoolEstablishmentQrDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQr(CancellationToken cancellationToken)
    {
        if (!_currentUser.SchoolId.HasValue)
        {
            return Unauthorized();
        }

        var qr = await _establishment.GetCurrentQrAsync(_currentUser.SchoolId.Value, cancellationToken);
        return Ok(ApiResponse<SchoolEstablishmentQrDto>.Ok(qr));
    }

    /// <summary>Régénère le credential (révoque l'ancien) + publication Bootstrap.</summary>
    [HttpPost("rotate")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<SchoolEstablishmentQrDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Rotate(
        [FromBody] RotateEstablishmentRequest? request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.SchoolId.HasValue)
        {
            return Unauthorized();
        }

        var qr = await _establishment.RotateAsync(
            _currentUser.SchoolId.Value,
            _currentUser.UserId,
            request?.Reason,
            cancellationToken);
        return Ok(ApiResponse<SchoolEstablishmentQrDto>.Ok(qr));
    }

    /// <summary>Retry publication registre Bootstrap (école locale déjà créée).</summary>
    [HttpPost("bootstrap-sync/retry")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<BootstrapSyncRetryResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RetryBootstrapSync(CancellationToken cancellationToken)
    {
        if (!_currentUser.SchoolId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _establishment.RetryBootstrapSyncAsync(
            _currentUser.SchoolId.Value,
            cancellationToken);
        return Ok(ApiResponse<BootstrapSyncRetryResult>.Ok(result));
    }
}

public sealed class RotateEstablishmentRequest
{
    public string? Reason { get; set; }
}
