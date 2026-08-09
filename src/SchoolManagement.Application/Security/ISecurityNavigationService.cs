namespace SchoolManagement.Application.Security;

using SchoolManagement.Application.Security.DTOs;

public interface ISecurityNavigationService
{
    Task<NavigationTreeDto> GetNavigationAsync(
        Guid userId,
        NavigationChannel channel,
        CancellationToken cancellationToken = default);
}
