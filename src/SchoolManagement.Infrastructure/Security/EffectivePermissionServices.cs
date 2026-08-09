using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Security;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Infrastructure.Security;

/// <summary>Cache process du catalogue sécurité — invalidation explicite uniquement (pas de TTL).</summary>
public sealed class SecurityCatalogCache : ISecurityCatalogCache
{
    private readonly object _gate = new();
    private CatalogSnapshot? _snapshot;
    private NavigationCatalogSnapshot? _navigation;

    public void Invalidate()
    {
        lock (_gate)
        {
            _snapshot = null;
            _navigation = null;
        }
    }

    internal CatalogSnapshot? TryGet()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    internal void Set(CatalogSnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
        }
    }

    internal NavigationCatalogSnapshot? TryGetNavigation()
    {
        lock (_gate)
        {
            return _navigation;
        }
    }

    internal void SetNavigation(NavigationCatalogSnapshot snapshot)
    {
        lock (_gate)
        {
            _navigation = snapshot;
        }
    }

    internal sealed record CatalogSnapshot(
        IReadOnlyDictionary<string, IReadOnlyList<string>> PrerequisitesByCode,
        IReadOnlySet<string> ActivePermissionCodes);

    internal sealed record NavigationCatalogSnapshot(
        IReadOnlyList<NavModuleRecord> Modules);

    internal sealed record NavModuleRecord(
        string Code,
        string Name,
        string? Icon,
        int SortOrder,
        IReadOnlyList<NavFunctionRecord> Functions);

    internal sealed record NavFunctionRecord(
        string Code,
        string Name,
        string? Icon,
        int SortOrder,
        IReadOnlyList<NavPageRecord> Pages);

    internal sealed record NavPageRecord(
        string Code,
        string Name,
        int SortOrder,
        string? RequiredPermissionCode,
        string? DesktopViewKey,
        string? WebRoute,
        string? MobileScreenKey,
        string? DeepLink,
        bool IsAvailableOnDesktop,
        bool IsAvailableOnWeb,
        bool IsAvailableOnMobile,
        IReadOnlyList<NavActionRecord> Actions);

    internal sealed record NavActionRecord(
        string Code,
        string Name,
        int SortOrder,
        bool IsAvailableOnDesktop,
        bool IsAvailableOnWeb,
        bool IsAvailableOnMobile);
}

public sealed class PermissionDependencyService : IPermissionDependencyService
{
    private readonly SchoolDbContext _db;
    private readonly SecurityCatalogCache _cache;
    private readonly ILogger<PermissionDependencyService> _logger;

    public PermissionDependencyService(
        SchoolDbContext db,
        SecurityCatalogCache cache,
        ILogger<PermissionDependencyService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetPrerequisiteMapAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await EnsureSnapshotAsync(cancellationToken);
        return snapshot.PrerequisitesByCode;
    }

    public async Task<IReadOnlySet<string>> GetRequiredClosureAsync(
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await EnsureSnapshotAsync(cancellationToken);
        return BuildClosure(permissionCode, snapshot.PrerequisitesByCode);
    }

    internal async Task<SecurityCatalogCache.CatalogSnapshot> EnsureSnapshotAsync(CancellationToken cancellationToken)
    {
        var existing = _cache.TryGet();
        if (existing is not null)
        {
            return existing;
        }

        var previousIgnore = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            var activeCodes = await _db.Permissions
                .AsNoTracking()
                .Where(p => p.IsActive && !p.IsDeleted)
                .Select(p => p.Code)
                .ToListAsync(cancellationToken);

            var edges = await (
                    from d in _db.PermissionDependencies.AsNoTracking().IgnoreQueryFilters()
                    where d.IsActive && !d.IsDeleted
                    join dependent in _db.Permissions.AsNoTracking().IgnoreQueryFilters() on d.PermissionId equals dependent.Id
                    join requires in _db.Permissions.AsNoTracking().IgnoreQueryFilters() on d.RequiresPermissionId equals requires.Id
                    where !dependent.IsDeleted && !requires.IsDeleted
                    select new { Dependent = dependent.Code, Requires = requires.Code })
                .ToListAsync(cancellationToken);

            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var edge in edges)
            {
                if (string.Equals(edge.Dependent, edge.Requires, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!map.TryGetValue(edge.Dependent, out var list))
                {
                    list = [];
                    map[edge.Dependent] = list;
                }

                if (!list.Contains(edge.Requires, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(edge.Requires);
                }
            }

            // Détection de cycle (DFS) — on loggue et on ignore les arêtes problématiques.
            var safeMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, prereqs) in map)
            {
                if (WouldCreateCycle(code, prereqs, map))
                {
                    _logger.LogWarning("Cycle détecté dans PermissionDependencies pour {Code} — arêtes ignorées.", code);
                    continue;
                }

                safeMap[code] = prereqs;
            }

