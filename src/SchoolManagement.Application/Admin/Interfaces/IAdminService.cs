namespace SchoolManagement.Application.Admin.Interfaces;

using SchoolManagement.Application.Admin.DTOs;

public interface IAdminService
{
    Task<IReadOnlyList<UserAccountDto>> GetUsersAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleDto>> GetRolesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<UserAccountDto> CreateUserAsync(Guid schoolId, CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserAccountDto> UpdateUserAsync(Guid schoolId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserAccountDto> SetUserRolesAsync(Guid schoolId, Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken = default);
}
