using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Mentions.DTOs;
using SchoolManagement.Application.Mentions.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.Mentions)]
public class MentionsController : ControllerBase
{
    private readonly IResultMentionService _service;
    private readonly ICurrentUserService _currentUser;

    public MentionsController(IResultMentionService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.SchoolsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ResultMentionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var list = await _service.GetAllAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ResultMentionDto>>.Ok(list));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ResultMentionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(
        [FromBody] CreateResultMentionRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var created = await _service.CreateAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<ResultMentionDto>.Ok(created, "Mention créée."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ResultMentionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateResultMentionRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var updated = await _service.UpdateAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<ResultMentionDto>.Ok(updated, "Mention mise à jour."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _service.DeleteAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Mention supprimée."));
    }
}
