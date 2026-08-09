using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.CloudSync;

/// <summary>
/// Transforme les changements EF en unités outbox (sans dépendance circulaire DI).
/// </summary>
public sealed class CloudSyncOutboxWriter : ICloudSyncOutboxWriter
{
    public async Task EnqueueFromChangeSetAsync(
        IReadOnlyList<CloudSyncChange> changes,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Utiliser EnqueueAsync(SchoolDbContext, ...) depuis SaveChanges.");

    public async Task EnqueueAsync(
        SchoolDbContext db,
        IReadOnlyList<CloudSyncChange> changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var filtered = changes
            .Where(c => !CloudSyncCatalog.SyncMetaTables.Contains(c.TableName))
            .Where(c => CloudSyncCatalog.TryGetClrType(c.TableName, out _))
            .ToList();

        if (filtered.Count == 0)
        {
            return;
        }

        var critical = filtered.Where(c => CloudSyncCatalog.IsCriticalTable(c.TableName)).ToList();
        var others = filtered.Except(critical).ToList();

        if (critical.Count > 0)
        {
            await EnqueueGroupedAsync(db, critical, preferCritical: true, cancellationToken);
        }

        if (others.Count > 0)
        {
            await EnqueueGroupedAsync(db, others, preferCritical: false, cancellationToken);
        }
    }

    private static async Task EnqueueGroupedAsync(
        SchoolDbContext db,
        List<CloudSyncChange> changes,
        bool preferCritical,
        CancellationToken cancellationToken)
    {
        var groups = changes.GroupBy(c =>
        {
            var aggType = c.AggregateType ?? "Entity";
            var aggId = c.AggregateId ?? c.EntityId;
            return (aggType, aggId);
        });

        foreach (var group in groups)
        {
            var items = group.ToList();
            var priority = preferCritical || items.Any(i => CloudSyncCatalog.IsCriticalTable(i.TableName))
                ? SyncPriority.Critical
                : items.Min(i => CloudSyncCatalog.ResolvePriority(i.TableName));

            var distinct = items
                .GroupBy(i => (i.TableName, i.EntityId))
                .Select(g => g.Last())
                .OrderBy(i => CloudSyncCatalog.GetSequence(i.TableName))
                .ToList();

            var entityIds = distinct.Select(d => d.EntityId).ToList();
            var tableNames = distinct.Select(d => d.TableName).Distinct().ToList();
            var pendingExisting = await db.Set<SyncOutboxItem>()
                .Where(i => !i.IsDeleted
                            && (i.Status == SyncOutboxStatus.Pending || i.Status == SyncOutboxStatus.Failed)
                            && entityIds.Contains(i.EntityId)
                            && tableNames.Contains(i.TableName))
                .Select(i => new { i.TableName, i.EntityId, i.Status, i.UnitId })
                .ToListAsync(cancellationToken);

            // Mettre à jour Failed → Pending (reprise), ignorer déjà Pending
            foreach (var pending in pendingExisting.Where(p => p.Status == SyncOutboxStatus.Pending))
            {
                distinct.RemoveAll(d => d.TableName == pending.TableName && d.EntityId == pending.EntityId);
            }

            if (distinct.Count == 0)
            {
                continue;
            }

            var schoolId = db.EffectiveTenantSchoolId
                ?? await LocalSchoolResolver.TryResolvePrimarySchoolIdAsync(db, cancellationToken)
                ?? throw new InvalidOperationException(
                    "Impossible d'enfiler la sync cloud : établissement local introuvable.");

            var unit = new SyncOutboxUnit
            {
                SchoolId = schoolId,
                AggregateType = group.Key.aggType,
                AggregateId = group.Key.aggId,
                Priority = priority,
                Status = SyncOutboxStatus.Pending,
                ExpectedItemCount = distinct.Count,
                CreatedAt = DateTime.UtcNow
            };

            var seq = 0;
            foreach (var change in distinct)
            {
                unit.Items.Add(new SyncOutboxItem
                {
                    TableName = change.TableName,
                    EntityId = change.EntityId,
                    Operation = change.Operation,
                    Status = SyncOutboxStatus.Pending,
                    Sequence = seq++,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await db.Set<SyncOutboxUnit>().AddAsync(unit, cancellationToken);
        }

        db.SuppressCloudSyncEnqueue = true;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            db.SuppressCloudSyncEnqueue = false;
        }
    }
}
