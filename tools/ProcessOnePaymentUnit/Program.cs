using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Traite EXACTEMENT 1 unité outbox Payment (tous ses items) — miroir ProcessUnitAsync.
/// Bypass ACTIF=0. Pas de drain massif. Pas de EnsureFinanceReferenceData global.
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
var cloudCs = factory.BuildConnectionString(
    new CloudDatabaseConfigurationManager(apiDir, encryption).LoadConfiguration().ToDatabaseConfiguration());

var localOpts = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs, s => s.CommandTimeout(30)).Options;
var cloudOpts = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs, s => s.CommandTimeout(120)).Options;

await using var local = new SchoolDbContext(localOpts) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };
await using var remote = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };

var unit = await local.SyncOutboxUnits
    .Include(u => u.Items)
    .Where(u => !u.IsDeleted && u.AggregateType == "Payment"
                && (u.Status == SyncOutboxStatus.Pending || u.Status == SyncOutboxStatus.Failed
                    || u.Status == SyncOutboxStatus.InProgress))
    .OrderBy(u => u.CreatedAt)
    .FirstOrDefaultAsync();

if (unit is null)
{
    Console.WriteLine("Aucune unité Payment Pending/Failed/InProgress.");
    return 1;
}

var items = unit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.Sequence).ToList();
Console.WriteLine($"=== ProcessOnePaymentUnit ===");
Console.WriteLine($"Unit={unit.Id} Aggregate={unit.AggregateId} Status={unit.Status} Attempts={unit.AttemptCount}");
Console.WriteLine($"Items={items.Count} expected={unit.ExpectedItemCount}");
foreach (var i in items)
{
    Console.WriteLine($"  seq={i.Sequence} {i.TableName} {i.EntityId} op={i.Operation}");
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

var total = Stopwatch.StartNew();
try
{
    Console.WriteLine("\n--- PrefetchParents (hors TX) ---");
    var prefetchMethod = typeof(CloudSyncEngine)
        .GetMethod("PrefetchParentsForItemAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
    foreach (var item in items.Where(i => i.Operation != SyncOperationType.Delete))
    {
        var sw = Stopwatch.StartNew();
        var task = (Task)prefetchMethod.Invoke(null, [local, remote, item, CancellationToken.None])!;
        await task;
        sw.Stop();
        Console.WriteLine($"  Prefetch {item.TableName}/{item.EntityId}: {sw.ElapsedMilliseconds} ms");
    }

    Console.WriteLine("\n--- BeginTransaction + Apply items ---");
    await using var tx = await remote.Database.BeginTransactionAsync();
    var applied = 0;
    foreach (var item in items)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"  Apply start seq={item.Sequence} {item.TableName} …");
        try
        {
            var task = (Task)applyMethod.Invoke(null, [local, remote, item, CancellationToken.None])!;
            await task;
            await remote.SaveChangesAsync();
            foreach (var entry in remote.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }

            sw.Stop();
            applied++;
            Console.WriteLine($"  Apply OK seq={item.Sequence} {item.TableName} in {sw.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"  Apply FAIL seq={item.Sequence} {item.TableName} in {sw.ElapsedMilliseconds} ms");
            Console.WriteLine($"  ERROR: {Flatten(ex)}");
            throw;
        }
    }

    await tx.CommitAsync();
    total.Stop();
    unit.Status = SyncOutboxStatus.Completed;
    unit.CompletedAt = DateTime.UtcNow;
    foreach (var item in items)
    {
        item.Status = SyncOutboxStatus.Completed;
    }

    await local.SaveChangesAsync();
    Console.WriteLine($"\nSUCCESS applied={applied} total={total.ElapsedMilliseconds} ms");
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
