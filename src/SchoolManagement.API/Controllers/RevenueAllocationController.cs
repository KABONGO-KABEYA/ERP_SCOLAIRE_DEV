using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.RevenueAllocation.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.RevenueAllocation)]
public sealed class RevenueAllocationController : ControllerBase
{
    private readonly IRevenueAllocationService _service;
    private readonly ICurrentUserService _currentUser;

    public RevenueAllocationController(IRevenueAllocationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("destinations")]
    [Authorize(Policy = Permissions.RevenueAllocationRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RevenueDestinationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDestinations([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var schoolId = RequireSchoolId();
        var items = await _service.GetDestinationsAsync(schoolId, activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RevenueDestinationDto>>.Ok(items));
    }

    [HttpPost("destinations")]
    [Authorize(Policy = Permissions.RevenueAllocationManage)]
    [ProducesResponseType(typeof(ApiResponse<RevenueDestinationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateDestination([FromBody] SaveRevenueDestinationRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var item = await _service.CreateDestinationAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<RevenueDestinationDto>.Ok(item, "Destination créée."));
    }

    [HttpPut("destinations/{id:guid}")]
    [Authorize(Policy = Permissions.RevenueAllocationManage)]
    [ProducesResponseType(typeof(ApiResponse<RevenueDestinationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDestination(Guid id, [FromBody] SaveRevenueDestinationRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var item = await _service.UpdateDestinationAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<RevenueDestinationDto>.Ok(item, "Destination mise à jour."));
    }

    [HttpPost("destinations/{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.RevenueAllocationManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeactivateDestination(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _service.DeactivateDestinationAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Destination désactivée."));
    }

    [HttpGet("keys")]
    [Authorize(Policy = Permissions.RevenueAllocationRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RevenueAllocationKeyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKeys([FromQuery] Guid? academicYearId, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var items = await _service.GetKeysAsync(schoolId, academicYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RevenueAllocationKeyDto>>.Ok(items));
    }

    [HttpGet("keys/{id:guid}")]
    [Authorize(Policy = Permissions.RevenueAllocationRead)]
    [ProducesResponseType(typeof(ApiResponse<RevenueAllocationKeyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKey(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var item = await _service.GetKeyByIdAsync(schoolId, id, cancellationToken);
        return item is null ? NotFound() : Ok(ApiResponse<RevenueAllocationKeyDto>.Ok(item));
    }

    [HttpPost("keys")]
    [Authorize(Policy = Permissions.RevenueAllocationManage)]
    [ProducesResponseType(typeof(ApiResponse<RevenueAllocationKeyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateKey([FromBody] CreateRevenueAllocationKeyRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var item = await _service.CreateKeyAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<RevenueAllocationKeyDto>.Ok(item, "Clé créée."));
    }

    [HttpPut("keys/{id:guid}")]
    [Authorize(Policy = Permissions.RevenueAllocationManage)]
    [ProducesResponseType(typeof(ApiResponse<RevenueAllocationKeyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateKey(Guid id, [FromBody] UpdateRevenueAllocationKeyRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var item = await _service.UpdateKeyAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<RevenueAllocationKeyDto>.Ok(item, "Clé mise à jour."));
    }

    [HttpPost("keys/{id:guid}/activate")]
    [Authorize(Policy = Permissions.RevenueAllocationManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateKey(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _service.ActivateKeyAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Répartition ouverte."));
    }

    [HttpPost("keys/{id:guid}/close")]
    [Authorize(Policy = Permissions.RevenueAllocationManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseKey(
        Guid id,
        [FromBody] CloseRevenueAllocationKeyRequest? request,
        CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _service.CloseKeyAsync(schoolId, id, request?.EndDate, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Répartition clôturée."));
    }

    [HttpPost("keys/{id:guid}/deactivate")]
    [Authorize(Policy = Permissions.RevenueAllocationManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeactivateKey(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _service.CloseKeyAsync(schoolId, id, null, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Répartition clôturée."));
    }

    [HttpDelete("keys/{id:guid}")]
    [Authorize(Policy = Permissions.RevenueAllocationManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteKey(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        await _service.DeleteKeyAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Répartition supprimée."));
    }

    [HttpGet("entries")]
    [Authorize(Policy = Permissions.RevenueAllocationRead)]
    [ProducesResponseType(typeof(ApiResponse<RevenueAllocationSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchEntries([FromQuery] RevenueAllocationSearchRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var result = await _service.SearchAllocationsAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<RevenueAllocationSearchResultDto>.Ok(result));
    }

    [HttpGet("entries/summary-by-fee-type")]
    [Authorize(Policy = Permissions.RevenueAllocationRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FeeTypeAllocationSummaryGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryByFeeType([FromQuery] RevenueAllocationSearchRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var result = await _service.GetAllocationSummaryByFeeTypeAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FeeTypeAllocationSummaryGroupDto>>.Ok(result));
    }

    [HttpGet("entries/cash-flow")]
    [Authorize(Policy = Permissions.RevenueAllocationRead)]
    [ProducesResponseType(typeof(ApiResponse<AllocationCashFlowResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCashFlow([FromQuery] RevenueAllocationSearchRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var result = await _service.GetAllocationCashFlowAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<AllocationCashFlowResultDto>.Ok(result));
    }

    [HttpGet("entries/withholdings")]
    [Authorize(Policy = Permissions.RevenueAllocationRead)]
    [ProducesResponseType(typeof(ApiResponse<WithholdingReportResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWithholdings([FromQuery] RevenueAllocationSearchRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var result = await _service.GetWithholdingReportAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<WithholdingReportResultDto>.Ok(result));
    }

    [HttpGet("entries/export/excel")]
    [Authorize(Policy = Permissions.RevenueAllocationRead)]
    public async Task<IActionResult> ExportExcel([FromQuery] RevenueAllocationSearchRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var bytes = await _service.ExportAllocationsExcelAsync(schoolId, request, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "repartition-recettes.xlsx");
    }

    [HttpGet("entries/export/pdf")]
    [Authorize(Policy = Permissions.RevenueAllocationRead)]
    public async Task<IActionResult> ExportPdf([FromQuery] RevenueAllocationSearchRequest request, CancellationToken cancellationToken)
    {
        var schoolId = RequireSchoolId();
        var bytes = await _service.ExportAllocationsPdfAsync(schoolId, request, cancellationToken);
        return File(bytes, "application/pdf", "repartition-recettes.pdf");
    }

    private Guid RequireSchoolId() =>
        _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
}
