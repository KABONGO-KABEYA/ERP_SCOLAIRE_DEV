using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Bulletins.DTOs;
using SchoolManagement.Application.Bulletins.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

/// <summary>
/// API Bulletins — endpoints préparés pour le développement suivant.
/// Les résultats académiques seront fournis via <c>IBulletinQueryService</c>
/// (moteur <c>IResultCalculationService</c>) ; aucun calcul côté client.
/// </summary>
[ApiController]
[Authorize]
[Route(ApiRoutes.Bulletins)]
public sealed class BulletinsController : ControllerBase
{
    private readonly IBulletinQueryService _bulletins;
    private readonly ICurrentUserService _currentUser;

    public BulletinsController(IBulletinQueryService bulletins, ICurrentUserService currentUser)
    {
        _bulletins = bulletins;
        _currentUser = currentUser;
    }

    /// <summary>Bulletin individuel (projection moteur — pas encore implémenté).</summary>
    [HttpPost("individual")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IndividualBulletinDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIndividual(
        [FromBody] IndividualBulletinRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var bulletin = await _bulletins.GetIndividualBulletinAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<IndividualBulletinDto>.Ok(bulletin));
    }

    /// <summary>Bulletins de la classe (lot — pas encore implémenté).</summary>
    [HttpPost("class")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<ClassBulletinsBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassBatch(
        [FromBody] ClassBulletinsRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var batch = await _bulletins.GetClassBulletinsAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<ClassBulletinsBatchDto>.Ok(batch));
    }

    /// <summary>Historique des impressions / réimpressions.</summary>
    [HttpGet("print-history")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BulletinPrintHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrintHistory(
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? classRoomId,
        [FromQuery] Guid? studentId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var history = await _bulletins.GetPrintHistoryAsync(
            schoolId, academicYearId, classRoomId, studentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BulletinPrintHistoryDto>>.Ok(history));
    }

    /// <summary>Enregistre une impression ou réimpression (métadonnées).</summary>
    [HttpPost("print-history")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<BulletinPrintHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordPrint(
        [FromBody] RecordBulletinPrintRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var entry = await _bulletins.RecordPrintAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<BulletinPrintHistoryDto>.Ok(entry, "Impression enregistrée."));
    }
}
