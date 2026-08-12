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
/// Vérifie puis soft-delete les unités Failed ORPHANED_LOCAL_ENTITY (Confirmation cloud COUNT=0).
/// Mode lecture seule par défaut ; --apply pour exécuter la suppression.
/// </summary>
var apply = args.Contains("--apply", StringComparer.OrdinalIgnoreCase);
var expectedCount = 458;
var countArg = args.FirstOrDefault(a => a.StartsWith("--expected=", StringComparison.OrdinalIgnoreCase));
if (countArg is not null && int.TryParse(countArg["--expected=".Length..], out var ec))
{
    expectedCount = ec;
}

var apiDir = args.FirstOrDefault(a => !a.StartsWith('-'))
    ?? @"d:\Mes Projet\ERP_Administration_Scolaire_2026\src\SchoolManagement.API\bin\Debug\net8.0";
apiDir = Path.GetFullPath(apiDir);

var enc = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var cloudCfg = new CloudDatabaseConfigurationManager(apiDir, enc).LoadConfiguration();
Console.WriteLine($"=== RemoveOrphanedFailedOutboxUnits {(apply ? "APPLY" : "VERIFY-ONLY")} ===");
Console.WriteLine($"ACTIF={cloudCfg.Actif}");
if (cloudCfg.Actif)
{
    Console.Error.WriteLine("ABORT: ACTIF=1.");
    return 3;
}

var localCs = factory.BuildConnectionString(new DatabaseConfigurationManager(apiDir, enc).LoadConfiguration());
var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());
await using var local = new SchoolDbContext(
    new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs, s => s.CommandTimeout(30)).Options)
{ SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };
await using var cloud = new SchoolDbContext(
    new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs, s => s.CommandTimeout(60)).Options)
{ SuppressCloudSyncEnqueue = true };

async Task<(int Pending, int Failed, int Completed, int TotalActive)> CountOutboxAsync()
{
    var pending = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Pending);
    var failed = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Failed);
    var completed = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Completed);
    var total = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted);
    return (pending, failed, completed, total);
}

var before = await CountOutboxAsync();
Console.WriteLine($"AVANT — Pending={before.Pending} Failed={before.Failed} Completed={before.Completed} TotalActive={before.TotalActive}");

var candidates = await local.SyncOutboxUnits
    .Include(u => u.Items)
    .Where(u => !u.IsDeleted
                && u.Status == SyncOutboxStatus.Failed
                && u.LastError != null
                && u.LastError.Contains("Confirmation cloud"))
    .OrderBy(u => u.CreatedAt)
    .ToListAsync();

Console.WriteLine($"Candidats Failed Confirmation cloud: {candidates.Count} (attendu {expectedCount})");
if (candidates.Count != expectedCount)
{
    Console.Error.WriteLine("ABORT: nombre de Failed inattendu.");
    return 2;
}

var orphaned = new List<SyncOutboxUnit>();
var rejected = new List<string>();

foreach (var unit in candidates)
{
    var items = unit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.Sequence).ToList();
    if (items.Count == 0)
    {
        rejected.Add($"{unit.Id}: aucun item actif");
        continue;
    }

    var primaryTable = items[0].TableName;
    var unitOk = true;

    foreach (var item in items)
    {
        if (!TryResolveClrType(item.TableName, out var clrType))
        {
            rejected.Add($"{unit.Id}: table non mappée {item.TableName}");
            unitOk = false;
            break;
        }

        if (await EntityExistsAsync(local, clrType, item.EntityId))
        {
            rejected.Add($"{unit.Id}: EntityId local présent {item.TableName}/{item.EntityId}");
            unitOk = false;
            break;
        }

        var cloudCount = await CountEntityAsync(cloud, clrType, item.EntityId);
        if (cloudCount != 0)
        {
            rejected.Add($"{unit.Id}: cloud COUNT={cloudCount} {item.TableName}/{item.EntityId}");
            unitOk = false;
            break;
        }
    }

    if (!unitOk)
    {
        continue;
    }

    if (unit.AggregateId is Guid aggId)
    {
        if (!TryResolveClrType(primaryTable, out var aggClr))
        {
            rejected.Add($"{unit.Id}: aggregate table non mappée {primaryTable}");
            continue;
        }

        if (aggId != items[0].EntityId && await EntityExistsAsync(local, aggClr, aggId))
        {
            rejected.Add($"{unit.Id}: AggregateId local présent {primaryTable}/{aggId}");
            continue;
        }

        if (await CountEntityAsync(cloud, aggClr, aggId) != 0)
        {
            rejected.Add($"{unit.Id}: AggregateId cloud COUNT!=0 {primaryTable}/{aggId}");
            continue;
        }
    }

    orphaned.Add(unit);
}

