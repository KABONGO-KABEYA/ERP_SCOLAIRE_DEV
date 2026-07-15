using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.SchoolFees.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Base}/school-fees")]
public class SchoolFeeController : ControllerBase
{
    private readonly ISchoolFeeService _schoolFeeService;
    private readonly ICurrentUserService _currentUser;

    public SchoolFeeController(ISchoolFeeService schoolFeeService, ICurrentUserService currentUser)
    {
        _schoolFeeService = schoolFeeService;
        _currentUser = currentUser;
    }

    [HttpGet("catalog")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var catalog = await _schoolFeeService.GetCatalogAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<SchoolFeeCatalogDto>.Ok(catalog));
    }

    [HttpGet("fee-types")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetFeeTypes(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _schoolFeeService.GetFeeTypesAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FeeTypeDto>>.Ok(items));
    }

    [HttpPost("fee-types")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CreateFeeType([FromBody] CreateFeeTypeRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var item = await _schoolFeeService.CreateFeeTypeAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<FeeTypeDto>.Ok(item, "Type de frais créé."));
    }

    [HttpPut("fee-types/{id:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> UpdateFeeType(Guid id, [FromBody] UpdateFeeTypeRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var item = await _schoolFeeService.UpdateFeeTypeAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<FeeTypeDto>.Ok(item, "Type de frais mis à jour."));
    }

    [HttpDelete("fee-types/{id:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> DeleteFeeType(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _schoolFeeService.DeleteFeeTypeAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Type de frais désactivé."));
    }

    [HttpGet("pricing-categories")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetPricingCategories(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _schoolFeeService.GetPricingCategoriesAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FeePricingCategoryDto>>.Ok(items));
    }

    [HttpPost("pricing-categories")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CreatePricingCategory([FromBody] CreateFeePricingCategoryRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var item = await _schoolFeeService.CreatePricingCategoryAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<FeePricingCategoryDto>.Ok(item, "Catégorie tarifaire créée."));
    }

    [HttpPut("pricing-categories/{id:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> UpdatePricingCategory(Guid id, [FromBody] UpdateFeePricingCategoryRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var item = await _schoolFeeService.UpdatePricingCategoryAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<FeePricingCategoryDto>.Ok(item, "Catégorie tarifaire mise à jour."));
    }

    [HttpDelete("pricing-categories/{id:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> DeletePricingCategory(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _schoolFeeService.DeletePricingCategoryAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Catégorie tarifaire désactivée."));
    }

    [HttpGet("installments")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetInstallments(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _schoolFeeService.GetInstallmentsAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FeeInstallmentDto>>.Ok(items));
    }

    [HttpPost("installments")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CreateInstallment([FromBody] SaveFeeInstallmentRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var item = await _schoolFeeService.CreateInstallmentAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<FeeInstallmentDto>.Ok(item, "Tranche créée."));
    }

    [HttpPut("installments/{id:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> UpdateInstallment(Guid id, [FromBody] SaveFeeInstallmentRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var item = await _schoolFeeService.UpdateInstallmentAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<FeeInstallmentDto>.Ok(item, "Tranche mise à jour."));
    }

    [HttpDelete("installments/{id:guid}")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> DeleteInstallment(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _schoolFeeService.DeleteInstallmentAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Tranche désactivée."));
    }

    [HttpGet("fee-types/{feeTypeId:guid}/installments")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetFeeTypeInstallments(Guid feeTypeId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _schoolFeeService.GetFeeTypeInstallmentsAsync(schoolId, feeTypeId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FeeTypeInstallmentDto>>.Ok(items));
    }

    [HttpPut("fee-types/{feeTypeId:guid}/installments")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> SaveFeeTypeInstallments(
        Guid feeTypeId,
        [FromBody] SaveFeeTypeInstallmentsRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _schoolFeeService.SaveFeeTypeInstallmentsAsync(schoolId, feeTypeId, request, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FeeTypeInstallmentDto>>.Ok(items, "Tranches affectées au type de frais."));
    }

    [HttpGet("schedule")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetSchedule(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid pedagogicalClassId,
        [FromQuery] Guid feePricingCategoryId,
        [FromQuery] Guid feeTypeId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var schedule = await _schoolFeeService.GetScheduleAsync(
            schoolId,
            academicYearId,
            pedagogicalClassId,
            feePricingCategoryId,
            feeTypeId,
            cancellationToken);
        return Ok(ApiResponse<ClassFeeScheduleDto>.Ok(schedule));
    }

    [HttpGet("schedule/signatures")]
    [Authorize(Policy = Permissions.SchoolsRead)]
    public async Task<IActionResult> GetScheduleSignatures(
        [FromQuery] Guid academicYearId,
        [FromQuery] Guid feePricingCategoryId,
        [FromQuery] Guid feeTypeId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var signatures = await _schoolFeeService.GetScheduleSignaturesAsync(
            schoolId,
            academicYearId,
            feePricingCategoryId,
            feeTypeId,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ClassFeeScheduleSignatureDto>>.Ok(signatures));
    }

    [HttpPut("schedule")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> SaveSchedule([FromBody] SaveClassFeeScheduleRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var schedule = await _schoolFeeService.SaveScheduleAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<ClassFeeScheduleDto>.Ok(schedule, "Tarifs enregistrés."));
    }

    [HttpPut("schedule/bulk")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> SaveScheduleBulk([FromBody] SaveClassFeeScheduleBulkRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _schoolFeeService.SaveScheduleBulkAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<SaveClassFeeScheduleBulkResult>.Ok(
            result,
            $"Tarifs enregistrés pour {result.SavedClassCount} classe(s)."));
    }

    [HttpPost("schedule/copy-from-previous")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CopyScheduleFromPrevious([FromBody] CopyClassFeeScheduleRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _schoolFeeService.CopyScheduleFromPreviousYearAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<CopyClassFeeScheduleResult>.Ok(result, $"{result.CopiedCount} montant(s) reporté(s)."));
    }

    [HttpPost("schedule/copy-from-previous/bulk")]
    [Authorize(Policy = Permissions.SchoolsUpdate)]
    public async Task<IActionResult> CopyScheduleFromPreviousBulk([FromBody] CopyClassFeeScheduleBulkRequest request, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var result = await _schoolFeeService.CopyScheduleFromPreviousYearBulkAsync(schoolId, request, cancellationToken);
        return Ok(ApiResponse<CopyClassFeeScheduleBulkResult>.Ok(
            result,
            $"{result.CopiedCount} montant(s) reporté(s) pour {result.ClassCount} classe(s)."));
    }
}
