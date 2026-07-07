namespace SchoolManagement.Application.Admin.Services;

using SchoolManagement.Application.Admin.DTOs;
using SchoolManagement.Application.Admin.Interfaces;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Exceptions;

public sealed class AdminService : IAdminService
{
    private readonly IRepository<UserAccount> _userRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<UserRoleAssignment> _userRoleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public AdminService(
        IRepository<UserAccount> userRepository,
        IRepository<Role> roleRepository,
        IRepository<UserRoleAssignment> userRoleRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<UserAccountDto>> GetUsersAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.FindAsync(u => u.SchoolId == schoolId, cancellationToken);
        var assignments = await _userRoleRepository.FindAsync(_ => true, cancellationToken);
        var roles = await _roleRepository.FindAsync(r => r.SchoolId == schoolId, cancellationToken);
        var roleMap = roles.ToDictionary(r => r.Id);

        return users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => MapUser(u, assignments, roleMap))
            .ToList();
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepository.FindAsync(r => r.SchoolId == schoolId, cancellationToken);
        return roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Code, r.Name, r.Description))
            .ToList();
    }

    public async Task<UserAccountDto> CreateUserAsync(
        Guid schoolId,
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _userRepository.FindAsync(
            u => u.SchoolId == schoolId && u.UserName == request.UserName, cancellationToken);

        if (existing.Count > 0)
        {
            throw new DomainException($"L'identifiant '{request.UserName}' existe déjà.");
        }

        var user = new UserAccount
        {
            SchoolId = schoolId,
            UserName = request.UserName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            MustChangePassword = true
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await AssignRolesInternalAsync(user.Id, schoolId, request.RoleIds, cancellationToken);

        var roles = await _roleRepository.FindAsync(r => r.SchoolId == schoolId, cancellationToken);
        var assignments = await _userRoleRepository.FindAsync(a => a.UserId == user.Id, cancellationToken);
        return MapUser(user, assignments, roles.ToDictionary(r => r.Id));
    }

    public async Task<UserAccountDto> UpdateUserAsync(
        Guid schoolId,
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(schoolId, userId, cancellationToken);
        user.Email = request.Email;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.IsActive = request.IsActive;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetUserDtoAsync(schoolId, userId, cancellationToken);
    }

    public async Task<UserAccountDto> SetUserRolesAsync(
        Guid schoolId,
        Guid userId,
        SetUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        await GetUserOrThrowAsync(schoolId, userId, cancellationToken);
        await AssignRolesInternalAsync(userId, schoolId, request.RoleIds, cancellationToken);
        return await GetUserDtoAsync(schoolId, userId, cancellationToken);
    }

    private async Task AssignRolesInternalAsync(
        Guid userId,
        Guid schoolId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var validRoles = await _roleRepository.FindAsync(r => r.SchoolId == schoolId, cancellationToken);
        var validRoleIds = validRoles.Select(r => r.Id).ToHashSet();

        foreach (var roleId in roleIds)
        {
            if (!validRoleIds.Contains(roleId))
            {
                throw new KeyNotFoundException("Rôle introuvable.");
            }
        }

        var existing = await _userRoleRepository.FindAsync(a => a.UserId == userId, cancellationToken);
        foreach (var assignment in existing)
        {
            await _userRoleRepository.DeleteAsync(assignment, cancellationToken);
        }

        foreach (var roleId in roleIds.Distinct())
        {
            await _userRoleRepository.AddAsync(new UserRoleAssignment
            {
                UserId = userId,
                RoleId = roleId
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserAccount> GetUserOrThrowAsync(Guid schoolId, Guid userId, CancellationToken cancellationToken)
    {
        var user = (await _userRepository.FindAsync(
            u => u.Id == userId && u.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Utilisateur introuvable.");
        return user;
    }

    private async Task<UserAccountDto> GetUserDtoAsync(Guid schoolId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetUserOrThrowAsync(schoolId, userId, cancellationToken);
        var assignments = await _userRoleRepository.FindAsync(a => a.UserId == userId, cancellationToken);
        var roles = await _roleRepository.FindAsync(r => r.SchoolId == schoolId, cancellationToken);
        return MapUser(user, assignments, roles.ToDictionary(r => r.Id));
    }

    private static UserAccountDto MapUser(
        UserAccount user,
        IReadOnlyList<UserRoleAssignment> assignments,
        IReadOnlyDictionary<Guid, Role> roleMap)
    {
        var roleCodes = assignments
            .Where(a => a.UserId == user.Id && roleMap.ContainsKey(a.RoleId))
            .Select(a => roleMap[a.RoleId].Code)
            .Distinct()
            .ToList();

        return new UserAccountDto(
            user.Id,
            user.UserName,
            user.Email,
            $"{user.LastName} {user.FirstName}",
            user.IsActive,
            user.MustChangePassword,
            roleCodes);
    }
}
