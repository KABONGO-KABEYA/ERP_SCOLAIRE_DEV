using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Traite EXACTEMENT 1 unité outbox AggregateType=Entity.
/// Bypass ACTIF=0. Pas de drain. Completed seulement après EXISTS cloud confirmé.
/// </summary>
var apiDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "SchoolManagement.API", "bin", "Debug", "net8.0"));
apiDir = Path.GetFullPath(apiDir);

var encryption = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var cloudMgr = new CloudDatabaseConfigurationManager(apiDir, encryption);
var cloudCfg = cloudMgr.LoadConfiguration();
Console.WriteLine($"ACTIF={cloudCfg.Actif} (doit rester false)");
if (cloudCfg.Actif)
{
    Console.Error.WriteLine("ABORT: ACTIF=1.");
    return 3;
}

var localCs = factory.BuildConnectionString(new DatabaseConfigurationManager(apiDir, encryption).LoadConfiguration());
var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());

var localOpts = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs, s => s.CommandTimeout(30)).Options;
var cloudOpts = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs, s => s.CommandTimeout(120)).Options;

await using var local = new SchoolDbContext(localOpts) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };
await using var remote = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };

var unit = await local.SyncOutboxUnits
    .Include(u => u.Items)
    .Where(u => !u.IsDeleted && u.AggregateType == "Entity" && u.Status == SyncOutboxStatus.Pending)
    .OrderBy(u => u.Priority)
    .ThenBy(u => u.CreatedAt)
    .FirstOrDefaultAsync();

if (unit is null)
{
    Console.WriteLine("Aucune unité Entity Pending.");
    return 1;
}

var items = unit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.Sequence).ToList();
Console.WriteLine("=== ProcessOneEntityUnit ===");
Console.WriteLine($"UnitId={unit.Id}");
Console.WriteLine($"AggregateType={unit.AggregateType}");
Console.WriteLine($"AggregateId={unit.AggregateId}");
Console.WriteLine($"Status={unit.Status} Priority={unit.Priority} Attempts={unit.AttemptCount}");
Console.WriteLine($"ExpectedItemCount={unit.ExpectedItemCount} Items={items.Count}");

foreach (var i in items)
{
    Console.WriteLine($"\n--- Item seq={i.Sequence} ---");
    Console.WriteLine($"TableName (type Entity)={i.TableName}");
    Console.WriteLine($"EntityId={i.EntityId}");
    Console.WriteLine($"Operation={i.Operation}");
    Console.WriteLine($"AggregateId == EntityId ? {unit.AggregateId == i.EntityId}");
    await DescribeLocalAndCloudAsync(local, remote, i.TableName, i.EntityId);
}

unit.Status = SyncOutboxStatus.InProgress;
unit.AttemptCount++;
unit.LastAttemptAt = DateTime.UtcNow;
unit.LastError = null;
foreach (var item in items)
{
    item.Status = SyncOutboxStatus.InProgress;
    item.LastError = null;
}
await local.SaveChangesAsync();

