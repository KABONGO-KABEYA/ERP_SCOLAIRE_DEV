using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Security;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.Security;

public sealed class SecurityCatalogAdminService : ISecurityCatalogAdminService
{
    private readonly SchoolDbContext _db;
    private readonly PermissionDependencyService _dependencies;
    private readonly ISecurityAuditService _audit;

    public SecurityCatalogAdminService(
        SchoolDbContext db,
        PermissionDependencyService dependencies,
        ISecurityAuditService audit)
    {
        _db = db;
        _dependencies = dependencies;
        _audit = audit;
    }

    public async Task<CatalogTreeDto> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        return await WithGlobalScopeAsync(async () =>
        {
            var modules = await _db.SecurityModules.AsNoTracking()
                .Where(m => !m.IsDeleted)
                .OrderBy(m => m.SortOrder)
                .ToListAsync(cancellationToken);
            var functions = await _db.SecurityFunctions.AsNoTracking()
                .Where(f => !f.IsDeleted)
                .OrderBy(f => f.SortOrder)
                .ToListAsync(cancellationToken);
            var pages = await _db.SecurityPages.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.SortOrder)
                .ToListAsync(cancellationToken);
            var actions = await _db.SecurityActions.AsNoTracking()
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.SortOrder)
                .ToListAsync(cancellationToken);
            var permissions = await _db.Permissions.AsNoTracking()
                .Where(p => !p.IsDeleted && p.SecurityActionId != null)
                .Select(p => new { p.SecurityActionId, p.Code })
                .ToListAsync(cancellationToken);
            var permsByAction = permissions
                .GroupBy(p => p.SecurityActionId!.Value)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Code).OrderBy(c => c).ToList());

            return new CatalogTreeDto(
                modules.Select(m =>
                {
                    var moduleFunctions = functions
                        .Where(f => f.ModuleId == m.Id)
                        .Select(f =>
                        {
                            var functionPages = pages
                                .Where(p => p.FunctionId == f.Id)
                                .Select(p =>
                                {
                                    var pageActions = actions
                                        .Where(a => a.PageId == p.Id)
                                        .Select(a =>
                                        {
                                            IReadOnlyList<string> codes = permsByAction.TryGetValue(a.Id, out var list)
                                                ? list
                                                : Array.Empty<string>();
                                            return new CatalogTreeActionDto(
                                                a.Id, a.Code, a.Name, a.SortOrder, a.IsActive, codes);
                                        })
                                        .ToList();
                                    return new CatalogTreePageDto(
                                        p.Id, p.Code, p.Name, p.SortOrder, p.IsActive,
                                        p.DesktopViewKey, p.RequiredPermissionCode, pageActions);
                                })
                                .ToList();
                            return new CatalogTreeFunctionDto(
                                f.Id, f.Code, f.Name, f.SortOrder, f.IsActive, functionPages);
                        })
                        .ToList();
                    return new CatalogTreeModuleDto(
                        m.Id, m.Code, m.Name, m.SortOrder, m.IsActive, moduleFunctions);
                }).ToList());
        });
    }

    public async Task<IReadOnlyList<SecurityModuleDto>> GetModulesAsync(CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            var rows = await _db.SecurityModules.AsNoTracking()
                .Where(m => !m.IsDeleted)
                .OrderBy(m => m.SortOrder)
                .ToListAsync(cancellationToken);
            return rows.Select(MapModule).ToList();
        });

    public async Task<SecurityModuleDto> UpsertModuleAsync(
        Guid? id,
        UpsertSecurityModuleRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            SecurityModule entity;
            if (id.HasValue)
            {
                entity = await _db.SecurityModules.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, cancellationToken)
                         ?? throw new KeyNotFoundException("Module introuvable.");
                entity.Name = request.Name.Trim();
                entity.Description = request.Description;
                entity.Icon = request.Icon;
                entity.SortOrder = request.SortOrder;
                entity.IsActive = request.IsActive;
                // Code immuable après création
            }
            else
            {
                var code = request.Code.Trim().ToUpperInvariant();
                if (await _db.SecurityModules.AnyAsync(m => m.Code == code && !m.IsDeleted, cancellationToken))
                {
                    throw new DomainException($"Module '{code}' existe déjà.");
                }

                entity = new SecurityModule
                {
                    Code = code,
                    Name = request.Name.Trim(),
                    Description = request.Description,
                    Icon = request.Icon,
                    SortOrder = request.SortOrder,
                    IsActive = request.IsActive
                };
                _db.SecurityModules.Add(entity);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                id.HasValue ? "Catalog.ModuleUpdated" : "Catalog.ModuleCreated",
                $"Module {entity.Code}",
                actorUserId: actorUserId,
                actorUserName: actorUserName,
                actorKind: SecurityAuditActorKind.PlatformSuperAdmin,
                targetEntityType: nameof(SecurityModule),
                targetEntityId: entity.Id,
                cancellationToken: cancellationToken);
            return MapModule(entity);
        });

    public async Task<IReadOnlyList<SecurityFunctionDto>> GetFunctionsAsync(
        Guid? moduleId,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            var q = _db.SecurityFunctions.AsNoTracking().Where(f => !f.IsDeleted);
            if (moduleId.HasValue)
            {
                q = q.Where(f => f.ModuleId == moduleId);
            }

            var rows = await q.OrderBy(f => f.SortOrder).ToListAsync(cancellationToken);
            return rows.Select(MapFunction).ToList();
        });

    public async Task<SecurityFunctionDto> UpsertFunctionAsync(
        Guid? id,
        UpsertSecurityFunctionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            _ = await _db.SecurityModules.FirstOrDefaultAsync(m => m.Id == request.ModuleId && !m.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Module introuvable.");

            SecurityFunction entity;
            if (id.HasValue)
            {
                entity = await _db.SecurityFunctions.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken)
                         ?? throw new KeyNotFoundException("Fonction introuvable.");
                entity.ModuleId = request.ModuleId;
                entity.Name = request.Name.Trim();
                entity.Description = request.Description;
                entity.Icon = request.Icon;
                entity.SortOrder = request.SortOrder;
                entity.IsActive = request.IsActive;
            }
            else
            {
                var code = request.Code.Trim().ToUpperInvariant();
                if (await _db.SecurityFunctions.AnyAsync(
                        f => f.ModuleId == request.ModuleId && f.Code == code && !f.IsDeleted, cancellationToken))
                {
                    throw new DomainException($"Fonction '{code}' existe déjà dans ce module.");
                }

                entity = new SecurityFunction
                {
                    ModuleId = request.ModuleId,
                    Code = code,
                    Name = request.Name.Trim(),
                    Description = request.Description,
                    Icon = request.Icon,
                    SortOrder = request.SortOrder,
                    IsActive = request.IsActive
                };
                _db.SecurityFunctions.Add(entity);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                id.HasValue ? "Catalog.FunctionUpdated" : "Catalog.FunctionCreated",
                $"Fonction {entity.Code}",
                actorUserId: actorUserId,
                actorUserName: actorUserName,
                actorKind: SecurityAuditActorKind.PlatformSuperAdmin,
                targetEntityType: nameof(SecurityFunction),
                targetEntityId: entity.Id,
                cancellationToken: cancellationToken);
            return MapFunction(entity);
        });

    public async Task<IReadOnlyList<SecurityPageDto>> GetPagesAsync(
        Guid? functionId,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            var q = _db.SecurityPages.AsNoTracking().Where(p => !p.IsDeleted);
            if (functionId.HasValue)
            {
                q = q.Where(p => p.FunctionId == functionId);
            }

            var rows = await q.OrderBy(p => p.SortOrder).ToListAsync(cancellationToken);
            return rows.Select(MapPage).ToList();
        });

    public async Task<SecurityPageDto> UpsertPageAsync(
        Guid? id,
        UpsertSecurityPageRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            _ = await _db.SecurityFunctions.FirstOrDefaultAsync(f => f.Id == request.FunctionId && !f.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Fonction introuvable.");

            SecurityPage entity;
            if (id.HasValue)
            {
                entity = await _db.SecurityPages.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
                         ?? throw new KeyNotFoundException("Page introuvable.");
                entity.FunctionId = request.FunctionId;
                entity.Name = request.Name.Trim();
                entity.Description = request.Description;
                entity.SortOrder = request.SortOrder;
                entity.IsActive = request.IsActive;
                entity.RequiredPermissionCode = request.RequiredPermissionCode;
                entity.DesktopViewKey = request.DesktopViewKey;
                entity.WebRoute = request.WebRoute;
                entity.MobileScreenKey = request.MobileScreenKey;
                entity.IsAvailableOnDesktop = request.IsAvailableOnDesktop;
                entity.IsAvailableOnWeb = request.IsAvailableOnWeb;
                entity.IsAvailableOnMobile = request.IsAvailableOnMobile;
            }
            else
            {
                var code = request.Code.Trim().ToUpperInvariant();
                entity = new SecurityPage
                {
                    FunctionId = request.FunctionId,
                    Code = code,
                    Name = request.Name.Trim(),
                    Description = request.Description,
                    SortOrder = request.SortOrder,
                    IsActive = request.IsActive,
                    RequiredPermissionCode = request.RequiredPermissionCode,
                    DesktopViewKey = request.DesktopViewKey,
                    WebRoute = request.WebRoute,
                    MobileScreenKey = request.MobileScreenKey,
                    IsAvailableOnDesktop = request.IsAvailableOnDesktop,
                    IsAvailableOnWeb = request.IsAvailableOnWeb,
                    IsAvailableOnMobile = request.IsAvailableOnMobile
                };
                _db.SecurityPages.Add(entity);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                id.HasValue ? "Catalog.PageUpdated" : "Catalog.PageCreated",
                $"Page {entity.Code}",
                actorUserId: actorUserId,
                actorUserName: actorUserName,
                actorKind: SecurityAuditActorKind.PlatformSuperAdmin,
                targetEntityType: nameof(SecurityPage),
                targetEntityId: entity.Id,
                cancellationToken: cancellationToken);
            return MapPage(entity);
        });

    public async Task<IReadOnlyList<SecurityActionDto>> GetActionsAsync(
        Guid? pageId,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            var q = _db.SecurityActions.AsNoTracking().Where(a => !a.IsDeleted);
            if (pageId.HasValue)
            {
                q = q.Where(a => a.PageId == pageId);
            }

            var rows = await q.OrderBy(a => a.SortOrder).ToListAsync(cancellationToken);
            return rows.Select(MapAction).ToList();
        });

    public async Task<SecurityActionDto> UpsertActionAsync(
        Guid? id,
        UpsertSecurityActionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            _ = await _db.SecurityPages.FirstOrDefaultAsync(p => p.Id == request.PageId && !p.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Page introuvable.");

            SecurityAction entity;
            if (id.HasValue)
            {
                entity = await _db.SecurityActions.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken)
                         ?? throw new KeyNotFoundException("Action introuvable.");
                entity.PageId = request.PageId;
                entity.Name = request.Name.Trim();
                entity.Description = request.Description;
                entity.SortOrder = request.SortOrder;
                entity.IsActive = request.IsActive;
                entity.IsAvailableOnDesktop = request.IsAvailableOnDesktop;
                entity.IsAvailableOnWeb = request.IsAvailableOnWeb;
                entity.IsAvailableOnMobile = request.IsAvailableOnMobile;
            }
            else
            {
                entity = new SecurityAction
                {
                    PageId = request.PageId,
                    Code = request.Code.Trim().ToUpperInvariant(),
                    Name = request.Name.Trim(),
                    Description = request.Description,
                    SortOrder = request.SortOrder,
                    IsActive = request.IsActive,
                    IsAvailableOnDesktop = request.IsAvailableOnDesktop,
                    IsAvailableOnWeb = request.IsAvailableOnWeb,
                    IsAvailableOnMobile = request.IsAvailableOnMobile
                };
                _db.SecurityActions.Add(entity);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                id.HasValue ? "Catalog.ActionUpdated" : "Catalog.ActionCreated",
                $"Action {entity.Code}",
                actorUserId: actorUserId,
                actorUserName: actorUserName,
                actorKind: SecurityAuditActorKind.PlatformSuperAdmin,
                targetEntityType: nameof(SecurityAction),
                targetEntityId: entity.Id,
                cancellationToken: cancellationToken);
            return MapAction(entity);
        });

    public async Task<IReadOnlyList<SecurityPermissionAdminDto>> GetPermissionsAsync(
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            var rows = await _db.Permissions.AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderBy(p => p.Module).ThenBy(p => p.Code)
                .ToListAsync(cancellationToken);
            return rows.Select(MapPermission).ToList();
        });

    public async Task<SecurityPermissionAdminDto> UpsertPermissionAsync(
        Guid? id,
        UpsertSecurityPermissionRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            Permission entity;
            if (id.HasValue)
            {
                entity = await _db.Permissions.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
                         ?? throw new KeyNotFoundException("Permission introuvable.");
                entity.DisplayName = request.DisplayName.Trim();
                entity.Module = request.Module.Trim();
                entity.Description = request.Description.Trim();
                entity.HelpText = request.HelpText;
                entity.IsActive = request.IsActive;
                entity.SecurityActionId = request.SecurityActionId;
                entity.Action = request.Action;
                entity.BusinessDescription = string.IsNullOrWhiteSpace(entity.BusinessDescription)
                    ? entity.Description
                    : entity.BusinessDescription;
            }
            else
            {
                var code = request.Code.Trim();
                if (await _db.Permissions.AnyAsync(p => p.Code == code && !p.IsDeleted, cancellationToken))
                {
                    throw new DomainException($"Permission '{code}' existe déjà.");
                }

                entity = new Permission
                {
                    Code = code,
                    DisplayName = request.DisplayName.Trim(),
                    Module = request.Module.Trim(),
                    Description = request.Description.Trim(),
                    BusinessDescription = request.Description.Trim(),
                    HelpText = request.HelpText,
                    IsActive = request.IsActive,
                    SecurityActionId = request.SecurityActionId,
                    Action = request.Action
                };
                _db.Permissions.Add(entity);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _audit.WriteAsync(
                id.HasValue ? "Catalog.PermissionUpdated" : "Catalog.PermissionCreated",
                $"Permission {entity.Code}",
                actorUserId: actorUserId,
                actorUserName: actorUserName,
                actorKind: SecurityAuditActorKind.PlatformSuperAdmin,
                targetEntityType: nameof(Permission),
                targetEntityId: entity.Id,
                cancellationToken: cancellationToken);
            return MapPermission(entity);
        });

    public async Task<IReadOnlyList<PermissionDependencyDto>> GetDependenciesAsync(
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            var rows = await (
                from d in _db.PermissionDependencies.AsNoTracking()
                where !d.IsDeleted
                join p in _db.Permissions.AsNoTracking() on d.PermissionId equals p.Id
                join r in _db.Permissions.AsNoTracking() on d.RequiresPermissionId equals r.Id
                select new PermissionDependencyDto(d.Id, d.PermissionId, p.Code, d.RequiresPermissionId, r.Code, d.IsActive)
            ).ToListAsync(cancellationToken);
            return rows
                .OrderBy(x => x.PermissionCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.RequiresPermissionCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });

    public async Task<PermissionDependencyDto> AddDependencyAsync(
        CreatePermissionDependencyRequest request,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            if (request.PermissionId == request.RequiresPermissionId)
            {
                throw new DomainException("Une permission ne peut pas dépendre d'elle-même.");
            }

            var dependent = await _db.Permissions.FirstOrDefaultAsync(p => p.Id == request.PermissionId && !p.IsDeleted, cancellationToken)
                            ?? throw new KeyNotFoundException("Permission dépendante introuvable.");
            var requires = await _db.Permissions.FirstOrDefaultAsync(p => p.Id == request.RequiresPermissionId && !p.IsDeleted, cancellationToken)
                           ?? throw new KeyNotFoundException("Permission prérequis introuvable.");

            if (await _db.PermissionDependencies.AnyAsync(
                    d => d.PermissionId == request.PermissionId
                         && d.RequiresPermissionId == request.RequiresPermissionId
                         && !d.IsDeleted,
                    cancellationToken))
            {
                throw new DomainException("Cette dépendance existe déjà.");
            }

            // Validation cycle via snapshot temporaire.
            var map = (await _dependencies.GetPrerequisiteMapAsync(cancellationToken))
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.ToList(),
                    StringComparer.OrdinalIgnoreCase);
            if (!map.TryGetValue(dependent.Code, out var list))
            {
                list = [];
                map[dependent.Code] = list;
            }

            if (!list.Contains(requires.Code, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(requires.Code);
            }

            if (WouldCreateCycle(dependent.Code, requires.Code, map))
            {
                throw new DomainException($"Ajout refusé : cycle détecté ({dependent.Code} → {requires.Code}).");
            }

            var entity = new PermissionDependency
            {
                PermissionId = request.PermissionId,
                RequiresPermissionId = request.RequiresPermissionId,
                IsActive = true
            };
            _db.PermissionDependencies.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync(
                "Dependency.Added",
                $"{dependent.Code} → {requires.Code}",
                actorUserId: actorUserId,
                actorUserName: actorUserName,
                actorKind: SecurityAuditActorKind.PlatformSuperAdmin,
                targetEntityType: nameof(PermissionDependency),
                targetEntityId: entity.Id,
                newValuesJson: JsonSerializer.Serialize(new { dependent.Code, Requires = requires.Code }),
                cancellationToken: cancellationToken);

            return new PermissionDependencyDto(
                entity.Id,
                entity.PermissionId,
                dependent.Code,
                entity.RequiresPermissionId,
                requires.Code,
                entity.IsActive);
        });

    public async Task RemoveDependencyAsync(
        Guid dependencyId,
        Guid? actorUserId,
        string? actorUserName,
        CancellationToken cancellationToken = default) =>
        await WithGlobalScopeAsync(async () =>
        {
            var entity = await _db.PermissionDependencies
                             .Include(d => d.Permission)
                             .Include(d => d.RequiresPermission)
                             .FirstOrDefaultAsync(d => d.Id == dependencyId && !d.IsDeleted, cancellationToken)
                         ?? throw new KeyNotFoundException("Dépendance introuvable.");

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);

            await _audit.WriteAsync(
                "Dependency.Removed",
                $"{entity.Permission.Code} → {entity.RequiresPermission.Code}",
                actorUserId: actorUserId,
                actorUserName: actorUserName,
                actorKind: SecurityAuditActorKind.PlatformSuperAdmin,
                targetEntityType: nameof(PermissionDependency),
                targetEntityId: entity.Id,
                cancellationToken: cancellationToken);
        });

    private async Task<T> WithGlobalScopeAsync<T>(Func<Task<T>> action)
    {
        var previous = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            return await action();
        }
        finally
        {
            _db.IgnoreSchoolScope = previous;
        }
    }

    private async Task WithGlobalScopeAsync(Func<Task> action)
    {
        var previous = _db.IgnoreSchoolScope;
        _db.IgnoreSchoolScope = true;
        try
        {
            await action();
        }
        finally
        {
            _db.IgnoreSchoolScope = previous;
        }
    }

    private static bool WouldCreateCycle(
        string start,
        string newPrereq,
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

        visiting.Add(start);
        return Dfs(newPrereq);
    }

    private static SecurityModuleDto MapModule(SecurityModule m) =>
        new(m.Id, m.Code, m.Name, m.Description, m.Icon, m.SortOrder, m.IsActive);

    private static SecurityFunctionDto MapFunction(SecurityFunction f) =>
        new(f.Id, f.ModuleId, f.Code, f.Name, f.Description, f.Icon, f.SortOrder, f.IsActive);

    private static SecurityPageDto MapPage(SecurityPage p) =>
        new(
            p.Id,
            p.FunctionId,
            p.Code,
            p.Name,
            p.Description,
            p.SortOrder,
            p.IsActive,
            p.RequiredPermissionCode,
            p.DesktopViewKey,
            p.WebRoute,
            p.MobileScreenKey,
            p.IsAvailableOnDesktop,
            p.IsAvailableOnWeb,
            p.IsAvailableOnMobile);

    private static SecurityActionDto MapAction(SecurityAction a) =>
        new(
            a.Id,
            a.PageId,
            a.Code,
            a.Name,
            a.Description,
            a.SortOrder,
            a.IsActive,
            a.IsAvailableOnDesktop,
            a.IsAvailableOnWeb,
            a.IsAvailableOnMobile);

    private static SecurityPermissionAdminDto MapPermission(Permission p) =>
        new(
            p.Id,
            p.Code,
            string.IsNullOrWhiteSpace(p.DisplayName) ? p.Code : p.DisplayName,
            p.Module,
            p.Description,
            p.HelpText,
            p.IsActive,
            p.SecurityActionId);
}
