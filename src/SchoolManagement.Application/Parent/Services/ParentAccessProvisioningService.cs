namespace SchoolManagement.Application.Parent.Services;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Parent.DTOs;
using SchoolManagement.Application.Parent.Interfaces;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Shared.Constants;

public sealed class ParentAccessProvisioningService : IParentAccessProvisioningService
{
    private readonly IRepository<UserAccount> _userRepository;
    private readonly IRepository<Role> _roleRepository;
    private readonly IRepository<UserRoleAssignment> _userRoleRepository;
    private readonly IRepository<Permission> _permissionRepository;
    private readonly IRepository<RolePermission> _rolePermissionRepository;
    private readonly IPasswordHasher _passwordHasher;

    public ParentAccessProvisioningService(
        IRepository<UserAccount> userRepository,
        IRepository<Role> roleRepository,
        IRepository<UserRoleAssignment> userRoleRepository,
        IRepository<Permission> permissionRepository,
        IRepository<RolePermission> rolePermissionRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<ParentAppAccessCredentialDto>> EnsureAccessForGuardiansAsync(
        Guid schoolId,
        IReadOnlyList<Guardian> guardians,
        CancellationToken cancellationToken = default)
    {
        if (guardians.Count == 0)
        {
            return [];
        }

        var parentRole = (await _roleRepository.FindAsync(
            r => r.SchoolId == schoolId && r.Code == "PARENT",
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException(
                "Le rôle PARENT est introuvable. Impossible de créer les accès application parent.");

        await EnsureParentRolePermissionsAsync(parentRole.Id, cancellationToken);

        var schoolUsers = (await _userRepository.FindAsync(u => u.SchoolId == schoolId, cancellationToken)).ToList();
        var reservedUserNames = new HashSet<string>(
            schoolUsers.Select(u => u.UserName),
            StringComparer.OrdinalIgnoreCase);

        var results = new List<ParentAppAccessCredentialDto>();
        var processedGuardianIds = new HashSet<Guid>();

        foreach (var guardian in guardians)
        {
            if (!processedGuardianIds.Add(guardian.Id))
            {
                continue;
            }

            var fullName = $"{guardian.FirstName} {guardian.LastName}".Trim();
            var existing = schoolUsers.FirstOrDefault(u => u.GuardianId == guardian.Id);
            if (existing is not null)
            {
                await EnsureUserHasParentRoleAsync(existing.Id, parentRole.Id, cancellationToken);

                // Si le parent n'a pas encore changé son mot de passe, on en régénère un
                // pour qu'il figure sur la fiche d'inscription remise au client.
                string? reissuedPassword = null;
                if (existing.MustChangePassword)
                {
                    reissuedPassword = GenerateTemporaryPassword();
                    existing.PasswordHash = _passwordHasher.Hash(reissuedPassword);
                    existing.MustChangePassword = true;
                    await _userRepository.UpdateAsync(existing, cancellationToken);
                }

                results.Add(new ParentAppAccessCredentialDto(
                    guardian.Id,
                    fullName,
                    existing.UserName,
                    reissuedPassword,
                    WasCreated: false,
                    existing.MustChangePassword));
                continue;
            }

            var userName = BuildUniqueUserName(guardian, reservedUserNames);
            reservedUserNames.Add(userName);

            var temporaryPassword = GenerateTemporaryPassword();
            var user = new UserAccount
            {
                SchoolId = schoolId,
                UserName = userName,
                Email = string.IsNullOrWhiteSpace(guardian.Email)
                    ? $"{userName}@ecole.local"
                    : guardian.Email.Trim(),
                PasswordHash = _passwordHasher.Hash(temporaryPassword),
                FirstName = guardian.FirstName.Trim(),
                LastName = guardian.LastName.Trim(),
                Phone = guardian.Phone?.Trim(),
                GuardianId = guardian.Id,
                IsActive = true,
                MustChangePassword = true
            };

            await _userRepository.AddAsync(user, cancellationToken);
            schoolUsers.Add(user);

            await _userRoleRepository.AddAsync(new UserRoleAssignment
            {
                UserId = user.Id,
                RoleId = parentRole.Id
            }, cancellationToken);

            results.Add(new ParentAppAccessCredentialDto(
                guardian.Id,
                fullName,
                userName,
                temporaryPassword,
                WasCreated: true,
                MustChangePassword: true));
        }

        return results;
    }

    private async Task EnsureUserHasParentRoleAsync(
        Guid userId,
        Guid parentRoleId,
        CancellationToken cancellationToken)
    {
        var assignments = await _userRoleRepository.FindAsync(a => a.UserId == userId, cancellationToken);
        if (assignments.Any(a => a.RoleId == parentRoleId))
        {
            return;
        }

        await _userRoleRepository.AddAsync(new UserRoleAssignment
        {
            UserId = userId,
            RoleId = parentRoleId
        }, cancellationToken);
    }

    private async Task EnsureParentRolePermissionsAsync(Guid parentRoleId, CancellationToken cancellationToken)
    {
        // List (pas string[]) : sous .NET récent, array.Contains peut lier
        // MemoryExtensions.Contains(ReadOnlySpan) et faire échouer EF Core.
        var requiredCodes = new List<string>
        {
            Permissions.PaymentsRead,
            Permissions.GradesRead,
            Permissions.ReportsRead,
            Permissions.StudentsRead
        };

        var permissions = (await _permissionRepository.FindAsync(
            p => requiredCodes.Contains(p.Code),
            cancellationToken)).ToList();

        var existing = (await _rolePermissionRepository.FindAsync(
            rp => rp.RoleId == parentRoleId,
            cancellationToken)).Select(rp => rp.PermissionId).ToHashSet();

        foreach (var permission in permissions)
        {
            if (existing.Contains(permission.Id))
            {
                continue;
            }

            await _rolePermissionRepository.AddAsync(new RolePermission
            {
                RoleId = parentRoleId,
                PermissionId = permission.Id
            }, cancellationToken);
            existing.Add(permission.Id);
        }
    }

    private static string BuildUniqueUserName(Guardian guardian, HashSet<string> reservedUserNames)
    {
        var slug = NormalizeSlug(guardian.LastName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = NormalizeSlug(guardian.FirstName);
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "parent";
        }

        var baseName = $"parent.{slug}";
        if (!reservedUserNames.Contains(baseName))
        {
            return baseName;
        }

        var phoneDigits = new string((guardian.Phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (phoneDigits.Length >= 4)
        {
            var withPhone = $"{baseName}.{phoneDigits[^4..]}";
            if (!reservedUserNames.Contains(withPhone))
            {
                return withPhone;
            }
        }

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName}.{i}";
            if (!reservedUserNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseName}.{Guid.NewGuid():N}"[..24];
    }

    private static string NormalizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string GenerateTemporaryPassword()
    {
        var suffix = RandomNumberGenerator.GetInt32(100000, 1000000);
        return $"Parent@{suffix}";
    }
}
