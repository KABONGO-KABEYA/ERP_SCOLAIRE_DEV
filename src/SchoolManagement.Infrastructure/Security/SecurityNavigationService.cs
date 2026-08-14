using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Security;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Infrastructure.Security;

public sealed class SecurityNavigationService : ISecurityNavigationService
{
    private static readonly HashSet<string> DafAllowedModuleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "DASHBOARD",
        "PERSONNEL",
        "FINANCE"
    };

    private readonly SchoolDbContext _db;
    private readonly SecurityCatalogCache _cache;
    private readonly IEffectivePermissionService _effectivePermissions;
    private readonly ILogger<SecurityNavigationService> _logger;

    public SecurityNavigationService(
        SchoolDbContext db,
        SecurityCatalogCache cache,
        IEffectivePermissionService effectivePermissions,
        ILogger<SecurityNavigationService> logger)
    {
        _db = db;
        _cache = cache;
        _effectivePermissions = effectivePermissions;
        _logger = logger;
    }

    public async Task<NavigationTreeDto> GetNavigationAsync(
        Guid userId,
        NavigationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var effective = await _effectivePermissions.ResolveAsync(userId, cancellationToken);
        var permissionSet = effective.PermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasAdminFull = permissionSet.Contains(Permissions.AdminFull);
        var restrictToDafMenu = ShouldRestrictNavigationToDafMenu(effective.Roles);

        var catalog = await EnsureNavigationSnapshotAsync(cancellationToken);
        var modules = new List<NavigationModuleDto>();

        foreach (var module in catalog.Modules.OrderBy(m => m.SortOrder).ThenBy(m => m.Name))
        {
            if (restrictToDafMenu && !DafAllowedModuleCodes.Contains(module.Code))
            {
                continue;
            }

            var functions = new List<NavigationFunctionDto>();
            foreach (var function in module.Functions.OrderBy(f => f.SortOrder).ThenBy(f => f.Name))
            {
                var pages = new List<NavigationPageDto>();
                foreach (var page in function.Pages.OrderBy(p => p.SortOrder).ThenBy(p => p.Name))
                {
                    if (!IsPageAvailableOnChannel(page, channel))
                    {
                        continue;
                    }

                    if (!HasChannelKey(page, channel))
                    {
                        continue;
                    }

                    if (!CanAccess(page.RequiredPermissionCode, permissionSet, hasAdminFull))
                    {
                        continue;
                    }

                    var actions = page.Actions
                        .Where(a => IsActionAvailableOnChannel(a, channel))
                        .OrderBy(a => a.SortOrder)
                        .ThenBy(a => a.Name)
                        .Select(a => new NavigationActionDto(a.Code, a.Name, a.SortOrder))
                        .ToList();

                    pages.Add(new NavigationPageDto(
                        page.Code,
                        page.Name,
                        page.SortOrder,
                        page.RequiredPermissionCode,
                        page.DesktopViewKey,
                        page.WebRoute,
                        page.MobileScreenKey,
                        page.DeepLink,
                        actions));
                }

                if (pages.Count == 0)
                {
                    continue;
                }

                functions.Add(new NavigationFunctionDto(
                    function.Code,
                    function.Name,
                    function.Icon,
                    function.SortOrder,
                    pages));
            }

            if (functions.Count == 0)
            {
                continue;
            }

            modules.Add(new NavigationModuleDto(
                module.Code,
                module.Name,
                module.Icon,
                module.SortOrder,
                functions));
        }

        _logger.LogDebug(
            "Navigation {Channel} pour {UserId}: {ModuleCount} modules, {PageCount} pages",
            channel,
            userId,
            modules.Count,
            modules.SelectMany(m => m.Functions).SelectMany(f => f.Pages).Count());

        return new NavigationTreeDto(channel, modules);
    }

    private async Task<SecurityCatalogCache.NavigationCatalogSnapshot> EnsureNavigationSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var existing = _cache.TryGetNavigation();
        if (existing is not null)
        {
            return existing;
        }

        var previousIgnore = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            var moduleEntities = await _db.SecurityModules
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(m => m.Functions.Where(f => f.IsActive && !f.IsDeleted))
                .ThenInclude(f => f.Pages.Where(p => p.IsActive && !p.IsDeleted))
                .ThenInclude(p => p.Actions.Where(a => a.IsActive && !a.IsDeleted))
                .Where(m => m.IsActive && !m.IsDeleted)
                .OrderBy(m => m.SortOrder)
                .ToListAsync(cancellationToken);

            var snapshotModules = moduleEntities.Select(m => new SecurityCatalogCache.NavModuleRecord(
                m.Code,
                m.Name,
                m.Icon,
                m.SortOrder,
                m.Functions
                    .OrderBy(f => f.SortOrder)
                    .Select(f => new SecurityCatalogCache.NavFunctionRecord(
                        f.Code,
                        f.Name,
                        f.Icon,
                        f.SortOrder,
                        f.Pages
                            .OrderBy(p => p.SortOrder)
                            .Select(p => new SecurityCatalogCache.NavPageRecord(
                                p.Code,
                                p.Name,
                                p.SortOrder,
                                p.RequiredPermissionCode,
                                p.DesktopViewKey,
                                p.WebRoute,
                                p.MobileScreenKey,
                                p.DeepLink,
                                p.IsAvailableOnDesktop,
                                p.IsAvailableOnWeb,
                                p.IsAvailableOnMobile,
                                p.Actions
                                    .OrderBy(a => a.SortOrder)
                                    .Select(a => new SecurityCatalogCache.NavActionRecord(
                                        a.Code,
                                        a.Name,
                                        a.SortOrder,
                                        a.IsAvailableOnDesktop,
                                        a.IsAvailableOnWeb,
                                        a.IsAvailableOnMobile))
                                    .ToList()))
                            .ToList()))
                    .ToList())).ToList();

            var snapshot = new SecurityCatalogCache.NavigationCatalogSnapshot(snapshotModules);
            _cache.SetNavigation(snapshot);
            return snapshot;
        }
        finally
        {
            _db.IgnoreSchoolScope = previousIgnore;
        }
    }

    private static bool ShouldRestrictNavigationToDafMenu(IReadOnlyList<string> roles)
    {
        if (roles.Any(r => r.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return roles.Any(r => r.Equals("DAF", StringComparison.OrdinalIgnoreCase));
    }

    private static bool CanAccess(
        string? requiredPermissionCode,
        IReadOnlySet<string> permissions,
        bool hasAdminFull)
    {
        if (hasAdminFull)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(requiredPermissionCode))
        {
            return true;
        }

        return permissions.Contains(requiredPermissionCode);
    }

    private static bool IsPageAvailableOnChannel(
        SecurityCatalogCache.NavPageRecord page,
        NavigationChannel channel) =>
        channel switch
        {
            NavigationChannel.Desktop => page.IsAvailableOnDesktop,
            NavigationChannel.Web => page.IsAvailableOnWeb,
            NavigationChannel.Mobile => page.IsAvailableOnMobile,
            _ => false
        };

    private static bool IsActionAvailableOnChannel(
        SecurityCatalogCache.NavActionRecord action,
        NavigationChannel channel) =>
        channel switch
        {
            NavigationChannel.Desktop => action.IsAvailableOnDesktop,
            NavigationChannel.Web => action.IsAvailableOnWeb,
            NavigationChannel.Mobile => action.IsAvailableOnMobile,
            _ => false
        };

    private static bool HasChannelKey(
        SecurityCatalogCache.NavPageRecord page,
        NavigationChannel channel) =>
        channel switch
        {
            NavigationChannel.Desktop => !string.IsNullOrWhiteSpace(page.DesktopViewKey),
            NavigationChannel.Web => !string.IsNullOrWhiteSpace(page.WebRoute),
            NavigationChannel.Mobile => !string.IsNullOrWhiteSpace(page.MobileScreenKey),
            _ => false
        };
}
