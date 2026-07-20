using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Application.Reports.Interfaces;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route($"{ApiRoutes.Reports}")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(IReportService reportService, ICurrentUserService currentUser)
    {
        _reportService = reportService;
        _currentUser = currentUser;
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var stats = await _reportService.GetDashboardAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<DashboardStatsDto>.Ok(stats));
    }

    [HttpGet("enrollment-by-class")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EnrollmentByClassDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnrollmentByClass([FromQuery] Guid? academicYearId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var data = await _reportService.GetEnrollmentByClassAsync(schoolId, academicYearId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<EnrollmentByClassDto>>.Ok(data));
    }

    [HttpGet("class-averages")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClassAverageReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassAverages([FromQuery] Guid? academicPeriodId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var data = await _reportService.GetClassAveragesAsync(schoolId, academicPeriodId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ClassAverageReportDto>>.Ok(data));
    }

    [HttpGet("financial-summary")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<FinancialSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFinancialSummary([FromQuery] Guid? academicYearId, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var data = await _reportService.GetFinancialSummaryAsync(schoolId, academicYearId, cancellationToken);
        return Ok(ApiResponse<FinancialSummaryDto>.Ok(data));
    }

    [HttpGet("financial-realized-receipts")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<RealizedReceiptsResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRealizedReceipts(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? feeTypeId,
        [FromQuery] Guid? classRoomId,
        [FromQuery] Guid? sectionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 500,
        CancellationToken cancellationToken = default)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var data = await _reportService.GetRealizedReceiptsAsync(
            schoolId,
            new RealizedReceiptsRequest(
                fromDate,
                toDate,
                academicYearId,
                feeTypeId,
                classRoomId,
                sectionId,
                page,
                pageSize),
            cancellationToken);
        return Ok(ApiResponse<RealizedReceiptsResultDto>.Ok(data));
    }

    [HttpGet("financial-realized-receipts/export/pdf")]
    [Authorize(Policy = Permissions.ReportsRead)]
    public async Task<IActionResult> ExportRealizedReceiptsPdf(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? feeTypeId,
        [FromQuery] Guid? classRoomId,
        [FromQuery] Guid? sectionId,
        CancellationToken cancellationToken = default)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var bytes = await _reportService.ExportRealizedReceiptsPdfAsync(
            schoolId,
            new RealizedReceiptsRequest(fromDate, toDate, academicYearId, feeTypeId, classRoomId, sectionId),
            cancellationToken);
        return File(bytes, "application/pdf", "recettes-realisees.pdf");
    }

    [HttpGet("financial-realized-receipts/export/excel")]
    [Authorize(Policy = Permissions.ReportsRead)]
    public async Task<IActionResult> ExportRealizedReceiptsExcel(
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? feeTypeId,
        [FromQuery] Guid? classRoomId,
        [FromQuery] Guid? sectionId,
        CancellationToken cancellationToken = default)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var bytes = await _reportService.ExportRealizedReceiptsExcelAsync(
            schoolId,
            new RealizedReceiptsRequest(fromDate, toDate, academicYearId, feeTypeId, classRoomId, sectionId),
            cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "recettes-realisees.xlsx");
    }
}
