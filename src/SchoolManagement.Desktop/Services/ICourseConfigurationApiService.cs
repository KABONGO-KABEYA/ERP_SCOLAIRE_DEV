using SchoolManagement.Application.CourseConfiguration.DTOs;

namespace SchoolManagement.Desktop.Services;

public interface ICourseConfigurationApiService
{
    Task<IReadOnlyList<BranchOptionDto>> GetBranchesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AvailableCourseBranchGroupDto>> GetAvailableCoursesAsync(
        Guid pedagogicalClassId,
        CancellationToken cancellationToken = default);

    Task<CourseConfigurationDto> GetConfigurationAsync(
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid classRoomId,
        CancellationToken cancellationToken = default);

    Task<CourseConfigurationDto> SaveConfigurationAsync(
        SaveCourseConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateCatalogCourseResultDto> CreateCatalogCourseAsync(
        CreateCatalogCourseRequest request,
        CancellationToken cancellationToken = default);
}
