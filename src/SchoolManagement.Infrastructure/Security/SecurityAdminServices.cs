using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Security;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Domain.Entities.Hr;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Security;

public sealed class SecurityAuditService : ISecurityAuditService
{
    private readonly SchoolDbContext _db;

    public SecurityAuditService(SchoolDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        string actionType,
        string summary,
        Guid? schoolId = null,
        Guid? actorUserId = null,
        string? actorUserName = null,
        SecurityAuditActorKind actorKind = SecurityAuditActorKind.SchoolAdmin,
        string? targetEntityType = null,
        Guid? targetEntityId = null,
        string? targetUserName = null,
        string? oldValuesJson = null,
        string? newValuesJson = null,
        string? ipAddress = null,
        string? userAgent = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var previousIgnore = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            _db.SecurityAuditLogs.Add(new SecurityAuditLog
            {
                OccurredAtUtc = DateTime.UtcNow,
                SchoolId = schoolId,
                ActorUserId = actorUserId,
                ActorUserName = actorUserName ?? string.Empty,
                ActorKind = actorKind,
                ActionType = actionType,
                TargetEntityType = targetEntityType,
                TargetEntityId = targetEntityId,
                TargetUserName = targetUserName,
                Summary = summary,
                OldValuesJson = oldValuesJson,
                NewValuesJson = newValuesJson,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CorrelationId = correlationId
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _db.IgnoreSchoolScope = previousIgnore;
        }
    }

    public async Task<IReadOnlyList<SecurityAuditLogDto>> QueryAsync(
        SecurityAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        var previousIgnore = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            var take = Math.Clamp(query.Take <= 0 ? 100 : query.Take, 1, 500);
            var skip = Math.Max(0, query.Skip);

            var q = _db.SecurityAuditLogs.AsNoTracking().IgnoreQueryFilters().Where(l => !l.IsDeleted);

            if (query.SchoolId.HasValue)
            {
                q = q.Where(l => l.SchoolId == query.SchoolId);
            }

            if (query.FromUtc.HasValue)
            {
                q = q.Where(l => l.OccurredAtUtc >= query.FromUtc);
            }

            if (query.ToUtc.HasValue)
            {
                q = q.Where(l => l.OccurredAtUtc <= query.ToUtc);
            }

            if (!string.IsNullOrWhiteSpace(query.ActionType))
            {
                q = q.Where(l => l.ActionType == query.ActionType);
            }

            if (query.ActorUserId.HasValue)
            {
                q = q.Where(l => l.ActorUserId == query.ActorUserId);
            }

            if (query.TargetEntityId.HasValue)
            {
                q = q.Where(l => l.TargetEntityId == query.TargetEntityId);
            }

            if (!string.IsNullOrWhiteSpace(query.TargetUserName))
            {
                var name = query.TargetUserName.Trim();
                q = q.Where(l => l.TargetUserName != null && l.TargetUserName.Contains(name));
            }

            if (!string.IsNullOrWhiteSpace(query.TargetEntityType))
            {
                q = q.Where(l => l.TargetEntityType == query.TargetEntityType);
            }

            var rows = await q
                .OrderByDescending(l => l.OccurredAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return rows.Select(MapAudit).ToList();
        }
        finally
        {
            _db.IgnoreSchoolScope = previousIgnore;
        }
    }

    public async Task<SecurityAuditLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var previousIgnore = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            var row = await _db.SecurityAuditLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted, cancellationToken);
            return row is null ? null : MapAudit(row);
        }
        finally
        {
            _db.IgnoreSchoolScope = previousIgnore;
        }
    }

    private static SecurityAuditLogDto MapAudit(SecurityAuditLog l) =>
        new(
            l.Id,
            l.OccurredAtUtc,
            l.SchoolId,
            l.ActorUserId,
            l.ActorUserName,
            l.ActorKind,
            l.ActionType,
            l.TargetEntityType,
            l.TargetEntityId,
            l.TargetUserName,
            l.Summary,
            l.OldValuesJson,
            l.NewValuesJson,
            l.IpAddress,
            l.CorrelationId);
}

public sealed class SecurityUserAdminService : ISecurityUserAdminService
{
    private readonly SchoolDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IEffectivePermissionService _effectivePermissions;
    private readonly ISecurityAuditService _audit;

