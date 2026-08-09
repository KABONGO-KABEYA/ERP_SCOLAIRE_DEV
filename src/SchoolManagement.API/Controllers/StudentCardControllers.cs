using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.StudentCards.DTOs;
using SchoolManagement.Application.StudentCards.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.StudentCards)]
public sealed class StudentCardsController : ControllerBase
{
    private readonly IStudentCardService _service;
    private readonly ICurrentUserService _currentUser;

    public StudentCardsController(IStudentCardService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = Permissions.StudentCardsRead)]
    [ProducesResponseType(typeof(ApiResponse<StudentCardDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard([FromQuery] Guid? academicYearId, CancellationToken cancellationToken)
    {
        var data = await _service.GetDashboardAsync(RequireSchoolId(), academicYearId, cancellationToken);
        return Ok(ApiResponse<StudentCardDashboardDto>.Ok(data));
    }

    [HttpGet("settings")]
    [Authorize(Policy = Permissions.StudentCardsRead)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var item = await _service.GetSettingsAsync(RequireSchoolId(), cancellationToken);
        return Ok(ApiResponse<CardSchoolSettingsDto>.Ok(item));
    }

    [HttpPut("settings")]
    [Authorize(Policy = Permissions.CardTemplatesManage)]
    public async Task<IActionResult> SaveSettings([FromBody] SaveCardSchoolSettingsRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.SaveSettingsAsync(RequireSchoolId(), request, UserId(), cancellationToken);
        return Ok(ApiResponse<CardSchoolSettingsDto>.Ok(item, "Paramètres cartes enregistrés."));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.StudentCardsRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StudentCardListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] StudentCardSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<PagedResult<StudentCardListItemDto>>.Ok(result.ToPagedResult()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.StudentCardsRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item));
    }

    [HttpPost("resolve-qr")]
    [Authorize(Policy = Permissions.StudentCardsRead)]
    public async Task<IActionResult> ResolveQr([FromBody] ResolveCardByQrRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.ResolveByQrAsync(RequireSchoolId(), request, cancellationToken);
        if (item is null)
            return NotFound(ApiResponse<object>.Fail("QR Code inconnu."));
        return Ok(ApiResponse<ResolvedStudentCardDto>.Ok(item));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.StudentCardsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateStudentCardRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.CreateAsync(RequireSchoolId(), request, UserId(), cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item, "Carte créée."));
    }

    [HttpPost("bulk")]
    [Authorize(Policy = Permissions.StudentCardsCreate)]
    public async Task<IActionResult> BulkCreate([FromBody] BulkCreateStudentCardsRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.BulkCreateAsync(RequireSchoolId(), request, UserId(), cancellationToken);
        return Ok(ApiResponse<BulkCreateStudentCardsResult>.Ok(result, result.Summary));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.StudentCardsUpdate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentCardRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.UpdateAsync(RequireSchoolId(), id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item, "Carte mise à jour."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.StudentCardsDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.SoftDeleteAsync(RequireSchoolId(), id, UserId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Carte supprimée (logique)."));
    }

    [HttpPost("print")]
    [Authorize(Policy = Permissions.StudentCardsPrint)]
    public async Task<IActionResult> Print([FromBody] PrintStudentCardsRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.PrintAsync(RequireSchoolId(), request, UserId(), cancellationToken);
        return Ok(ApiResponse<PrintStudentCardsResult>.Ok(result, $"{result.PrintedCount} carte(s) marquée(s) imprimée(s)."));
    }

    [HttpPost("{id:guid}/reprint")]
    [Authorize(Policy = Permissions.StudentCardsPrint)]
    public async Task<IActionResult> Reprint(Guid id, [FromBody] ReprintStudentCardRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.ReprintAsync(RequireSchoolId(), id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item, "Réimpression enregistrée."));
    }

    [HttpPost("{id:guid}/renew")]
    [Authorize(Policy = Permissions.StudentCardsRenew)]
    public async Task<IActionResult> Renew(Guid id, [FromBody] RenewStudentCardRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.RenewAsync(RequireSchoolId(), id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item, "Carte renouvelée."));
    }

    [HttpPost("{id:guid}/lost")]
    [Authorize(Policy = Permissions.StudentCardsDeclareLost)]
    public async Task<IActionResult> DeclareLost(Guid id, [FromBody] DeclareCardIncidentRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.DeclareLostAsync(RequireSchoolId(), id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item, "Carte déclarée perdue."));
    }

    [HttpPost("{id:guid}/stolen")]
    [Authorize(Policy = Permissions.StudentCardsDeclareLost)]
    public async Task<IActionResult> DeclareStolen(Guid id, [FromBody] DeclareCardIncidentRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.DeclareStolenAsync(RequireSchoolId(), id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item, "Carte déclarée volée."));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.StudentCardsUpdate)]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateStudentCardRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.DeactivateAsync(RequireSchoolId(), id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item, "Carte désactivée."));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Permissions.StudentCardsUpdate)]
    public async Task<IActionResult> Activate(Guid id, [FromBody] ActivateStudentCardRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.ActivateAsync(RequireSchoolId(), id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item, "Carte activée."));
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = Permissions.StudentCardsUpdate)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendStudentCardRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.SuspendAsync(RequireSchoolId(), id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<StudentCardDetailDto>.Ok(item, "Carte suspendue."));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();

    private Guid UserId() =>
        _currentUser.UserId ?? Guid.Empty;
}

[ApiController]
[Authorize]
[Route(ApiRoutes.CardTemplates)]
public sealed class CardTemplatesController : ControllerBase
{
    private readonly IStudentCardService _service;
    private readonly ICurrentUserService _currentUser;

    public CardTemplatesController(IStudentCardService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.CardTemplatesRead)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var items = await _service.ListTemplatesAsync(RequireSchoolId(), activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CardTemplateDto>>.Ok(items));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.CardTemplatesRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetTemplateAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<CardTemplateDto>.Ok(item));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.CardTemplatesManage)]
    public async Task<IActionResult> Create([FromBody] SaveCardTemplateRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.CreateTemplateAsync(RequireSchoolId(), request, UserId(), cancellationToken);
        return Ok(ApiResponse<CardTemplateDto>.Ok(item, "Modèle créé."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.CardTemplatesManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCardTemplateRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.UpdateTemplateAsync(RequireSchoolId(), id, request, UserId(), cancellationToken);
        return Ok(ApiResponse<CardTemplateDto>.Ok(item, "Modèle mis à jour."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.CardTemplatesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteTemplateAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Modèle supprimé."));
    }

    [HttpPost("preview")]
    [Authorize(Policy = Permissions.CardTemplatesRead)]
    public async Task<IActionResult> Preview([FromBody] SaveCardTemplateRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.PreviewTemplateAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<CardTemplateDto>.Ok(item));
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();

    private Guid UserId() =>
        _currentUser.UserId ?? Guid.Empty;
}
