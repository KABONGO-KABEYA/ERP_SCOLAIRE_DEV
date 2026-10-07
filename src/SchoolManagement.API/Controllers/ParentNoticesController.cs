using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.ParentNotices.DTOs;
using SchoolManagement.Application.ParentNotices.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.ParentNotices)]
public sealed class ParentNoticesController : ControllerBase
{
    private readonly IParentNoticeService _service;
    private readonly ICurrentUserService _currentUser;

    public ParentNoticesController(IParentNoticeService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.StudentsRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var data = await _service.ListAsync(RequireSchool(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentNoticeDto>>.Ok(data));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.StudentsRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var data = await _service.GetAsync(RequireSchool(), id, cancellationToken);
        return Ok(ApiResponse<ParentNoticeDto>.Ok(data));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.StudentsUpdate)]
    public async Task<IActionResult> Create(SaveParentNoticeRequest request, CancellationToken cancellationToken)
    {
        var data = await _service.CreateAsync(RequireSchool(), request, cancellationToken);
        return Created(string.Empty, ApiResponse<ParentNoticeDto>.Ok(data, "Brouillon enregistré."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.StudentsUpdate)]
    public async Task<IActionResult> Update(Guid id, SaveParentNoticeRequest request, CancellationToken cancellationToken)
    {
        var data = await _service.UpdateAsync(RequireSchool(), id, request, cancellationToken);
        return Ok(ApiResponse<ParentNoticeDto>.Ok(data, "Brouillon enregistré."));
    }

    [HttpPost("{id:guid}/generate")]
    [Authorize(Policy = Permissions.StudentsUpdate)]
    public async Task<IActionResult> Generate(Guid id, GenerateParentNoticeRequest request, CancellationToken cancellationToken)
    {
        var data = await _service.MarkGeneratedAsync(RequireSchool(), id, request, cancellationToken);
        return Ok(ApiResponse<ParentNoticeDto>.Ok(data, "Avis générés."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.StudentsUpdate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(RequireSchool(), id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Avis supprimé."));
    }

    private Guid RequireSchool() => _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
}
