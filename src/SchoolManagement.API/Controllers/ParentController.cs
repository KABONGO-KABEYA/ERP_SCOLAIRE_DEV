using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Parent.DTOs;
using SchoolManagement.Application.Parent.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/parent")]
public class ParentController : ControllerBase
{
    private readonly IParentService _parentService;
    private readonly IUserAccountRepository _userRepository;
    private readonly ICurrentUserService _currentUser;

    public ParentController(
        IParentService parentService,
        IUserAccountRepository userRepository,
        ICurrentUserService currentUser)
    {
        _parentService = parentService;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    [HttpGet("children")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentChildDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildren(CancellationToken cancellationToken)
    {
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var children = await _parentService.GetMyChildrenAsync(guardianId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentChildDto>>.Ok(children));
    }

    [HttpGet("children/{studentId:guid}/payments")]
    [Authorize(Policy = Permissions.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentPaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildPayments(Guid studentId, CancellationToken cancellationToken)
    {
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var payments = await _parentService.GetChildPaymentsAsync(guardianId, studentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentPaymentDto>>.Ok(payments));
    }

    [HttpGet("children/{studentId:guid}/bulletins")]
    [Authorize(Policy = Permissions.GradesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentBulletinSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildBulletins(Guid studentId, CancellationToken cancellationToken)
    {
        var guardianId = await ResolveGuardianIdAsync(cancellationToken);
        var bulletins = await _parentService.GetChildBulletinsAsync(guardianId, studentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentBulletinSummaryDto>>.Ok(bulletins));
    }

    private async Task<Guid> ResolveGuardianIdAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException();

        if (user.GuardianId is null)
        {
            throw new UnauthorizedAccessException("Ce compte n'est pas lié à un tuteur.");
        }

        return user.GuardianId.Value;
    }
}
