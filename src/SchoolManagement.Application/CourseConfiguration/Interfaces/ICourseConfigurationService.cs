using SchoolManagement.Application.CourseConfiguration.DTOs;

namespace SchoolManagement.Application.CourseConfiguration.Interfaces;

public interface ICourseConfigurationService
{
    Task<IReadOnlyList<BranchOptionDto>> GetBranchesAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AvailableCourseBranchGroupDto>> GetAvailableCoursesAsync(
        Guid schoolId,
        Guid pedagogicalClassId,
        CancellationToken cancellationToken = default);

    Task<CourseConfigurationDto> GetConfigurationAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid classRoomId,
        CancellationToken cancellationToken = default);

    Task<CourseConfigurationDto> SaveConfigurationAsync(
        Guid schoolId,
        SaveCourseConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateCatalogCourseResultDto> CreateCatalogCourseAsync(
        Guid schoolId,
        CreateCatalogCourseRequest request,
        CancellationToken cancellationToken = default);
}
