using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Drain contrôlé complet : épuise toutes les Pending, soft-delete orphelines, ACTIF=0.
/// </summary>
var apiDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "SchoolManagement.API", "bin", "Debug", "net8.0"));

var encryption = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var cloudMgr = new CloudDatabaseConfigurationManager(apiDir, encryption);
var cloudCfg = cloudMgr.LoadConfiguration();

Console.WriteLine("=== FullControlledOutboxDrain ===");
Console.WriteLine($"ACTIF={cloudCfg.Actif}");
if (cloudCfg.Actif)
{
    Console.Error.WriteLine("ABORT: ACTIF=1.");
    return 3;
}

var localCfg = new DatabaseConfigurationManager(apiDir, encryption).LoadConfiguration();
var localCs = factory.BuildConnectionString(localCfg);
var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());

var services = new ServiceCollection();
services.AddLogging(b => { b.AddConsole(); b.SetMinimumLevel(LogLevel.Warning); });
services.AddSingleton(encryption);
services.AddSingleton(factory);
services.AddSingleton(cloudMgr);
services.AddDbContext<SchoolDbContext>(opts => opts.UseSqlServer(localCs, sql => sql.CommandTimeout(30)));
services.AddScoped<ICloudSyncEngine, CloudSyncEngine>();

await using var provider = services.BuildServiceProvider();

await using var local = new SchoolDbContext(
    new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs).Options)
{ SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };
await using var cloud = new SchoolDbContext(
    new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs, sql => sql.CommandTimeout(60)).Options)
{ SuppressCloudSyncEnqueue = true };

async Task<(int Pending, int Failed, int Completed, int Active, int PendingRetry)> SnapAsync()
{
    var pending = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Pending);
    var failed = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Failed);
    var completed = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Completed);
    var active = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted);
    var retry = await local.SyncOutboxUnits.CountAsync(u =>
        !u.IsDeleted && u.Status == SyncOutboxStatus.Pending && u.AttemptCount > 0);
    return (pending, failed, completed, active, retry);
}

var totalSw = Stopwatch.StartNew();
var sessionStartUtc = DateTime.UtcNow;
var start = await SnapAsync();
Console.WriteLine($"DÉBUT — Pending={start.Pending} Failed={start.Failed} Completed={start.Completed} Active={start.Active}");

var control = new CloudSyncDrainControl(
    BypassActif: true,
    PendingOnly: true,
    VerifyCloudAfterCommit: true,
    RetryPendingOnDependencyError: true,
    SoftDeleteOrphanedLocalEntity: true);

var cumCompleted = 0;
var cumOrphaned = 0;
var cumFailed = 0;
var cumTimeouts = 0;
var round = 0;
var tableHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var fkIssues = new List<string>();
var dupCount = 0;

// Nettoyage préalable des Failed orphelines (158 attendues)
var failedCleaned = await SoftDeleteFailedOrphansAsync(local, cloud);
Console.WriteLine($"Failed orphelines soft-deleted (pré-pass): {failedCleaned}");

while (true)
{
    round++;
    var beforeRound = await SnapAsync();
    if (beforeRound.Pending == 0)
    {
        Console.WriteLine($"Round {round}: Pending=0 — fin.");
        break;
    }

    Console.WriteLine($"\n--- Round {round} Pending={beforeRound.Pending} ---");

    await using var scope = provider.CreateAsyncScope();
    var engine = scope.ServiceProvider.GetRequiredService<ICloudSyncEngine>();
    var result = await engine.DrainAsync(
        criticalOnly: false,
        maxUnits: 500,
        control: control);

    Console.WriteLine($"  {result.Message}");
    Console.WriteLine($"  OK={result.UnitsSucceeded} Failed={result.UnitsFailed} Orphaned={result.OrphanedSoftDeleted} ms={result.DurationMs}");

    cumCompleted += result.UnitsSucceeded;
    cumOrphaned += result.OrphanedSoftDeleted;
    cumFailed += result.UnitsFailed;

    var afterRound = await SnapAsync();
    if (afterRound.Pending == beforeRound.Pending
        && result.UnitsSucceeded == 0
        && result.OrphanedSoftDeleted == 0
        && result.UnitsFailed == 0)
    {
        Console.WriteLine("  Aucun progrès — arrêt.");
        break;
    }

    if (round > 50)
    {
        Console.WriteLine("  Limite rounds — arrêt.");
        break;
    }
}

totalSw.Stop();
var end = await SnapAsync();

// Tables des unités Completed durant cette session (delta completed)
var newCompletedIds = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Completed && u.CompletedAt >= sessionStartUtc)
    .Include(u => u.Items)
    .ToListAsync();

foreach (var u in newCompletedIds)
{
    foreach (var i in u.Items.Where(x => !x.IsDeleted))
    {
        tableHits[i.TableName] = tableHits.GetValueOrDefault(i.TableName) + 1;
    }
}

// Doublons sur Completed session
foreach (var u in newCompletedIds)
{
    foreach (var item in u.Items.Where(i => !i.IsDeleted && i.Operation != SyncOperationType.Delete))
    {
        try
        {
            var c = await CountCloudAsync(cloud, item.TableName, item.EntityId);
            if (c != 1)
            {
                dupCount++;
            }
        }
        catch (Exception ex)
        {
            fkIssues.Add($"{item.TableName}: {ex.Message}");
        }
    }
}

// Failed réels restants
var failedReal = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Failed)
    .ToListAsync();

foreach (var u in failedReal.Take(10))
{
    if (u.LastError?.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) == true
        || u.LastError?.Contains("Table sync inconnue", StringComparison.OrdinalIgnoreCase) == true)
    {
        fkIssues.Add(u.LastError);
    }
}