    public SecurityUserAdminService(
        SchoolDbContext db,
        IPasswordHasher passwordHasher,
        IRefreshTokenRepository refreshTokens,
        IEffectivePermissionService effectivePermissions,
        ISecurityAuditService audit)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _refreshTokens = refreshTokens;
        _effectivePermissions = effectivePermissions;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SecurityUserDto>> GetUsersAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var users = await _db.UserAccounts
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.SchoolId == schoolId && !u.IsDeleted)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        return await MapUsersAsync(schoolId, users, cancellationToken);
    }

    public async Task<IReadOnlyList<SecurityPersonnelCandidateDto>> SearchPersonnelCandidatesAsync(
        Guid schoolId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var linkedTeacherIds = await _db.UserAccounts
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.SchoolId == schoolId && !u.IsDeleted && u.TeacherId != null)
            .Select(u => u.TeacherId!.Value)
            .ToListAsync(cancellationToken);
        var linked = linkedTeacherIds.ToHashSet();

        var teachers = await _db.Teachers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.SchoolId == schoolId && !t.IsDeleted && t.IsActive)
            .ToListAsync(cancellationToken);

        teachers = teachers.Where(t => !linked.Contains(t.Id)).ToList();
        if (teachers.Count == 0)
            return [];

        var teacherIds = teachers.Select(t => t.Id).ToList();
        var profiles = await _db.PersonnelHrProfiles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.SchoolId == schoolId && !p.IsDeleted && teacherIds.Contains(p.TeacherId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(p => p.TeacherId);

        var functionIds = profiles
            .Where(p => p.JobFunctionId.HasValue)
            .Select(p => p.JobFunctionId!.Value)
            .Distinct()
            .ToList();
        var functions = functionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.HrJobFunctions
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(f => functionIds.Contains(f.Id) && !f.IsDeleted)
                .ToDictionaryAsync(f => f.Id, f => f.Name, cancellationToken);

        var term = search?.Trim();
        IEnumerable<SecurityPersonnelCandidateDto> query = teachers.Select(t =>
        {
            profileMap.TryGetValue(t.Id, out var profile);
            var status = ResolvePersonnelStatus(t.IsActive, profile?.Status);
            string? functionName = null;
            if (profile?.JobFunctionId is Guid fnId)
                functions.TryGetValue(fnId, out functionName);

            return new SecurityPersonnelCandidateDto(
                t.Id,
                t.EmployeeNumber,
                t.FirstName,
                t.LastName,
                $"{t.FirstName} {t.LastName}".Trim(),
                functionName,
                t.Email,
                GetPersonnelStatusLabel(status),
                t.IsActive && status == PersonnelStatus.Actif);
        });

        // Actifs uniquement (Teacher.IsActive + statut RH Actif quand profil connu)
        query = query.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(c =>
                c.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || c.EmployeeNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || c.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || c.LastName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (c.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (c.FunctionName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return query
            .OrderBy(c => c.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.FirstName, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();
    }

    public async Task<SecurityUserDto> CreateAsync(
        Guid schoolId,
        CreateSecurityUserRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        if (request.TeacherId == Guid.Empty)
        {
            throw new DomainException("Sélectionnez un personnel pour créer le compte utilisateur.");
        }

        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new DomainException("Identifiant et mot de passe requis.");
        }

        var teacher = await _db.Teachers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TeacherId && t.SchoolId == schoolId && !t.IsDeleted, cancellationToken)
            ?? throw new DomainException("Personnel introuvable.");

        if (!teacher.IsActive)
        {
            throw new DomainException("Ce personnel n'est pas actif. Impossible de créer un compte.");
        }

        var alreadyLinked = await _db.UserAccounts
            .IgnoreQueryFilters()
            .AnyAsync(u => u.TeacherId == teacher.Id && !u.IsDeleted, cancellationToken);
        if (alreadyLinked)
        {
            throw new DomainException(
                "Ce personnel possède déjà un compte utilisateur. Création refusée.");
        }

        var exists = await _db.UserAccounts.AnyAsync(
            u => u.SchoolId == schoolId && u.UserName == request.UserName.Trim() && !u.IsDeleted,
            cancellationToken);
        if (exists)
        {
            throw new DomainException($"L'identifiant '{request.UserName.Trim()}' existe déjà.");
        }

        var email = string.IsNullOrWhiteSpace(teacher.Email)
            ? $"{request.UserName.Trim()}@local"
            : teacher.Email.Trim();

        var emailClash = await _db.UserAccounts.AnyAsync(
            u => u.SchoolId == schoolId && u.Email == email && !u.IsDeleted,
            cancellationToken);
        if (emailClash)
        {
            // Dériver un email unique technique si l'email personnel est déjà pris.
            email = $"{request.UserName.Trim()}.{teacher.Id:N}@local";
        }

        var user = new UserAccount
        {
            SchoolId = schoolId,
            TeacherId = teacher.Id,
            UserName = request.UserName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = teacher.FirstName.Trim(),
            LastName = teacher.LastName.Trim(),
            Phone = teacher.Phone,
            IsActive = true,
            MustChangePassword = request.MustChangePassword
        };

        _db.UserAccounts.Add(user);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("IX_UserAccounts_TeacherId", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message.Contains("TeacherId", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new DomainException(
                "Ce personnel possède déjà un compte utilisateur. Création refusée.");
        }

        // Aucun rôle à la création : attribution uniquement via l'onglet Rôles.

        await _audit.WriteAsync(
            "User.Created",
            $"Création utilisateur {user.UserName} (personnel {teacher.EmployeeNumber})",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(UserAccount),
            targetEntityId: user.Id,
            targetUserName: user.UserName,
            newValuesJson: JsonSerializer.Serialize(new
            {
                user.UserName,
                user.Email,
                TeacherId = teacher.Id
            }),
            cancellationToken: cancellationToken);

        return (await MapUsersAsync(schoolId, [user], cancellationToken))[0];
    }

    private static PersonnelStatus ResolvePersonnelStatus(bool teacherIsActive, PersonnelStatus? profileStatus)
    {
        if (!teacherIsActive)
            return PersonnelStatus.Inactif;
        return profileStatus ?? PersonnelStatus.Actif;
    }

    private static string GetPersonnelStatusLabel(PersonnelStatus status) => status switch
    {
        PersonnelStatus.Actif => "Actif",
        PersonnelStatus.Conge => "Congé",
        PersonnelStatus.FinContrat => "Fin de contrat",
        PersonnelStatus.Inactif => "Inactif",
        _ => status.ToString()
    };

    public async Task<SecurityUserDto> UpdateAsync(
        Guid schoolId,
        Guid userId,
        UpdateSecurityUserRequest request,
        Guid? actorUserId,
        string? actorUserName,
        bool actorIsPlatformSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(schoolId, userId, cancellationToken);
        var oldJson = JsonSerializer.Serialize(new
        {
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive,
            user.IsPlatformSuperAdmin
        });

        var wasActive = user.IsActive;
        user.Email = request.Email.Trim();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.IsActive = request.IsActive;

        if (request.IsPlatformSuperAdmin.HasValue)
        {
            if (!actorIsPlatformSuperAdmin)
            {
                throw new DomainException("Seul un Super Admin plateforme peut modifier IsPlatformSuperAdmin.");
            }

            user.IsPlatformSuperAdmin = request.IsPlatformSuperAdmin.Value;
        }

        if (wasActive && !request.IsActive)
        {
            await _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            wasActive && !request.IsActive ? "User.Deactivated" : "User.Updated",
            $"Mise à jour utilisateur {user.UserName}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(UserAccount),
            targetEntityId: user.Id,
            targetUserName: user.UserName,
            oldValuesJson: oldJson,
            newValuesJson: JsonSerializer.Serialize(new
            {
                user.Email,
                user.FirstName,
                user.LastName,
                user.IsActive,
                user.IsPlatformSuperAdmin
            }),
            cancellationToken: cancellationToken);

        return (await MapUsersAsync(schoolId, [user], cancellationToken))[0];
    }

    public async Task<SecurityUserDto> SetRolesAsync(
        Guid schoolId,
        Guid userId,
        SetSecurityUserRolesRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(schoolId, userId, cancellationToken);
        await EnsureNotRemovingLastAdminAsync(schoolId, userId, request.RoleIds, cancellationToken);
        await AssignRolesInternalAsync(schoolId, userId, request.RoleIds, cancellationToken);

        await _audit.WriteAsync(
            "User.RolesChanged",
            $"Rôles mis à jour pour {user.UserName}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(UserAccount),
            targetEntityId: user.Id,
            targetUserName: user.UserName,
            newValuesJson: JsonSerializer.Serialize(new { RoleIds = request.RoleIds }),
            cancellationToken: cancellationToken);

        return (await MapUsersAsync(schoolId, [user], cancellationToken))[0];
    }

    public async Task ResetPasswordAsync(
        Guid schoolId,
        Guid userId,
        ResetPasswordRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new DomainException("Nouveau mot de passe requis.");
        }

        var user = await GetUserOrThrowAsync(schoolId, userId, cancellationToken);
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = request.MustChangePassword;
        await _refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            "User.PasswordReset",
            $"Réinitialisation mot de passe pour {user.UserName}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(UserAccount),
            targetEntityId: user.Id,
            targetUserName: user.UserName,
            cancellationToken: cancellationToken);
    }

    public async Task<EffectivePermissionExplanationDto> GetEffectivePermissionsAsync(
        Guid schoolId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await GetUserOrThrowAsync(schoolId, userId, cancellationToken);
        return await _effectivePermissions.ExplainAsync(userId, cancellationToken);
    }

    private async Task EnsureNotRemovingLastAdminAsync(
        Guid schoolId,
        Guid userId,
        IReadOnlyList<Guid> newRoleIds,
        CancellationToken cancellationToken)
    {
        var adminRole = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.SchoolId == schoolId && r.Code == "ADMIN" && !r.IsDeleted, cancellationToken);
        if (adminRole is null)
        {
            return;
        }

        var currentlyAdmin = await _db.UserRoleAssignments.AnyAsync(
            a => a.UserId == userId && a.RoleId == adminRole.Id && !a.IsDeleted,
            cancellationToken);
        if (!currentlyAdmin || newRoleIds.Contains(adminRole.Id))
        {
            return;
        }

        var otherAdmins = await (
            from a in _db.UserRoleAssignments
            join u in _db.UserAccounts on a.UserId equals u.Id
            where a.RoleId == adminRole.Id
                  && a.UserId != userId
                  && !a.IsDeleted
                  && u.SchoolId == schoolId
                  && u.IsActive
                  && !u.IsDeleted
            select a.UserId).AnyAsync(cancellationToken);

        if (!otherAdmins)
        {
            throw new DomainException("Impossible de retirer le dernier administrateur actif de l'établissement.");
        }
    }

    private async Task AssignRolesInternalAsync(
        Guid schoolId,
        Guid userId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var roles = await _db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.SchoolId == schoolId && !r.IsDeleted)
            .ToListAsync(cancellationToken);
        var valid = roles.ToDictionary(r => r.Id);

        foreach (var roleId in roleIds.Distinct())
        {
            if (!valid.TryGetValue(roleId, out var role))
            {
                throw new KeyNotFoundException("Rôle introuvable.");
            }

            if (!role.IsAssignable && role.Code != "ADMIN")
            {
                throw new DomainException($"Le rôle '{role.Code}' n'est pas assignable.");
            }
        }

        var desiredRoleIds = roleIds.Distinct().ToHashSet();
        var allAssignments = await _db.UserRoleAssignments
            .IgnoreQueryFilters()
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        var byRoleId = allAssignments
            .GroupBy(a => a.RoleId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.IsDeleted ? 0 : 1).First());

        foreach (var (roleId, assignment) in byRoleId)
        {
            if (desiredRoleIds.Contains(roleId))
            {
                if (assignment.IsDeleted)
                {
                    assignment.IsDeleted = false;
                    assignment.DeletedAt = null;
                }

                desiredRoleIds.Remove(roleId);
            }
            else if (!assignment.IsDeleted)
            {
                assignment.IsDeleted = true;
                assignment.DeletedAt = DateTime.UtcNow;
            }
        }

        foreach (var roleId in desiredRoleIds)
        {
            var orphan = await _db.UserRoleAssignments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.UserId == userId && a.RoleId == roleId, cancellationToken);
            if (orphan is not null)
            {
                orphan.IsDeleted = false;
                orphan.DeletedAt = null;
                continue;
            }

            _db.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = userId,
                RoleId = roleId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserAccount> GetUserOrThrowAsync(Guid schoolId, Guid userId, CancellationToken cancellationToken)
    {
        return await _db.UserAccounts
                   .IgnoreQueryFilters()
                   .FirstOrDefaultAsync(u => u.Id == userId && u.SchoolId == schoolId && !u.IsDeleted, cancellationToken)
               ?? throw new KeyNotFoundException("Utilisateur introuvable.");
    }

    private async Task<IReadOnlyList<SecurityUserDto>> MapUsersAsync(
        Guid schoolId,
        IReadOnlyList<UserAccount> users,
        CancellationToken cancellationToken)
    {
        var userIds = users.Select(u => u.Id).ToHashSet();
        var assignments = await (
            from a in _db.UserRoleAssignments.AsNoTracking().IgnoreQueryFilters()
            join r in _db.Roles.AsNoTracking().IgnoreQueryFilters() on a.RoleId equals r.Id
            where userIds.Contains(a.UserId) && !a.IsDeleted && !r.IsDeleted && r.SchoolId == schoolId
            select new { a.UserId, a.RoleId, r.Code, r.Name }).ToListAsync(cancellationToken);

        return users.Select(u =>
        {
            var roles = assignments.Where(a => a.UserId == u.Id).ToList();
            var labels = PreferBusinessRoleLabels(roles.Select(r => (r.Code, r.Name)).ToList());
            var isExternalParent = u.GuardianId.HasValue
                || roles.Any(r => r.Code.Equals("PARENT", StringComparison.OrdinalIgnoreCase));
            return new SecurityUserDto(
                u.Id,
                u.UserName,
                u.Email,
                u.FirstName,
                u.LastName,
                $"{u.FirstName} {u.LastName}".Trim(),
                u.IsActive,
                u.MustChangePassword,
                u.IsPlatformSuperAdmin,
                roles.Select(r => r.Code).OrderBy(c => c).ToList(),
                roles.Select(r => r.RoleId).ToList(),
                labels,
                u.LastLoginAt,
                isExternalParent);
        }).ToList();
    }

    /// <summary>
    /// Affichage uniquement : un seul libellé métier pour les doublons legacy (TEACHER / ENSEIGNANT).
    /// </summary>
    private static IReadOnlyList<string> PreferBusinessRoleLabels(
        IReadOnlyList<(string Code, string Name)> roles)
    {
        var codes = roles.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var labels = new List<string>();
        foreach (var role in roles.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (role.Code.Equals("TEACHER", StringComparison.OrdinalIgnoreCase)
                && codes.Contains("ENSEIGNANT"))
            {
                continue;
            }

            labels.Add(role.Code.Equals("TEACHER", StringComparison.OrdinalIgnoreCase)
                ? "Enseignant"
                : role.Name);
        }

        return labels.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class SecurityRoleAdminService : ISecurityRoleAdminService
{
    private readonly SchoolDbContext _db;
    private readonly IPermissionDependencyService _dependencies;
    private readonly ISecurityAuditService _audit;

    public SecurityRoleAdminService(
        SchoolDbContext db,
        IPermissionDependencyService dependencies,
        ISecurityAuditService audit)
    {
        _db = db;
        _dependencies = dependencies;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SecurityRoleDto>> GetRolesAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.SchoolId == schoolId && !r.IsDeleted)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var counts = await _db.RolePermissions
            .AsNoTracking()
            .Where(rp => !rp.IsDeleted && roles.Select(r => r.Id).Contains(rp.RoleId))
            .GroupBy(rp => rp.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var countMap = counts.ToDictionary(c => c.RoleId, c => c.Count);

        return roles.Select(r => MapRole(r, countMap.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<SecurityRoleDto> CreateAsync(
        Guid schoolId,
        CreateSecurityRoleRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainException("Code et nom du rôle requis.");
        }

        if (await _db.Roles.AnyAsync(r => r.SchoolId == schoolId && r.Code == code && !r.IsDeleted, cancellationToken))
        {
            throw new DomainException($"Le code de rôle '{code}' existe déjà.");
        }

        var role = new Role
        {
            SchoolId = schoolId,
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            SystemRole = UserRole.Direction, // valeur technique ; Code/Name font foi pour les rôles établissement
            IsSystem = false,
            IsAssignable = request.IsAssignable,
            SortOrder = request.SortOrder
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            "Role.Created",
            $"Création rôle {role.Code}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(Role),
            targetEntityId: role.Id,
            newValuesJson: JsonSerializer.Serialize(new { role.Code, role.Name }),
            cancellationToken: cancellationToken);

        return MapRole(role, 0);
    }

    public async Task<SecurityRoleDto> UpdateAsync(
        Guid schoolId,
        Guid roleId,
        UpdateSecurityRoleRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(schoolId, roleId, cancellationToken);
        var oldJson = JsonSerializer.Serialize(new { role.Name, role.Description, role.IsAssignable, role.SortOrder });

        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();
        role.IsAssignable = request.IsAssignable;
        role.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            "Role.Updated",
            $"Mise à jour rôle {role.Code}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(Role),
            targetEntityId: role.Id,
            oldValuesJson: oldJson,
            newValuesJson: JsonSerializer.Serialize(new { role.Name, role.Description, role.IsAssignable, role.SortOrder }),
            cancellationToken: cancellationToken);

        var count = await _db.RolePermissions.CountAsync(rp => rp.RoleId == role.Id && !rp.IsDeleted, cancellationToken);
        return MapRole(role, count);
    }

    public async Task DeleteAsync(
        Guid schoolId,
        Guid roleId,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(schoolId, roleId, cancellationToken);
        if (role.IsSystem)
        {
            throw new DomainException("Les rôles système ne peuvent pas être supprimés.");
        }

        var hasAssignments = await _db.UserRoleAssignments.AnyAsync(
            a => a.RoleId == roleId && !a.IsDeleted,
            cancellationToken);
        if (hasAssignments)
        {
            throw new DomainException("Impossible de supprimer un rôle encore assigné à des utilisateurs.");
        }

        role.IsDeleted = true;
        role.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            "Role.Deleted",
            $"Suppression rôle {role.Code}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(Role),
            targetEntityId: role.Id,
            cancellationToken: cancellationToken);
    }

    public async Task<RolePermissionsDto> GetPermissionsAsync(
        Guid schoolId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(schoolId, roleId, cancellationToken);
        var codes = await _db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId && !rp.IsDeleted && !rp.Permission.IsDeleted)
            .Select(rp => rp.Permission.Code)
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        return new RolePermissionsDto(role.Id, role.Code, IsAdminRole(role), codes);
    }

    public async Task<RolePermissionsDto> SetPermissionsAsync(
        Guid schoolId,
        Guid roleId,
        SetRolePermissionsRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        var role = await GetRoleOrThrowAsync(schoolId, roleId, cancellationToken);
        if (IsAdminRole(role))
        {
            throw new DomainException("La matrice du rôle ADMIN est en lecture seule (toutes les permissions actives).");
        }

        var requested = request.PermissionCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Auto-sélection des prérequis (closure).
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in requested)
        {
            var closure = await _dependencies.GetRequiredClosureAsync(code, cancellationToken);
            expanded.UnionWith(closure);
        }

        // Refus si un prérequis est retiré alors qu'un dépendant reste demandé.
        var prereqMap = await _dependencies.GetPrerequisiteMapAsync(cancellationToken);
        foreach (var code in expanded)
        {
            if (!prereqMap.TryGetValue(code, out var prereqs))
            {
                continue;
            }

            foreach (var prereq in prereqs)
            {
                if (!expanded.Contains(prereq))
                {
                    throw new DomainException(
                        $"Impossible de retirer le prérequis '{prereq}' alors que '{code}' reste sélectionné.");
                }
            }
        }

        var permissions = await _db.Permissions
            .Where(p => expanded.Contains(p.Code) && p.IsActive && !p.IsDeleted)
            .ToListAsync(cancellationToken);
        var found = permissions.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = expanded.Where(c => !found.Contains(c)).ToList();
        if (missing.Count > 0)
        {
            throw new DomainException($"Permissions introuvables ou inactives : {string.Join(", ", missing)}");
        }

        var existing = await _db.RolePermissions
            .Where(rp => rp.RoleId == roleId && !rp.IsDeleted)
            .Include(rp => rp.Permission)
            .ToListAsync(cancellationToken);
        var oldCodes = existing.Select(rp => rp.Permission.Code).OrderBy(c => c).ToList();

        foreach (var rp in existing)
        {
            rp.IsDeleted = true;
            rp.DeletedAt = DateTime.UtcNow;
        }

        foreach (var permission in permissions)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var newCodes = permissions.Select(p => p.Code).OrderBy(c => c).ToList();
        await _audit.WriteAsync(
            "Role.PermissionsChanged",
            $"Permissions mises à jour pour le rôle {role.Code}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(Role),
            targetEntityId: role.Id,
            oldValuesJson: JsonSerializer.Serialize(oldCodes),
            newValuesJson: JsonSerializer.Serialize(newCodes),
            cancellationToken: cancellationToken);

        return new RolePermissionsDto(role.Id, role.Code, false, newCodes);
    }

    public async Task<IReadOnlyList<PermissionCatalogItemDto>> GetPermissionCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var previousIgnore = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            var prereqMap = await _dependencies.GetPrerequisiteMapAsync(cancellationToken);
            var permissions = await _db.Permissions
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(p => p.IsActive && !p.IsDeleted)
                .OrderBy(p => p.Module).ThenBy(p => p.DisplayName)
                .ToListAsync(cancellationToken);

            return permissions.Select(p => new PermissionCatalogItemDto(
                p.Id,
                p.Code,
                string.IsNullOrWhiteSpace(p.DisplayName) ? p.Code : p.DisplayName,
                p.HelpText,
                p.Module,
                p.IsActive,
                prereqMap.TryGetValue(p.Code, out var prereqs) ? prereqs : Array.Empty<string>())).ToList();
        }
        finally
        {
            _db.IgnoreSchoolScope = previousIgnore;
        }
    }

    private async Task<Role> GetRoleOrThrowAsync(Guid schoolId, Guid roleId, CancellationToken cancellationToken)
    {
        return await _db.Roles
                   .IgnoreQueryFilters()
                   .FirstOrDefaultAsync(r => r.Id == roleId && r.SchoolId == schoolId && !r.IsDeleted, cancellationToken)
               ?? throw new KeyNotFoundException("Rôle introuvable.");
    }

    private static bool IsAdminRole(Role role) =>
        string.Equals(role.Code, "ADMIN", StringComparison.OrdinalIgnoreCase);

    private static SecurityRoleDto MapRole(Role r, int permissionCount) =>
        new(
            r.Id,
            r.Code,
            r.Name,
            r.Description,
            r.IsSystem,
            r.IsAssignable,
            r.SortOrder,
            IsAdminRole(r),
            permissionCount);
}

