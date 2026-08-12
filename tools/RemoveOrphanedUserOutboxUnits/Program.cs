using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Supprime (soft-delete) les 51 unités ORPHANED_LOCAL_ENTITY User* identifiées par l'analyse.
/// Aucune donnée métier touchée. ACTIF=0 obligatoire.
/// </summary>
var apiDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : @"d:\Mes Projet\ERP_Administration_Scolaire_2026\src\SchoolManagement.API\bin\Debug\net8.0";

var enc = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var cloudCfg = new CloudDatabaseConfigurationManager(apiDir, enc).LoadConfiguration();
Console.WriteLine($"=== RemoveOrphanedUserOutboxUnits ===");
Console.WriteLine($"ACTIF={cloudCfg.Actif}");
if (cloudCfg.Actif)
{
    Console.Error.WriteLine("ABORT: ACTIF=1.");
    return 3;
}

var localCs = factory.BuildConnectionString(new DatabaseConfigurationManager(apiDir, enc).LoadConfiguration());
var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());
var lo = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs, s => s.CommandTimeout(30)).Options;
var co = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs, s => s.CommandTimeout(60)).Options;

await using var local = new SchoolDbContext(lo) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };
await using var cloud = new SchoolDbContext(co) { SuppressCloudSyncEnqueue = true };

var targetTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "UserAccounts", "UserRoleAssignments", "UserPermissionExceptions"
};

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
    .ToListAsync();

candidates = candidates
    .Where(u => u.Items.Any(i => !i.IsDeleted && targetTables.Contains(i.TableName)))
    .ToList();

Console.WriteLine($"Candidats Failed User*: {candidates.Count}");

var toRemove = new List<SyncOutboxUnit>();
foreach (var unit in candidates)
{
    var primary = unit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.Sequence)
        .First(i => targetTables.Contains(i.TableName));

    var localExists = await EntityExistsLocalAsync(local, primary.TableName, primary.EntityId);
    if (localExists)
    {
        Console.WriteLine($"SKIP {unit.Id}: entité locale encore présente ({primary.TableName}/{primary.EntityId}).");
        continue;
    }

    var cloudCount = await CountCloudAsync(cloud, primary.TableName, primary.EntityId);
    if (cloudCount != 0)
    {
        Console.WriteLine($"SKIP {unit.Id}: cloud COUNT={cloudCount} (attendu 0).");
        continue;
    }

    toRemove.Add(unit);
}

Console.WriteLine($"Unités éligibles ORPHANED à supprimer: {toRemove.Count}");
if (toRemove.Count != 51)
{
    Console.Error.WriteLine($"ABORT: attendu 51 unités, trouvé {toRemove.Count}. Aucune suppression.");
    return 2;
}

var removeIds = toRemove.Select(u => u.Id).ToHashSet();
var pendingBeforeIds = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Pending)
    .Select(u => u.Id)
    .ToListAsync();

var now = DateTime.UtcNow;
foreach (var unit in toRemove)
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
Console.WriteLine($"Supprimées (soft-delete): {toRemove.Count}");

var after = await CountOutboxAsync();
Console.WriteLine($"APRÈS — Pending={after.Pending} Failed={after.Failed} Completed={after.Completed} TotalActive={after.TotalActive}");

var remainingRemoved = await local.SyncOutboxUnits
    .CountAsync(u => !u.IsDeleted && removeIds.Contains(u.Id));
Console.WriteLine($"Unités supprimées encore actives: {remainingRemoved} (attendu 0)");

var failedUserVerify = await local.SyncOutboxUnits
    .CountAsync(u => !u.IsDeleted
                     && u.Status == SyncOutboxStatus.Failed
                     && u.LastError != null
                     && u.LastError.Contains("Confirmation cloud")
                     && u.Items.Any(i => !i.IsDeleted && targetTables.Contains(i.TableName)));
Console.WriteLine($"Failed User* Confirmation cloud restants: {failedUserVerify} (attendu 0)");

var pendingAfterIds = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Pending)
    .Select(u => u.Id)
    .ToListAsync();
var pendingUnchanged = pendingBeforeIds.Count == pendingAfterIds.Count
                       && pendingBeforeIds.All(pendingAfterIds.Contains);
Console.WriteLine($"Pending inchangés: {pendingUnchanged} ({pendingBeforeIds.Count} → {pendingAfterIds.Count})");

var deltaTotal = before.TotalActive - after.TotalActive;
Console.WriteLine($"Delta TotalActive: {deltaTotal} (attendu 51)");

var actifFinal = new CloudDatabaseConfigurationManager(apiDir, enc).LoadConfiguration().Actif;
Console.WriteLine($"ACTIF final={actifFinal}");

var ok = remainingRemoved == 0
         && failedUserVerify == 0
         && pendingUnchanged
         && deltaTotal == 51
         && toRemove.Count == 51;
Console.WriteLine(ok ? "\nVÉRIFICATION OK." : "\nVÉRIFICATION ÉCHEC.");
return ok ? 0 : 1;

static async Task<bool> EntityExistsLocalAsync(SchoolDbContext db, string tableName, Guid id) =>
    tableName.ToUpperInvariant() switch
    {
        "USERACCOUNTS" => await db.Set<UserAccount>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "USERROLEASSIGNMENTS" => await db.Set<UserRoleAssignment>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "USERPERMISSIONEXCEPTIONS" => await db.Set<UserPermissionException>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        _ => false
    };

static async Task<int> CountCloudAsync(SchoolDbContext db, string tableName, Guid id) =>
    tableName.ToUpperInvariant() switch
    {
        "USERACCOUNTS" => await db.Set<UserAccount>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "USERROLEASSIGNMENTS" => await db.Set<UserRoleAssignment>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "USERPERMISSIONEXCEPTIONS" => await db.Set<UserPermissionException>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        _ => -1
    };
