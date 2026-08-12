using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Nettoyage prudent des unités Payment Failed/InProgress du même AggregateId
/// uniquement si chaque EntityId de l'unité existe déjà sur le cloud.
/// ACTIF reste 0 — aucun drain.
/// </summary>
var apiDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "SchoolManagement.API", "bin", "Debug", "net8.0"));
apiDir = Path.GetFullPath(apiDir);

var encryption = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var localCs = factory.BuildConnectionString(new DatabaseConfigurationManager(apiDir, encryption).LoadConfiguration());
var cloudCfg = new CloudDatabaseConfigurationManager(apiDir, encryption).LoadConfiguration();
Console.WriteLine($"ACTIF={cloudCfg.Actif} (doit rester false)");
if (cloudCfg.Actif)
{
    Console.Error.WriteLine("ABORT: ACTIF=1 — refuse de continuer.");
    return 3;
}

var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());
var localOpts = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs, s => s.CommandTimeout(30)).Options;
var cloudOpts = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs, s => s.CommandTimeout(60)).Options;

await using var local = new SchoolDbContext(localOpts) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };
await using var cloud = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };

var paymentAggregate = Guid.Parse("4EFE0AEE-EFEF-49A5-A07B-E7E81AC62371");

var units = await local.SyncOutboxUnits
    .Include(u => u.Items)
    .Where(u => !u.IsDeleted
                && u.AggregateType == "Payment"
                && u.AggregateId == paymentAggregate
                && (u.Status == SyncOutboxStatus.Failed
                    || u.Status == SyncOutboxStatus.InProgress
                    || u.Status == SyncOutboxStatus.DeadLetter))
    .OrderBy(u => u.CreatedAt)
    .ToListAsync();

var completed = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.AggregateType == "Payment" && u.AggregateId == paymentAggregate
                && u.Status == SyncOutboxStatus.Completed)
    .Select(u => new { u.Id, u.CompletedAt })
    .ToListAsync();

Console.WriteLine($"AggregateId cible: {paymentAggregate}");
Console.WriteLine($"Unités Completed déjà OK: {completed.Count} → {string.Join(", ", completed.Select(c => c.Id))}");
Console.WriteLine($"Unités à analyser (Failed/InProgress/DeadLetter): {units.Count}");

var marked = 0;
var leftFailed = 0;

foreach (var unit in units)
{
    Console.WriteLine($"\n--- Unit {unit.Id} Status={unit.Status} Attempts={unit.AttemptCount} ---");

    if (unit.AggregateId != paymentAggregate)
    {
        Console.WriteLine("  SKIP: AggregateId différent.");
        leftFailed++;
        continue;
    }

    var items = unit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.Sequence).ToList();
    if (items.Count == 0)
    {
        Console.WriteLine("  SKIP: aucun item.");
        leftFailed++;
        continue;
    }

    var allCovered = true;
    foreach (var item in items)
    {
        var exists = await EntityExistsOnCloudAsync(cloud, item.TableName, item.EntityId);
        var flag = exists ? "CLOUD_OK" : "CLOUD_MISSING";
        Console.WriteLine($"  [{flag}] seq={item.Sequence} {item.TableName} {item.EntityId} op={item.Operation}");
        if (!exists)
        {
            allCovered = false;
        }
    }

    if (!allCovered)
    {
        Console.WriteLine("  → laisse Failed/inchangé (couverture cloud incomplète).");
        leftFailed++;
        continue;
    }

    var now = DateTime.UtcNow;
    unit.Status = SyncOutboxStatus.Completed;
    unit.CompletedAt = now;
    unit.LastError = "Nettoyage manuel: agrégat déjà synchronisé (données cloud vérifiées).";
    foreach (var item in items)
    {
        item.Status = SyncOutboxStatus.Completed;
        item.LastError = null;
    }

    await local.SaveChangesAsync();
    marked++;
    Console.WriteLine("  → marqué Completed (données cloud présentes pour tous les EntityId).");
}

Console.WriteLine($"\n=== Résumé ===");
Console.WriteLine($"Marquées Completed: {marked}");
Console.WriteLine($"Laissées Failed/analyse: {leftFailed}");

var remaining = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.AggregateType == "Payment"
                && (u.Status == SyncOutboxStatus.Failed
                    || u.Status == SyncOutboxStatus.InProgress
                    || u.Status == SyncOutboxStatus.DeadLetter
                    || u.Status == SyncOutboxStatus.Pending))
    .GroupBy(u => u.Status)
    .Select(g => new { Status = g.Key, Count = g.Count() })
    .ToListAsync();
Console.WriteLine("Payment outbox restant non-Completed:");
foreach (var r in remaining)
{
    Console.WriteLine($"  {r.Status}: {r.Count}");
}

// Re-vérifie ACTIF fichier
var recheck = new CloudDatabaseConfigurationManager(apiDir, encryption).LoadConfiguration();
Console.WriteLine($"ACTIF final={recheck.Actif}");
return 0;

static async Task<bool> EntityExistsOnCloudAsync(SchoolDbContext cloud, string tableName, Guid entityId)
{
    return tableName.ToUpperInvariant() switch
    {
        "PAYMENTS" => await cloud.Set<Payment>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(e => e.Id == entityId),
        "PAYMENTLINES" => await cloud.Set<PaymentLine>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(e => e.Id == entityId),
        "FINREPARTITIONRECETTE" => await cloud.Set<RevenueAllocationEntry>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(e => e.Id == entityId),
        "FINRETENUEAPPLICATION" => await cloud.Set<WithholdingApplication>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(e => e.Id == entityId),
        _ => throw new InvalidOperationException($"Table non prévue pour couverture Payment: {tableName}")
    };
}
