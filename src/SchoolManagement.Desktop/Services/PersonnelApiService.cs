using System.Net.Http;
using SchoolManagement.Application.Personnel.DTOs;

namespace SchoolManagement.Desktop.Services;

public interface IPersonnelApiService
{
    Task<PersonnelKpiDto> GetKpisAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonnelListItemDto>> GetPersonnelAsync(
        Guid? departmentId = null,
        Guid? jobFunctionId = null,
        int? status = null,
        int? contractType = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<PersonnelDetailDto> GetPersonnelByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PersonnelDetailDto> CreatePersonnelAsync(SavePersonnelRequest request, CancellationToken cancellationToken = default);

    Task<PersonnelDetailDto> UpdatePersonnelAsync(Guid id, SavePersonnelRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HrDepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HrJobFunctionDto>> GetJobFunctionsAsync(Guid? departmentId = null, CancellationToken cancellationToken = default);

    Task<HrDepartmentDto> CreateDepartmentAsync(CreateHrDepartmentRequest request, CancellationToken cancellationToken = default);

    Task<HrJobFunctionDto> CreateJobFunctionAsync(CreateHrJobFunctionRequest request, CancellationToken cancellationToken = default);
}

public sealed class PersonnelApiService : ApiServiceBase, IPersonnelApiService
{
    public PersonnelApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<PersonnelKpiDto> GetKpisAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PersonnelKpiDto>("api/v1/personnel/kpis", cancellationToken);

    public Task<IReadOnlyList<PersonnelListItemDto>> GetPersonnelAsync(
        Guid? departmentId = null,
        Guid? jobFunctionId = null,
        int? status = null,
        int? contractType = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (departmentId.HasValue) parts.Add($"departmentId={departmentId}");
        if (jobFunctionId.HasValue) parts.Add($"jobFunctionId={jobFunctionId}");
        if (status.HasValue) parts.Add($"status={status}");
        if (contractType.HasValue) parts.Add($"contractType={contractType}");
        if (!string.IsNullOrWhiteSpace(search)) parts.Add($"search={Uri.EscapeDataString(search)}");
        var query = parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
        return GetAsync<IReadOnlyList<PersonnelListItemDto>>($"api/v1/personnel{query}", cancellationToken);
    }

    public Task<PersonnelDetailDto> GetPersonnelByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<PersonnelDetailDto>($"api/v1/personnel/{id}", cancellationToken);

    public Task<PersonnelDetailDto> CreatePersonnelAsync(SavePersonnelRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<PersonnelDetailDto>("api/v1/personnel", request, cancellationToken);

    public Task<PersonnelDetailDto> UpdatePersonnelAsync(Guid id, SavePersonnelRequest request, CancellationToken cancellationToken = default) =>
        PutAsync<PersonnelDetailDto>($"api/v1/personnel/{id}", request, cancellationToken);

    public Task<IReadOnlyList<HrDepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<HrDepartmentDto>>("api/v1/personnel/departments", cancellationToken);

    public Task<IReadOnlyList<HrJobFunctionDto>> GetJobFunctionsAsync(Guid? departmentId = null, CancellationToken cancellationToken = default)
    {
        var query = departmentId.HasValue ? $"?departmentId={departmentId}" : string.Empty;
        return GetAsync<IReadOnlyList<HrJobFunctionDto>>($"api/v1/personnel/functions{query}", cancellationToken);
    }

    public Task<HrDepartmentDto> CreateDepartmentAsync(CreateHrDepartmentRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<HrDepartmentDto>("api/v1/personnel/departments", request, cancellationToken);

    public Task<HrJobFunctionDto> CreateJobFunctionAsync(CreateHrJobFunctionRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<HrJobFunctionDto>("api/v1/personnel/functions", request, cancellationToken);
}
