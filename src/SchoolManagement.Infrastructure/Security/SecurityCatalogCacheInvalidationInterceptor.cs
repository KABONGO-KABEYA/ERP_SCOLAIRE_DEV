using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolManagement.Application.Security;
using SchoolManagement.Domain.Entities.Security;

namespace SchoolManagement.Infrastructure.Security;

/// <summary>
/// Invalide automatiquement le cache catalogue après toute mutation EF réussie
/// des entités de sécurité concernées (Permissions, Rôles, Dépendances, navigation…).
/// Les appels manuels dispersés à <see cref="ISecurityCatalogCache.Invalidate"/> ne sont plus nécessaires.
/// </summary>
public sealed class SecurityCatalogCacheInvalidationInterceptor : SaveChangesInterceptor
{
    private readonly ISecurityCatalogCache _cache;

    private static readonly HashSet<Type> CatalogEntityTypes =
    [
        typeof(Permission),
        typeof(PermissionDependency),
        typeof(Role),
        typeof(RolePermission),
        typeof(SecurityModule),
        typeof(SecurityFunction),
        typeof(SecurityPage),
        typeof(SecurityAction)
    ];

    private static readonly ConditionalWeakTable<DbContext, StrongBox<bool>> PendingByContext = new();

    public SecurityCatalogCacheInvalidationInterceptor(ISecurityCatalogCache cache)
    {
        _cache = cache;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        MarkIfNeeded(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        MarkIfNeeded(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushIfNeeded(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        FlushIfNeeded(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static void MarkIfNeeded(DbContext? context)
    {
        if (context is null || !HasCatalogChanges(context))
        {
            return;
        }

        PendingByContext.GetOrCreateValue(context).Value = true;
    }

    private void FlushIfNeeded(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        if (!PendingByContext.TryGetValue(context, out var pending) || !pending.Value)
        {
            return;
        }

        pending.Value = false;
        _cache.Invalidate();
    }

    private static bool HasCatalogChanges(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var type = entry.Entity.GetType();
            if (type.Namespace?.Contains("Proxies", StringComparison.Ordinal) == true)
            {
                type = type.BaseType ?? type;
            }

            if (CatalogEntityTypes.Contains(type))
            {
                return true;
            }
        }

        return false;
    }
}
