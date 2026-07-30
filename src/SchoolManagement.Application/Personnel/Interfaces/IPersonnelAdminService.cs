namespace SchoolManagement.Application.Personnel.Interfaces;

using SchoolManagement.Application.Personnel.DTOs;
using SchoolManagement.Domain.Enums;

public interface IPersonnelAdminService
{
    Task<PersonnelKpiDto> GetKpisAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonnelListItemDto>> GetPersonnelAsync(
        Guid schoolId,
        Guid? departmentId = null,
        Guid? jobFunctionId = null,
        PersonnelStatus? status = null,
        PersonnelContractType? contractType = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<PersonnelDetailDto> GetPersonnelByIdAsync(
        Guid schoolId,
        Guid personnelId,
        CancellationToken cancellationToken = default);

    Task<PersonnelDetailDto> CreatePersonnelAsync(
        Guid schoolId,
        SavePersonnelRequest request,
        CancellationToken cancellationToken = default);

    Task<PersonnelDetailDto> UpdatePersonnelAsync(
        Guid schoolId,
        Guid personnelId,
        SavePersonnelRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HrDepartmentDto>> GetDepartmentsAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HrJobFunctionDto>> GetJobFunctionsAsync(
        Guid schoolId,
        Guid? departmentId = null,
        CancellationToken cancellationToken = default);

    Task<HrDepartmentDto> CreateDepartmentAsync(
        Guid schoolId,
        CreateHrDepartmentRequest request,
        CancellationToken cancellationToken = default);

    Task<HrJobFunctionDto> CreateJobFunctionAsync(
        Guid schoolId,
        CreateHrJobFunctionRequest request,
        CancellationToken cancellationToken = default);

    Task EnsureDefaultLookupsAsync(Guid schoolId, CancellationToken cancellationToken = default);
}