var sessionCompleted = end.Completed - start.Completed;
var sessionOrphaned = cumOrphaned + failedCleaned;
var minutes = Math.Max(totalSw.Elapsed.TotalMinutes, 0.001);

Console.WriteLine("\n=== BILAN GLOBAL FullControlledOutboxDrain ===");
Console.WriteLine($"Completed (session): {sessionCompleted} (cumul moteur rounds: {cumCompleted})");
Console.WriteLine($"Orphaned soft-deleted (session): {sessionOrphaned} (rounds: {cumOrphaned}, Failed pré-pass: {failedCleaned})");
Console.WriteLine($"Pending/Retry restant: {end.Pending} (retry={end.PendingRetry})");
Console.WriteLine($"Failed réel restant: {end.Failed}");
Console.WriteLine($"Timeouts (cumul Failed rounds): {cumTimeouts}");
Console.WriteLine($"Durée totale: {totalSw.ElapsedMilliseconds} ms ({totalSw.Elapsed.TotalSeconds:F1} s)");
Console.WriteLine($"Débit moyen: {sessionCompleted / minutes:F2} Completed/min");
Console.WriteLine($"Outbox actives restantes: {end.Active}");
Console.WriteLine($"ACTIF final={cloudMgr.LoadConfiguration().Actif}");

Console.WriteLine("\nTables (Completed session):");
foreach (var kv in tableHits.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
{
    Console.WriteLine($"  {kv.Key}: {kv.Value}");
}

Console.WriteLine("\nFK/mappings:");
if (fkIssues.Count == 0)
{
    Console.WriteLine("  (aucun)");
}
else
{
    foreach (var f in fkIssues.Distinct().Take(15))
    {
        Console.WriteLine($"  {f}");
    }
}

Console.WriteLine($"\nDoublons Completed session: {dupCount}");

if (failedReal.Count > 0)
{
    Console.WriteLine("\nFailed restants (échantillon):");
    foreach (var u in failedReal.Take(8))
    {
        Console.WriteLine($"  {u.Id}: {Truncate(u.LastError, 120)}");
    }
}

return end.Pending == 0 && end.Failed == 0 ? 0 : end.Failed > 0 ? 2 : 1;

static async Task<int> SoftDeleteFailedOrphansAsync(SchoolDbContext local, SchoolDbContext cloud)
{
    var failed = await local.SyncOutboxUnits
        .Include(u => u.Items)
        .Where(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Failed)
        .ToListAsync();

    var cleaned = 0;
    var now = DateTime.UtcNow;
    foreach (var unit in failed)
    {
        var items = unit.Items.Where(i => !i.IsDeleted).ToList();
        if (items.Count == 0)
        {
            continue;
        }

        var orphan = true;
        foreach (var item in items.Where(i => i.Operation != SyncOperationType.Delete))
        {
            if (!TryResolveClr(item.TableName, out var clr))
            {
                orphan = false;
                break;
            }

            if (await ExistsLocalAsync(local, clr, item.EntityId))
            {
                orphan = false;
                break;
            }

            if (await CountAsync(cloud, clr, item.EntityId) != 0)
            {
                orphan = false;
                break;
            }
        }

        if (!orphan)
        {
            continue;
        }

        unit.IsDeleted = true;
        unit.DeletedAt = now;
        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.DeletedAt = now;
        }

        cleaned++;
    }

    if (cleaned > 0)
    {
        await local.SaveChangesAsync();
    }

    return cleaned;
}

static bool TryResolveClr(string tableName, out Type clrType)
{
    clrType = null!;
    var catalogType = typeof(CloudSyncEngine).Assembly
        .GetType("SchoolManagement.Infrastructure.CloudSync.CloudSyncCatalog", throwOnError: true)!;
    var syncOrder = (IReadOnlyList<(string Table, Type ClrType)>)catalogType
        .GetField("SyncOrder", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
    foreach (var e in syncOrder)
    {
        if (e.Table.Equals(tableName, StringComparison.OrdinalIgnoreCase))
        {
            clrType = e.ClrType;
            return true;
        }
    }

    return false;
}

static async Task<bool> ExistsLocalAsync(SchoolDbContext db, Type clr, Guid id)
{
    var m = typeof(FullDrainHelpers).GetMethod(nameof(FullDrainHelpers.ExistsAsync))!.MakeGenericMethod(clr);
    return await (Task<bool>)m.Invoke(null, [db, id])!;
}

static async Task<int> CountAsync(SchoolDbContext db, Type clr, Guid id)
{
    var m = typeof(FullDrainHelpers).GetMethod(nameof(FullDrainHelpers.CountAsync))!.MakeGenericMethod(clr);
    return await (Task<int>)m.Invoke(null, [db, id])!;
}

static async Task<int> CountCloudAsync(SchoolDbContext db, string table, Guid id)
{
    if (!TryResolveClr(table, out var clr))
    {
        return -1;
    }

    return await CountAsync(db, clr, id);
}

static string Truncate(string? s, int max) =>
    string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max];

static class FullDrainHelpers
{
    public static async Task<bool> ExistsAsync<T>(SchoolDbContext db, Guid id) where T : AuditableEntity
        => await db.Set<T>().IgnoreQueryFilters().AnyAsync(e => e.Id == id);

    public static async Task<int> CountAsync<T>(SchoolDbContext db, Guid id) where T : AuditableEntity
        => await db.Set<T>().IgnoreQueryFilters().CountAsync(e => e.Id == id);
}