Console.WriteLine($"\n=== VÉRIFICATION ORPHANED ===");
Console.WriteLine($"ORPHANED_LOCAL_ENTITY: {orphaned.Count}/{expectedCount}");
Console.WriteLine($"Rejetées: {rejected.Count}");

if (rejected.Count > 0)
{
    Console.WriteLine("Exemples rejet:");
    foreach (var r in rejected.Take(15))
    {
        Console.WriteLine($"  {r}");
    }

    Console.Error.WriteLine("ABORT: toutes les unités ne sont pas ORPHANED.");
    return 2;
}

if (orphaned.Count != expectedCount)
{
    Console.Error.WriteLine("ABORT: count orphaned != expected.");
    return 2;
}

Console.WriteLine("Toutes les unités respectent: EntityId absent, AggregateId absent, Cloud COUNT=0.");

if (!apply)
{
    Console.WriteLine("\nLecture seule terminée. Relancer avec --apply pour soft-delete.");
    return 0;
}

var removeIds = orphaned.Select(u => u.Id).ToHashSet();
var pendingBefore = before.Pending;
var completedBefore = before.Completed;
var totalBefore = before.TotalActive;

var now = DateTime.UtcNow;
foreach (var unit in orphaned)
{
    unit.IsDeleted = true;
    unit.DeletedAt = now;
    foreach (var item in unit.Items.Where(i => !i.IsDeleted))
    {
        item.IsDeleted = true;
        item.DeletedAt = now;
    }
}

await local.SaveChangesAsync();
Console.WriteLine($"\nSoft-deleted: {orphaned.Count}");

var after = await CountOutboxAsync();
Console.WriteLine($"APRÈS — Pending={after.Pending} Failed={after.Failed} Completed={after.Completed} TotalActive={after.TotalActive}");

var stillActive = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && removeIds.Contains(u.Id));
var delta = totalBefore - after.TotalActive;

var ok = after.Failed == 0
         && after.Pending == pendingBefore
         && after.Completed == completedBefore
         && stillActive == 0
         && delta == expectedCount
         && orphaned.Count == expectedCount;

Console.WriteLine($"Unités supprimées encore actives: {stillActive} (attendu 0)");
Console.WriteLine($"Delta TotalActive: {delta} (attendu {expectedCount})");
Console.WriteLine($"ACTIF final={new CloudDatabaseConfigurationManager(apiDir, enc).LoadConfiguration().Actif}");
Console.WriteLine(ok ? "\nVÉRIFICATION POST-SUPPRESSION OK." : "\nVÉRIFICATION POST-SUPPRESSION ÉCHEC.");
return ok ? 0 : 1;

static bool TryResolveClrType(string tableName, out Type clrType)
{
    clrType = null!;
    var catalogType = typeof(CloudSyncEngine).Assembly
        .GetType("SchoolManagement.Infrastructure.CloudSync.CloudSyncCatalog", throwOnError: true)!;
    var syncOrderObj = catalogType.GetField("SyncOrder", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
    var syncOrder = (IReadOnlyList<(string Table, Type ClrType)>)syncOrderObj!;
    foreach (var entry in syncOrder)
    {
        if (entry.Table.Equals(tableName, StringComparison.OrdinalIgnoreCase))
        {
            clrType = entry.ClrType;
            return true;
        }
    }

    return false;
}

static async Task<bool> EntityExistsAsync(SchoolDbContext db, Type clrType, Guid id)
{
    var method = typeof(OrphanHelpers).GetMethod(nameof(OrphanHelpers.ExistsAsync), BindingFlags.Public | BindingFlags.Static)!
        .MakeGenericMethod(clrType);
    var task = (Task<bool>)method.Invoke(null, [db, id])!;
    return await task;
}

static async Task<int> CountEntityAsync(SchoolDbContext db, Type clrType, Guid id)
{
    var method = typeof(OrphanHelpers).GetMethod(nameof(OrphanHelpers.CountAsync), BindingFlags.Public | BindingFlags.Static)!
        .MakeGenericMethod(clrType);
    var task = (Task<int>)method.Invoke(null, [db, id])!;
    return await task;
}

static class OrphanHelpers
{
    public static async Task<bool> ExistsAsync<T>(SchoolDbContext db, Guid id)
        where T : AuditableEntity
        => await db.Set<T>().IgnoreQueryFilters().AsNoTracking().AnyAsync(e => e.Id == id);

    public static async Task<int> CountAsync<T>(SchoolDbContext db, Guid id)
        where T : AuditableEntity
        => await db.Set<T>().IgnoreQueryFilters().AsNoTracking().CountAsync(e => e.Id == id);
}