            foreach (var code in activeCodes)
            {
                safeMap.TryAdd(code, Array.Empty<string>());
            }

            var snapshot = new SecurityCatalogCache.CatalogSnapshot(
                safeMap,
                activeCodes.ToHashSet(StringComparer.OrdinalIgnoreCase));
            _cache.Set(snapshot);
            return snapshot;
        }
        finally
        {
            _db.IgnoreSchoolScope = previousIgnore;
        }
    }

    private static bool WouldCreateCycle(
        string start,
        IReadOnlyList<string> directPrereqs,
        Dictionary<string, List<string>> map)
    {
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool Dfs(string node)
        {
            if (!visiting.Add(node))
            {
                return true;
            }

            if (map.TryGetValue(node, out var next))
            {
                foreach (var n in next)
                {
                    if (Dfs(n))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(node);
            return false;
        }

        foreach (var p in directPrereqs)
        {
            visiting.Clear();
            visiting.Add(start);
            if (Dfs(p))
            {
                return true;
            }
        }

        return false;
    }

    internal static HashSet<string> BuildClosure(
        string permissionCode,
        IReadOnlyDictionary<string, IReadOnlyList<string>> prerequisitesByCode)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { permissionCode };
        var stack = new Stack<string>();
        stack.Push(permissionCode);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!prerequisitesByCode.TryGetValue(current, out var prereqs))
            {
                continue;
            }

            foreach (var p in prereqs)
            {
                if (result.Add(p))
                {
                    stack.Push(p);
                }
            }
        }

        return result;
    }
}

public sealed class EffectivePermissionService : IEffectivePermissionService
{
    private readonly SchoolDbContext _db;
    private readonly PermissionDependencyService _dependencies;
    private readonly ILogger<EffectivePermissionService> _logger;

    public EffectivePermissionService(
        SchoolDbContext db,
        PermissionDependencyService dependencies,
        ILogger<EffectivePermissionService> logger)
    {
        _db = db;
        _dependencies = dependencies;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            return false;
        }

