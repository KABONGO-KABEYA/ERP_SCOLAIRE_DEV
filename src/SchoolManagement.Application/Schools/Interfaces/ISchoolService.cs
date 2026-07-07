namespace SchoolManagement.Application.Schools.Interfaces;

using SchoolManagement.Application.Schools.DTOs;

public interface ISchoolService
{
    Task<SchoolDto?> GetSchoolAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<SchoolDto> UpdateSchoolAsync(Guid schoolId, UpdateSchoolRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AcademicYearDto>> GetAcademicYearsAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<AcademicYearDto> CreateAcademicYearAsync(Guid schoolId, CreateAcademicYearRequest request, CancellationToken cancellationToken = default);

    Task SetCurrentAcademicYearAsync(Guid schoolId, Guid academicYearId, CancellationToken cancellationToken = default);

    Task<SchoolLookupsDto> GetLookupsAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<SchoolRegulationDto> GetRegulationAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<SchoolRegulationDto> UpdateRegulationAsync(Guid schoolId, UpdateSchoolRegulationRequest request, CancellationToken cancellationToken = default);
}