var applyMethod = typeof(CloudSyncEngine)
    .GetMethod("ApplyItemAsync", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("ApplyItemAsync introuvable");
var prefetchMethod = typeof(CloudSyncEngine)
    .GetMethod("PrefetchParentsForItemAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

var total = Stopwatch.StartNew();
try
{
    Console.WriteLine("\n--- PrefetchParents (hors TX) ---");
    foreach (var item in items.Where(i => i.Operation != SyncOperationType.Delete))
    {
        var sw = Stopwatch.StartNew();
        var task = (Task)prefetchMethod.Invoke(null, [local, remote, item, CancellationToken.None])!;
        await task;
        sw.Stop();
        Console.WriteLine($"  Prefetch {item.TableName}/{item.EntityId}: {sw.ElapsedMilliseconds} ms");
    }

    Console.WriteLine("\n--- BeginTransaction + Apply ---");
    await using var tx = await remote.Database.BeginTransactionAsync();
    var applied = 0;
    foreach (var item in items)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"  Apply start seq={item.Sequence} {item.TableName} …");
        var task = (Task)applyMethod.Invoke(null, [local, remote, item, CancellationToken.None])!;
        await task;
        await remote.SaveChangesAsync();
        foreach (var entry in remote.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }

        sw.Stop();
        applied++;
        Console.WriteLine($"  Apply OK seq={item.Sequence} in {sw.ElapsedMilliseconds} ms");
    }

    var expected = unit.ExpectedItemCount > 0 ? unit.ExpectedItemCount : items.Count;
    if (applied != expected || applied != items.Count)
    {
        throw new InvalidOperationException($"Intégrité: attendu {expected}, traité {applied}/{items.Count}");
    }

    await tx.CommitAsync();
    total.Stop();
    Console.WriteLine($"\nTX commit OK — durée totale: {total.ElapsedMilliseconds} ms");

    Console.WriteLine("\n--- Confirmation Cloud (EXISTS + anti-doublon) ---");
    // Nouveau contexte lecture pour éviter cache tracker
    await using var verify = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
    var allConfirmed = true;
    foreach (var item in items)
    {
        var count = await CountOnCloudAsync(verify, item.TableName, item.EntityId);
        Console.WriteLine($"  {item.TableName}/{item.EntityId}: EXISTS={count > 0} COUNT={count}");
        if (count != 1)
        {
            allConfirmed = false;
            Console.WriteLine(count > 1
                ? "  DOUBLON détecté — ne marque pas Completed."
                : "  Manquant cloud — ne marque pas Completed.");
        }
    }

    if (!allConfirmed)
    {
        unit.Status = SyncOutboxStatus.Failed;
        unit.LastError = "Écriture TX OK mais confirmation cloud EXISTS/COUNT échouée.";
        foreach (var item in items)
        {
            item.Status = SyncOutboxStatus.Failed;
            item.LastError = unit.LastError;
        }

        await local.SaveChangesAsync();
        Console.WriteLine("Résultat: Failed (confirmation cloud).");
        return 2;
    }

    unit.Status = SyncOutboxStatus.Completed;
    unit.CompletedAt = DateTime.UtcNow;
    unit.LastError = null;
    foreach (var item in items)
    {
        item.Status = SyncOutboxStatus.Completed;
        item.LastError = null;
    }

    await local.SaveChangesAsync();
    Console.WriteLine($"\nRésultat: Completed (cloud confirmé, pas de doublon). Unit={unit.Id}");
    Console.WriteLine($"Durée totale: {total.ElapsedMilliseconds} ms");
    Console.WriteLine($"ACTIF final={cloudMgr.LoadConfiguration().Actif}");
    return 0;
}
catch (Exception ex)
{
    total.Stop();
    try
    {
        foreach (var entry in remote.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
    catch { /* ignore */ }

    unit.Status = SyncOutboxStatus.Failed;
    unit.LastError = Truncate(Flatten(ex), 2000);
    foreach (var item in items)
    {
        item.Status = SyncOutboxStatus.Failed;
        item.LastError = unit.LastError;
    }

    await local.SaveChangesAsync();
    Console.WriteLine($"\nFAILED after {total.ElapsedMilliseconds} ms");
    Console.WriteLine(Flatten(ex));
    return 2;
}

static async Task DescribeLocalAndCloudAsync(
    SchoolDbContext local, SchoolDbContext remote, string tableName, Guid entityId)
{
    var clr = ResolveClr(tableName);
    Console.WriteLine($"  ClrType={clr.Name}");

    if (tableName.Equals("ClassFeeAmounts", StringComparison.OrdinalIgnoreCase))
    {
        var row = await local.Set<ClassFeeAmount>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entityId);
        if (row is null)
        {
            Console.WriteLine("  Local: ABSENT");
        }
        else
        {
            Console.WriteLine($"  Local SchoolId={row.SchoolId}");
            Console.WriteLine($"  FK AcademicYearId={row.AcademicYearId}");
            Console.WriteLine($"  FK PedagogicalClassId={row.PedagogicalClassId}");
            Console.WriteLine($"  FK FeePricingCategoryId={row.FeePricingCategoryId}");
            Console.WriteLine($"  FK FeeTypeId={row.FeeTypeId}");
            Console.WriteLine($"  FK FeeInstallmentId={row.FeeInstallmentId}");
            Console.WriteLine($"  Amount={row.Amount} SortOrder={row.SortOrder}");
        }
    }

    var before = await CountOnCloudAsync(remote, tableName, entityId);
    Console.WriteLine($"  Cloud BEFORE: EXISTS={before > 0} COUNT={before}");
}

static Type ResolveClr(string tableName) => tableName.ToUpperInvariant() switch
{
    "CLASSFEEAMOUNTS" => typeof(ClassFeeAmount),
    _ => throw new InvalidOperationException(
        $"Table non mappée dans l'outil diagnostic (étendre ResolveClr): {tableName}")
};

static async Task<int> CountOnCloudAsync(SchoolDbContext remote, string tableName, Guid entityId)
{
    var clr = ResolveClr(tableName);
    var method = typeof(ProgramHelpers)
        .GetMethod(nameof(ProgramHelpers.CountByIdAsync), BindingFlags.Public | BindingFlags.Static)!
        .MakeGenericMethod(clr);
    var task = (Task<int>)method.Invoke(null, [remote, entityId])!;
    return await task;
}

static string Flatten(Exception ex)
{
    var parts = new List<string>();
    for (var c = ex; c is not null; c = c.InnerException)
    {
        if (!string.IsNullOrWhiteSpace(c.Message))
        {
            parts.Add(c.Message.Trim());
        }
    }

    return string.Join(" -> ", parts.Distinct());
}

static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

static class ProgramHelpers
{
    public static async Task<int> CountByIdAsync<T>(SchoolDbContext remote, Guid entityId)
        where T : AuditableEntity
        => await remote.Set<T>().IgnoreQueryFilters().AsNoTracking()
            .CountAsync(e => e.Id == entityId);
}