public sealed class SecurityExceptionAdminService : ISecurityExceptionAdminService
{
    private readonly SchoolDbContext _db;
    private readonly ISecurityAuditService _audit;

    public SecurityExceptionAdminService(SchoolDbContext db, ISecurityAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<SecurityExceptionDto>> GetAsync(
        Guid schoolId,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var q = _db.UserPermissionExceptions
            .AsNoTracking()
            .Include(e => e.User)
            .Include(e => e.Permission)
            .Where(e => e.SchoolId == schoolId && !e.IsDeleted);

        if (userId.HasValue)
        {
            q = q.Where(e => e.UserId == userId);
        }

        var now = DateTime.UtcNow;
        var rows = await q.OrderByDescending(e => e.ValidFrom).ToListAsync(cancellationToken);
        return rows.Select(e => Map(e, now)).ToList();
    }

    public async Task<SecurityExceptionDto> CreateAsync(
        Guid schoolId,
        CreateSecurityExceptionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        ValidateWindow(request.ValidFrom, request.ValidTo);

        var user = await _db.UserAccounts
                       .IgnoreQueryFilters()
                       .FirstOrDefaultAsync(u => u.Id == request.UserId && u.SchoolId == schoolId && !u.IsDeleted, cancellationToken)
                   ?? throw new KeyNotFoundException("Utilisateur introuvable.");

        var permission = await _db.Permissions
                             .IgnoreQueryFilters()
                             .FirstOrDefaultAsync(p => p.Id == request.PermissionId && !p.IsDeleted, cancellationToken)
                         ?? throw new KeyNotFoundException("Permission introuvable.");

        var entity = new UserPermissionException
        {
            SchoolId = schoolId,
            UserId = user.Id,
            PermissionId = permission.Id,
            Effect = request.Effect,
            ValidFrom = request.ValidFrom.ToUniversalTime(),
            ValidTo = request.ValidTo?.ToUniversalTime(),
            Reason = request.Reason?.Trim(),
            GrantedByUserId = actorUserId
        };

        _db.UserPermissionExceptions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var actionType = request.Effect == PermissionExceptionEffect.Grant ? "Exception.Granted" : "Exception.Denied";
        await _audit.WriteAsync(
            actionType,
            $"{request.Effect} {permission.Code} pour {user.UserName}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(UserPermissionException),
            targetEntityId: entity.Id,
            targetUserName: user.UserName,
            newValuesJson: JsonSerializer.Serialize(new
            {
                permission.Code,
                request.Effect,
                entity.ValidFrom,
                entity.ValidTo,
                entity.Reason
            }),
            cancellationToken: cancellationToken);

        entity.User = user;
        entity.Permission = permission;
        return Map(entity, DateTime.UtcNow);
    }

    public async Task<SecurityExceptionDto> UpdateAsync(
        Guid schoolId,
        Guid exceptionId,
        UpdateSecurityExceptionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        ValidateWindow(request.ValidFrom, request.ValidTo);
        var entity = await LoadExceptionAsync(schoolId, exceptionId, cancellationToken);
        var oldJson = JsonSerializer.Serialize(new { entity.ValidFrom, entity.ValidTo, entity.Reason });

        entity.ValidFrom = request.ValidFrom.ToUniversalTime();
        entity.ValidTo = request.ValidTo?.ToUniversalTime();
        entity.Reason = request.Reason?.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            "Exception.Updated",
            $"Exception mise à jour ({entity.Permission.Code}) pour {entity.User.UserName}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(UserPermissionException),
            targetEntityId: entity.Id,
            targetUserName: entity.User.UserName,
            oldValuesJson: oldJson,
            newValuesJson: JsonSerializer.Serialize(new { entity.ValidFrom, entity.ValidTo, entity.Reason }),
            cancellationToken: cancellationToken);

        return Map(entity, DateTime.UtcNow);
    }

