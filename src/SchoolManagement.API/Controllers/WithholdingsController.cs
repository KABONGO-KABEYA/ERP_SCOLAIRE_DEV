using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Withholdings.DTOs;
using SchoolManagement.Application.Withholdings.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.Withholdings)]
public sealed class WithholdingsController : ControllerBase
{
    private readonly IWithholdingService _service;
    private readonly ICurrentUserService _currentUser;

    public WithholdingsController(IWithholdingService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("types")]
    [Authorize(Policy = Permissions.WithholdingsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WithholdingTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTypes([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var items = await _service.GetTypesAsync(RequireSchoolId(), activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WithholdingTypeDto>>.Ok(items));
    }

    [HttpPost("types")]
    [Authorize(Policy = Permissions.WithholdingsManage)]
    public async Task<IActionResult> CreateType([FromBody] SaveWithholdingTypeRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.CreateTypeAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<WithholdingTypeDto>.Ok(item, "Type de retenue créé."));
    }

    [HttpPut("types/{id:guid}")]
    [Authorize(Policy = Permissions.WithholdingsManage)]
    public async Task<IActionResult> UpdateType(Guid id, [FromBody] SaveWithholdingTypeRequest request, CancellationToken cancellationToken)
    {
        var item = await _service.UpdateTypeAsync(RequireSchoolId(), id, request, cancellationToken);
        return Ok(ApiResponse<WithholdingTypeDto>.Ok(item, "Type de retenue mis à jour."));
    }

    [HttpPost("types/{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.WithholdingsManage)]
    public async Task<IActionResult> DeactivateType(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeactivateTypeAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Type de retenue désactivé."));
    }

    [HttpGet("configurations")]
    [Authorize(Policy = Permissions.WithholdingsRead)]
    public async Task<IActionResult> SearchConfigurations(
        [FromQuery] WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SearchConfigurationsAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<WithholdingConfigurationSearchResultDto>.Ok(result));
    }

    [HttpGet("configurations/{id:guid}")]
    [Authorize(Policy = Permissions.WithholdingsRead)]
    public async Task<IActionResult> GetConfiguration(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetConfigurationByIdAsync(RequireSchoolId(), id, cancellationToken);
        return item is null ? NotFound() : Ok(ApiResponse<WithholdingConfigurationDto>.Ok(item));
    }

    [HttpPost("configurations")]
    [Authorize(Policy = Permissions.WithholdingsManage)]
    public async Task<IActionResult> CreateConfiguration(
        [FromBody] SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _service.CreateConfigurationAsync(RequireSchoolId(), request, cancellationToken);
        return Ok(ApiResponse<WithholdingConfigurationDto>.Ok(item, "Configuration de retenue créée."));
    }

    [HttpPut("configurations/{id:guid}")]
    [Authorize(Policy = Permissions.WithholdingsManage)]
    public async Task<IActionResult> UpdateConfiguration(
        Guid id,
        [FromBody] SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _service.UpdateConfigurationAsync(RequireSchoolId(), id, request, cancellationToken);
        return Ok(ApiResponse<WithholdingConfigurationDto>.Ok(item, "Configuration de retenue mise à jour."));
    }

    [HttpPost("configurations/{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.WithholdingsManage)]
    public async Task<IActionResult> DeactivateConfiguration(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeactivateConfigurationAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Configuration désactivée."));
    }

    [HttpDelete("configurations/{id:guid}")]
    [Authorize(Policy = Permissions.WithholdingsManage)]
    public async Task<IActionResult> DeleteConfiguration(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteConfigurationAsync(RequireSchoolId(), id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Configuration supprimée."));
    }

    /// <summary>Préparé pour le module Finance : résout les retenues applicables à une ligne d'encaissement.</summary>
    [HttpPost("resolve")]
    [Authorize(Policy = Permissions.WithholdingsRead)]
    public async Task<IActionResult> Resolve(
        [FromBody] WithholdingResolveContext context,
        CancellationToken cancellationToken)
    {
        var items = await _service.ResolveApplicableAsync(RequireSchoolId(), context, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<WithholdingConfigurationDto>>.Ok(items));
    }

    /// <summary>Préparé pour le module Finance : calcule MontantNet = MontantBrut - TotalRetenues.</summary>
    [HttpPost("calculate")]
    [Authorize(Policy = Permissions.WithholdingsRead)]
    public async Task<IActionResult> Calculate(
        [FromBody] WithholdingCalculateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CalculateForPaymentLineAsync(
            RequireSchoolId(),
            request.GrossAmount,
            request.Context,
            cancellationToken);
        return Ok(ApiResponse<WithholdingCalculationResult>.Ok(result));
    }

    [HttpGet("configurations/export/excel")]
    [Authorize(Policy = Permissions.WithholdingsRead)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken)
    {
        var bytes = await _service.ExportConfigurationsExcelAsync(RequireSchoolId(), request, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "retenues.xlsx");
    }

    [HttpGet("configurations/export/pdf")]
    [Authorize(Policy = Permissions.WithholdingsRead)]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken)
    {
        var bytes = await _service.ExportConfigurationsPdfAsync(RequireSchoolId(), request, cancellationToken);
        return File(bytes, "application/pdf", "retenues.pdf");
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
}
