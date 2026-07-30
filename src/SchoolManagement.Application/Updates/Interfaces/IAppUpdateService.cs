using SchoolManagement.Application.Updates.DTOs;

namespace SchoolManagement.Application.Updates.Interfaces;

public interface IAppUpdateService
{
    Task<UpdateCheckResponseDto?> GetLatestAsync(
        string platform,
        string? currentVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationVersionAdminDto>> ListVersionsAsync(
        CancellationToken cancellationToken = default);

    Task<ApplicationVersionAdminDto> PublishAsync(
        PublishApplicationVersionRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationVersionAdminDto> SetActiveAsync(
        Guid id,
        bool active,
        bool deactivateOthers,
        CancellationToken cancellationToken = default);
}