    public async Task<SecurityExceptionDto> CloseAsync(
        Guid schoolId,
        Guid exceptionId,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadExceptionAsync(schoolId, exceptionId, cancellationToken);
        var now = DateTime.UtcNow;
        if (entity.ValidTo is not null && entity.ValidTo <= now)
        {
            return Map(entity, now);
        }

        entity.ValidTo = now;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.WriteAsync(
            "Exception.Closed",
            $"Exception clôturée ({entity.Permission.Code}) pour {entity.User.UserName}",
            schoolId,
            actorUserId,
            actorUserName,
            targetEntityType: nameof(UserPermissionException),
            targetEntityId: entity.Id,
            targetUserName: entity.User.UserName,
            cancellationToken: cancellationToken);

        return Map(entity, now);
    }

    private async Task<UserPermissionException> LoadExceptionAsync(
        Guid schoolId,
        Guid exceptionId,
        CancellationToken cancellationToken)
    {
        return await _db.UserPermissionExceptions
                   .Include(e => e.User)
                   .Include(e => e.Permission)
                   .FirstOrDefaultAsync(e => e.Id == exceptionId && e.SchoolId == schoolId && !e.IsDeleted, cancellationToken)
               ?? throw new KeyNotFoundException("Exception introuvable.");
    }

    private static void ValidateWindow(DateTime validFrom, DateTime? validTo)
    {
        if (validTo.HasValue && !(validFrom < validTo.Value))
        {
            throw new DomainException("ValidFrom doit être strictement antérieur à ValidTo.");
        }
    }

    private static SecurityExceptionDto Map(UserPermissionException e, DateTime now) =>
        new(
            e.Id,
            e.UserId,
            e.User.UserName,
            e.PermissionId,
            e.Permission.Code,
            string.IsNullOrWhiteSpace(e.Permission.DisplayName) ? e.Permission.Code : e.Permission.DisplayName,
            e.Effect,
            e.ValidFrom,
            e.ValidTo,
            e.Reason,
            e.ValidFrom <= now && (e.ValidTo == null || now < e.ValidTo));
}
