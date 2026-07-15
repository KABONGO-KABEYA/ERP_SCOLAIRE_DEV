namespace SchoolManagement.Application.Admin.DTOs;

using SchoolManagement.Application.Geography.DTOs;

public sealed record UserAccountDto(
    Guid Id,
    string UserName,
    string Email,
    string FullName,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    Guid? AddressId,
    string? AddressLine)
{
    public string RolesDisplay => string.Join(", ", Roles);
}

public sealed record RoleDto(
    Guid Id,
    string Code,
    string Name,
    string? Description);

public sealed record CreateUserRequest(
    string UserName,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    IReadOnlyList<Guid> RoleIds,
    AddressInputDto? ResidenceAddress);

public sealed record UpdateUserRequest(
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    AddressInputDto? ResidenceAddress = null,
    bool UpdateAddress = false);

public sealed record SetUserRolesRequest(IReadOnlyList<Guid> RoleIds);
