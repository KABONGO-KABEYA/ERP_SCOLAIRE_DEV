using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Personnel.DTOs;
using SchoolManagement.Application.Personnel.Interfaces;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.Personnel)]
public class PersonnelController : ControllerBase
{
    private readonly IPersonnelAdminService _personnelService;
    private readonly ICurrentUserService _currentUser;

    public PersonnelController(IPersonnelAdminService personnelService, ICurrentUserService currentUser)
    {
        _personnelService = personnelService;
        _currentUser = currentUser;
    }

    [HttpGet("kpis")]
    [Authorize(Policy = Permissions.PersonnelRead)]
    public async Task<IActionResult> GetKpis(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var kpis = await _personnelService.GetKpisAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<PersonnelKpiDto>.Ok(kpis));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.PersonnelRead)]
    public async Task<IActionResult> GetPersonnel(
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? jobFunctionId,
        [FromQuery] PersonnelStatus? status,
        [FromQuery] PersonnelContractType? contractType,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _personnelService.GetPersonnelAsync(
            schoolId,
            departmentId,
            jobFunctionId,
            status,
            contractType,
            search,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PersonnelListItemDto>>.Ok(items));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.PersonnelRead)]
    public async Task<IActionResult> GetPersonnelById(Guid id, CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var detail = await _personnelService.GetPersonnelByIdAsync(schoolId, id, cancellationToken);
        return Ok(ApiResponse<PersonnelDetailDto>.Ok(detail));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.PersonnelManage)]
    public async Task<IActionResult> CreatePersonnel(
        [FromBody] SavePersonnelRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var detail = await _personnelService.CreatePersonnelAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<PersonnelDetailDto>.Ok(detail, "Personnel enregistré."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.PersonnelManage)]
    public async Task<IActionResult> UpdatePersonnel(
        Guid id,
        [FromBody] SavePersonnelRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var detail = await _personnelService.UpdatePersonnelAsync(schoolId, id, request, cancellationToken);
        return Ok(ApiResponse<PersonnelDetailDto>.Ok(detail, "Personnel mis à jour."));
    }

    [HttpGet("departments")]
    [Authorize(Policy = Permissions.PersonnelRead)]
    public async Task<IActionResult> GetDepartments(CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _personnelService.GetDepartmentsAsync(schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HrDepartmentDto>>.Ok(items));
    }

    [HttpPost("departments")]
    [Authorize(Policy = Permissions.PersonnelManage)]
    public async Task<IActionResult> CreateDepartment(
        [FromBody] CreateHrDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var item = await _personnelService.CreateDepartmentAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<HrDepartmentDto>.Ok(item, "Département créé."));
    }

    [HttpGet("functions")]
    [Authorize(Policy = Permissions.PersonnelRead)]
    public async Task<IActionResult> GetJobFunctions(
        [FromQuery] Guid? departmentId,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var items = await _personnelService.GetJobFunctionsAsync(schoolId, departmentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HrJobFunctionDto>>.Ok(items));
    }

    [HttpPost("functions")]
    [Authorize(Policy = Permissions.PersonnelManage)]
    public async Task<IActionResult> CreateJobFunction(
        [FromBody] CreateHrJobFunctionRequest request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        var item = await _personnelService.CreateJobFunctionAsync(schoolId, request, cancellationToken);
        return Created(string.Empty, ApiResponse<HrJobFunctionDto>.Ok(item, "Fonction créée."));
    }
}
