using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

var apiDir = @"d:\Mes Projet\ERP_Administration_Scolaire_2026\src\SchoolManagement.API\bin\Debug\net8.0";
var enc = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var cloudCfg = new CloudDatabaseConfigurationManager(apiDir, enc).LoadConfiguration();
var localCs = factory.BuildConnectionString(new DatabaseConfigurationManager(apiDir, enc).LoadConfiguration());
await using var local = new SchoolDbContext(
    new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs).Options)
{
    SuppressCloudSyncEnqueue = true,
    IgnoreSchoolScope = true
};

var pending = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Pending);
var failed = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Failed);
var completed = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Completed);
var inProgress = await local.SyncOutboxUnits.CountAsync(u => !u.IsDeleted && u.Status == SyncOutboxStatus.InProgress);
var pendingRetry = await local.SyncOutboxUnits.CountAsync(u =>
    !u.IsDeleted && u.Status == SyncOutboxStatus.Pending && u.AttemptCount > 0);

Console.WriteLine($"ACTIF={cloudCfg.Actif}");
Console.WriteLine($"Pending={pending} Failed={failed} Completed={completed} InProgress={inProgress} PendingRetry={pendingRetry}");

var failedByError = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Failed)
    .GroupBy(u => u.LastError != null && u.LastError.Contains("Confirmation cloud") ? "Confirmation cloud COUNT=0" :
                  u.LastError != null && u.LastError.Contains("FOREIGN KEY") ? "FK" :
                  u.LastError != null && u.LastError.Contains("timeout") ? "Timeout" : "Other")
    .Select(g => new { Key = g.Key, Count = g.Count() })
    .ToListAsync();
foreach (var g in failedByError.OrderByDescending(x => x.Count))
    Console.WriteLine($"Failed[{g.Key}]={g.Count}");

var failedTables = await local.SyncOutboxItems
    .Where(i => !i.IsDeleted && i.Unit != null && !i.Unit.IsDeleted && i.Unit.Status == SyncOutboxStatus.Failed)
    .GroupBy(i => i.TableName)
    .Select(g => new { Table = g.Key, Count = g.Count() })
    .OrderByDescending(x => x.Count)
    .Take(15)
    .ToListAsync();
Console.WriteLine("Failed tables:");
foreach (var t in failedTables)
    Console.WriteLine($"  {t.Table}: {t.Count}");

var completedDelta = completed - 951;
var failedDelta = failed - 0;
var pendingDelta = pending - 908;
Console.WriteLine($"Delta vs avant drain: Completed+{completedDelta} Failed+{failedDelta} Pending{pendingDelta}");

var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());
await using var cloud = new SchoolDbContext(
    new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs).Options)
{ SuppressCloudSyncEnqueue = true };

var cutoff = DateTime.UtcNow.AddMinutes(-25);
var batch = await local.SyncOutboxUnits
    .Include(u => u.Items)
    .Where(u => !u.IsDeleted && u.LastAttemptAt >= cutoff)
    .ToListAsync();

Console.WriteLine($"\n=== Batch 500 (LastAttemptAt >= {cutoff:O}) ===");
Console.WriteLine($"Units batch: {batch.Count}");
var batchCompleted = batch.Count(u => u.Status == SyncOutboxStatus.Completed);
var batchFailed = batch.Count(u => u.Status == SyncOutboxStatus.Failed);
var batchPendingRetry = batch.Count(u => u.Status == SyncOutboxStatus.Pending && u.AttemptCount > 0);
Console.WriteLine($"Completed={batchCompleted} Failed={batchFailed} PendingRetry={batchPendingRetry}");

var tableHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
foreach (var u in batch)
{
    foreach (var i in u.Items.Where(x => !x.IsDeleted))
    {
        tableHits[i.TableName] = tableHits.GetValueOrDefault(i.TableName) + 1;
    }
}

Console.WriteLine("Tables batch:");
foreach (var kv in tableHits.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
{
    Console.WriteLine($"  {kv.Key}: {kv.Value}");
}

var orphan = 0;
var existsLocal = 0;
foreach (var u in batch.Where(x => x.Status == SyncOutboxStatus.Failed))
{
    var item = u.Items.First(i => !i.IsDeleted);
    if (await ExistsLocalAsync(local, item.TableName, item.EntityId))
    {
        existsLocal++;
    }
    else
    {
        orphan++;
    }
}

Console.WriteLine($"Failed: orphanLocal={orphan} localExists={existsLocal}");

var dup = 0;
foreach (var u in batch.Where(x => x.Status == SyncOutboxStatus.Completed))
{
    foreach (var item in u.Items.Where(i => !i.IsDeleted && i.Operation != SyncOperationType.Delete))
    {
        var c = await CountCloudAsync(cloud, item.TableName, item.EntityId);
        if (c != 1)
        {
            dup++;
        }
    }
}

Console.WriteLine($"Doublons/missing Completed batch: {dup}");

static async Task<bool> ExistsLocalAsync(SchoolDbContext db, string table, Guid id) =>
    table.ToUpperInvariant() switch
    {
        "USERACCOUNTS" => await db.Set<UserAccount>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "USERROLEASSIGNMENTS" => await db.Set<UserRoleAssignment>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "USERPERMISSIONEXCEPTIONS" => await db.Set<UserPermissionException>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "ROLEPERMISSIONS" => await db.Set<RolePermission>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "PERMISSIONDEPENDENCIES" => await db.Set<PermissionDependency>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "SECURITYMODULES" => await db.Set<SecurityModule>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "SECURITYFUNCTIONS" => await db.Set<SecurityFunction>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "SECURITYPAGES" => await db.Set<SecurityPage>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        "SECURITYACTIONS" => await db.Set<SecurityAction>().IgnoreQueryFilters().AnyAsync(e => e.Id == id),
        _ => false
    };

static async Task<int> CountCloudAsync(SchoolDbContext db, string table, Guid id) =>
    table.ToUpperInvariant() switch
    {
        "USERACCOUNTS" => await db.Set<UserAccount>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "USERROLEASSIGNMENTS" => await db.Set<UserRoleAssignment>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "USERPERMISSIONEXCEPTIONS" => await db.Set<UserPermissionException>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "ROLEPERMISSIONS" => await db.Set<RolePermission>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "PERMISSIONDEPENDENCIES" => await db.Set<PermissionDependency>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "SECURITYMODULES" => await db.Set<SecurityModule>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "SECURITYFUNCTIONS" => await db.Set<SecurityFunction>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "SECURITYPAGES" => await db.Set<SecurityPage>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "SECURITYACTIONS" => await db.Set<SecurityAction>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        _ => -1
    };