        var result = await ResolveAsync(userId, cancellationToken);
        return result.PermissionCodes.Contains(permissionCode, StringComparer.OrdinalIgnoreCase)
            || result.PermissionCodes.Contains(Permissions.AdminFull, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<EffectivePermissionResult> ResolveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var previousIgnore = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            var user = await _db.UserAccounts
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Utilisateur introuvable.");

            var roleRows = await _db.UserRoleAssignments
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(a => a.UserId == userId && !a.IsDeleted)
                .Select(a => new { a.RoleId, RoleCode = a.Role.Code, RoleDeleted = a.Role.IsDeleted })
                .ToListAsync(cancellationToken);

            var roleCodes = roleRows
                .Where(r => !r.RoleDeleted)
                .Select(r => r.RoleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var roleIds = roleRows.Where(r => !r.RoleDeleted).Select(r => r.RoleId).Distinct().ToList();

            var basePermissions = roleIds.Count == 0
                ? []
                : await _db.RolePermissions
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(rp => roleIds.Contains(rp.RoleId) && !rp.IsDeleted && !rp.Permission.IsDeleted && rp.Permission.IsActive)
                    .Select(rp => rp.Permission.Code)
                    .Distinct()
                    .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var exceptionRows = await _db.UserPermissionExceptions
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(e => e.UserId == userId && !e.IsDeleted && e.ValidFrom <= now && (e.ValidTo == null || now < e.ValidTo))
                .Select(e => new { e.Effect, Code = e.Permission.Code, PermActive = e.Permission.IsActive, PermDeleted = e.Permission.IsDeleted })
                .ToListAsync(cancellationToken);

            var grants = exceptionRows
                .Where(e => e.Effect == PermissionExceptionEffect.Grant && e.PermActive && !e.PermDeleted)
                .Select(e => e.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var denies = exceptionRows
                .Where(e => e.Effect == PermissionExceptionEffect.Deny)
                .Select(e => e.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var raw = new HashSet<string>(basePermissions, StringComparer.OrdinalIgnoreCase);
            raw.UnionWith(grants);
            raw.ExceptWith(denies);

            var isAdminRole = roleCodes.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase));
            var hasAdminFull = raw.Contains(Permissions.AdminFull)
                || grants.Contains(Permissions.AdminFull)
                || basePermissions.Contains(Permissions.AdminFull, StringComparer.OrdinalIgnoreCase);

            if ((isAdminRole || hasAdminFull) && !denies.Contains(Permissions.AdminFull))
            {
                var snapshot = await _dependencies.EnsureSnapshotAsync(cancellationToken);
                raw.UnionWith(snapshot.ActivePermissionCodes);
                raw.ExceptWith(denies);
            }

            var prereqMap = await _dependencies.GetPrerequisiteMapAsync(cancellationToken);
            var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in raw)
            {
                var closure = PermissionDependencyService.BuildClosure(code, prereqMap);
                if (closure.All(p => raw.Contains(p)))
                {
                    effective.Add(code);
                }
            }

            if (user.IsPlatformSuperAdmin)
            {
                foreach (var code in await _db.Permissions
                             .AsNoTracking()
                             .Where(p => p.IsActive && !p.IsDeleted && p.Code.StartsWith("platform."))
                             .Select(p => p.Code)
                             .ToListAsync(cancellationToken))
                {
                    if (!denies.Contains(code))
                    {
                        effective.Add(code);
                    }
                }
            }

            return new EffectivePermissionResult(
                roleCodes.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList(),
                effective.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
                user.IsPlatformSuperAdmin);
        }
        finally
        {
            _db.IgnoreSchoolScope = previousIgnore;
        }
    }

    public async Task<EffectivePermissionExplanationDto> ExplainAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var previousIgnore = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            var user = await _db.UserAccounts
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Utilisateur introuvable.");

            var roleRows = await _db.UserRoleAssignments
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(a => a.UserId == userId && !a.IsDeleted && !a.Role.IsDeleted)
                .Select(a => new { a.RoleId, RoleCode = a.Role.Code })
                .ToListAsync(cancellationToken);

            var roleCodes = roleRows
                .Select(r => r.RoleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var roleIds = roleRows.Select(r => r.RoleId).Distinct().ToList();

            var rolePermissionRows = roleIds.Count == 0
                ? []
                : await _db.RolePermissions
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(rp => roleIds.Contains(rp.RoleId) && !rp.IsDeleted && !rp.Permission.IsDeleted && rp.Permission.IsActive)
                    .Select(rp => new
                    {
                        RoleCode = rp.Role.Code,
                        Code = rp.Permission.Code,
                        rp.Permission.DisplayName,
                        rp.Permission.HelpText
                    })
                    .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var exceptionRows = await _db.UserPermissionExceptions
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(e => e.UserId == userId && !e.IsDeleted && e.ValidFrom <= now && (e.ValidTo == null || now < e.ValidTo))
                .Select(e => new
                {
                    e.Id,
                    e.Effect,
                    Code = e.Permission.Code,
                    e.Permission.DisplayName,
                    e.Permission.HelpText,
                    PermActive = e.Permission.IsActive,
                    PermDeleted = e.Permission.IsDeleted
                })
                .ToListAsync(cancellationToken);

            var grants = exceptionRows
                .Where(e => e.Effect == PermissionExceptionEffect.Grant && e.PermActive && !e.PermDeleted)
                .ToList();
            var denies = exceptionRows
                .Where(e => e.Effect == PermissionExceptionEffect.Deny)
                .ToList();

            var resolve = await ResolveAsync(userId, cancellationToken);
            var effectiveSet = resolve.PermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var meta = new Dictionary<string, (string DisplayName, string? HelpText)>(StringComparer.OrdinalIgnoreCase);
            void Remember(string code, string displayName, string? helpText)
            {
                if (!meta.ContainsKey(code))
                {
                    meta[code] = (displayName, helpText);
                }
            }

            foreach (var row in rolePermissionRows)
            {
                Remember(row.Code, row.DisplayName, row.HelpText);
            }

            foreach (var row in exceptionRows)
            {
                Remember(row.Code, row.DisplayName, row.HelpText);
            }

            var missingMeta = effectiveSet.Where(c => !meta.ContainsKey(c)).ToList();
            if (missingMeta.Count > 0)
            {
                var rows = await _db.Permissions
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(p => missingMeta.Contains(p.Code) && !p.IsDeleted)
                    .Select(p => new { p.Code, p.DisplayName, p.HelpText })
                    .ToListAsync(cancellationToken);
                foreach (var row in rows)
                {
                    Remember(row.Code, row.DisplayName, row.HelpText);
                }
            }

            var codes = new HashSet<string>(effectiveSet, StringComparer.OrdinalIgnoreCase);
            foreach (var row in rolePermissionRows)
            {
                codes.Add(row.Code);
            }

            foreach (var row in grants)
            {
                codes.Add(row.Code);
            }

            foreach (var row in denies)
            {
                codes.Add(row.Code);
            }

            var prereqMap = await _dependencies.GetPrerequisiteMapAsync(cancellationToken);
            var explanations = new List<PermissionExplanationDto>();

            foreach (var code in codes.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            {
                var origins = new List<PermissionOriginDetailDto>();

                foreach (var roleCode in rolePermissionRows
                             .Where(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase))
                             .Select(r => r.RoleCode)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
                {
                    origins.Add(new PermissionOriginDetailDto(PermissionOriginKind.Role, RoleCode: roleCode));
                }

                foreach (var grant in grants.Where(g => string.Equals(g.Code, code, StringComparison.OrdinalIgnoreCase)))
                {
                    origins.Add(new PermissionOriginDetailDto(PermissionOriginKind.Grant, ExceptionId: grant.Id));
                }

                foreach (var deny in denies.Where(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)))
                {
                    origins.Add(new PermissionOriginDetailDto(
                        PermissionOriginKind.Deny,
                        ExceptionId: deny.Id,
                        Note: "Exception Deny active — permission absente de l'effectif."));
                }

                if (effectiveSet.Contains(code))
                {
                    var directDependents = prereqMap
                        .Where(kv => kv.Value.Contains(code, StringComparer.OrdinalIgnoreCase)
                                     && effectiveSet.Contains(kv.Key))
                        .Select(kv => kv.Key)
                        .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                        .Take(10)
                        .ToList();

                    foreach (var dependent in directDependents)
                    {
                        origins.Add(new PermissionOriginDetailDto(
                            PermissionOriginKind.Dependency,
                            SourcePermissionCode: dependent,
                            Note: $"Prérequis de {dependent}."));
                    }
                }

                if (!meta.TryGetValue(code, out var m))
                {
                    m = (code, null);
                }

                explanations.Add(new PermissionExplanationDto(
                    code,
                    string.IsNullOrWhiteSpace(m.DisplayName) ? code : m.DisplayName,
                    m.HelpText,
                    effectiveSet.Contains(code),
                    origins));
            }

            return new EffectivePermissionExplanationDto(
                roleCodes,
                user.IsPlatformSuperAdmin,
                explanations);
        }
        finally
        {
            _db.IgnoreSchoolScope = previousIgnore;
        }
    }
}
