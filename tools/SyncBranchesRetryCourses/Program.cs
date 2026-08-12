using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// 1) Upsert les 10 Branches des 39 Courses Failed.
/// 2) Retente les 39 Courses Failed.
/// ACTIF=0, pas de drain, pas de modif moteur.
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
Console.WriteLine($"=== SyncBranchesThenRetryCourses ===");
Console.WriteLine($"ACTIF={cloudCfg.Actif}");
if (cloudCfg.Actif)
{
    Console.Error.WriteLine("ABORT: ACTIF=1");
    return 3;
}

var localCs = factory.BuildConnectionString(new DatabaseConfigurationManager(apiDir, encryption).LoadConfiguration());
var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());
var localOpts = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs, s => s.CommandTimeout(30)).Options;
var cloudOpts = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs, s => s.CommandTimeout(120)).Options;

await using var local = new SchoolDbContext(localOpts) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };

var failedUnits = await local.SyncOutboxUnits
    .Include(u => u.Items)
    .Where(u => !u.IsDeleted && u.AggregateType == "Entity" && u.Status == SyncOutboxStatus.Failed)
    .OrderBy(u => u.CreatedAt)
    .ToListAsync();

var courseItems = failedUnits
    .SelectMany(u => u.Items.Where(i => !i.IsDeleted && i.TableName == "Courses")
        .Select(i => (Unit: u, Item: i)))
    .ToList();

Console.WriteLine($"Failed Entity units={failedUnits.Count} Courses items={courseItems.Count}");

var courseIds = courseItems.Select(x => x.Item.EntityId).Distinct().ToList();
var courses = await local.Set<Course>().IgnoreQueryFilters().AsNoTracking()
    .Where(c => courseIds.Contains(c.Id)).ToListAsync();
var schoolIds = courses.Select(c => c.SchoolId).Distinct().ToList();
Console.WriteLine($"SchoolId distinct courses: {string.Join(", ", schoolIds)}");

var branchIds = courses.Where(c => c.BranchId.HasValue).Select(c => c.BranchId!.Value).Distinct().OrderBy(x => x).ToList();
Console.WriteLine($"Branches distinctes à sync: {branchIds.Count}");

var batchSw = Stopwatch.StartNew();
var branchesOk = 0;
var branchesFail = 0;
var branchDup = 0;

Console.WriteLine("\n========== PHASE 1: Branches ==========");
foreach (var branchId in branchIds)
{
    try
    {
        var localBranch = await local.Set<Branch>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId)
            ?? throw new InvalidOperationException($"Branch locale absente {branchId}");

        Console.WriteLine($"\nBranch {branchId}");
        Console.WriteLine($"  SchoolId={localBranch.SchoolId} Code={localBranch.Code} Name={localBranch.Name}");

        await using var cloud = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
        var before = await cloud.Set<Branch>().IgnoreQueryFilters().AsNoTracking()
            .CountAsync(b => b.Id == branchId);
        Console.WriteLine($"  Cloud BEFORE COUNT={before}");

        // Ensure School parent if missing
        var schoolExists = await cloud.Set<School>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(s => s.Id == localBranch.SchoolId);
        if (!schoolExists)
        {
            var school = await local.Set<School>().IgnoreQueryFilters().AsNoTracking()
                .FirstAsync(s => s.Id == localBranch.SchoolId);
            await UpsertScalarAsync(cloud, school, remoteExists: false);
            await cloud.SaveChangesAsync();
            foreach (var e in cloud.ChangeTracker.Entries().ToList())
            {
                e.State = EntityState.Detached;
            }

            Console.WriteLine($"  School {localBranch.SchoolId} upserté");
        }

        await UpsertScalarAsync(cloud, localBranch, remoteExists: before > 0);
        await cloud.SaveChangesAsync();

        await using var verify = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
        var after = await verify.Set<Branch>().IgnoreQueryFilters().AsNoTracking()
            .CountAsync(b => b.Id == branchId);
        var schoolOk = await verify.Set<Branch>().IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => b.SchoolId)
            .FirstAsync();
        Console.WriteLine($"  Cloud AFTER COUNT={after} SchoolId={schoolOk}");
        if (after != 1)
        {
            if (after > 1)
            {
                branchDup++;
            }

            throw new InvalidOperationException($"Confirmation Branch COUNT={after}");
        }

        if (schoolOk != localBranch.SchoolId)
        {
            throw new InvalidOperationException($"SchoolId mismatch cloud={schoolOk} local={localBranch.SchoolId}");
        }

        branchesOk++;
        Console.WriteLine("  → Branch OK");
    }
    catch (Exception ex)
    {
        branchesFail++;
        Console.WriteLine($"  → Branch FAIL: {Flatten(ex)}");
    }
}

Console.WriteLine($"\nBranches résumé: {branchesOk}/{branchIds.Count} OK, fail={branchesFail}, dup={branchDup}");

Console.WriteLine("\n========== PHASE 2: Retry Courses ==========");
var applyMethod = typeof(CloudSyncEngine)
    .GetMethod("ApplyItemAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
var prefetchMethod = typeof(CloudSyncEngine)
    .GetMethod("PrefetchParentsForItemAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

var coursesOk = 0;
var coursesFail = 0;
var courseDup = 0;
var newFkErrors = new List<string>();

foreach (var (unit, item) in courseItems)
{
    Console.WriteLine($"\n--- Course Unit={unit.Id} EntityId={item.EntityId} ---");
    try
    {
        var course = courses.FirstOrDefault(c => c.Id == item.EntityId)
            ?? await local.Set<Course>().IgnoreQueryFilters().AsNoTracking()
                .FirstAsync(c => c.Id == item.EntityId);

        Console.WriteLine($"  SchoolId={course.SchoolId} BranchId={course.BranchId} Code={course.Code}");

        await using var remote = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
        if (course.BranchId is Guid bid)
        {
            var branchCloud = await remote.Set<Branch>().IgnoreQueryFilters().AsNoTracking()
                .CountAsync(b => b.Id == bid);
            Console.WriteLine($"  Branch cloud COUNT={branchCloud}");
            if (branchCloud != 1)
            {
                throw new InvalidOperationException($"Branch {bid} cloud COUNT={branchCloud} — skip Course");
            }
        }

        unit.Status = SyncOutboxStatus.InProgress;
        unit.AttemptCount++;
        unit.LastAttemptAt = DateTime.UtcNow;
        unit.LastError = null;
        item.Status = SyncOutboxStatus.InProgress;
        item.LastError = null;
        await local.SaveChangesAsync();

        var sw = Stopwatch.StartNew();
        if (item.Operation != SyncOperationType.Delete)
        {
            var pref = (Task)prefetchMethod.Invoke(null, [local, remote, item, CancellationToken.None])!;
            await pref;
        }

        await using var tx = await remote.Database.BeginTransactionAsync();
        var apply = (Task)applyMethod.Invoke(null, [local, remote, item, CancellationToken.None])!;
        await apply;
        await remote.SaveChangesAsync();
        foreach (var e in remote.ChangeTracker.Entries().ToList())
        {
            e.State = EntityState.Detached;
        }

        await tx.CommitAsync();
        sw.Stop();
        Console.WriteLine($"  TX OK {sw.ElapsedMilliseconds} ms");

        await using var verify = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
        var count = await verify.Set<Course>().IgnoreQueryFilters().AsNoTracking()
            .CountAsync(c => c.Id == item.EntityId);
        Console.WriteLine($"  Cloud AFTER COUNT={count}");
        if (count != 1)
        {
            if (count > 1)
            {
                courseDup++;
            }

            throw new InvalidOperationException($"Confirmation Course COUNT={count}");
        }

        unit.Status = SyncOutboxStatus.Completed;
        unit.CompletedAt = DateTime.UtcNow;
        unit.LastError = null;
        item.Status = SyncOutboxStatus.Completed;
        item.LastError = null;
        await local.SaveChangesAsync();
        coursesOk++;
        Console.WriteLine("  → Completed");
    }
    catch (Exception ex)
    {
        var msg = Flatten(ex);
        unit.Status = SyncOutboxStatus.Failed;
        unit.LastError = Truncate(msg, 2000);
        item.Status = SyncOutboxStatus.Failed;
        item.LastError = unit.LastError;
        await local.SaveChangesAsync();
        coursesFail++;
        if (msg.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
            && !msg.Contains("FK_Courses_Branches", StringComparison.OrdinalIgnoreCase))
        {
            newFkErrors.Add($"{item.EntityId}: {msg}");
        }

        Console.WriteLine($"  → Failed: {msg}");
    }
}

batchSw.Stop();

var pending = await local.SyncOutboxUnits.CountAsync(u =>
    !u.IsDeleted && u.AggregateType == "Entity" && u.Status == SyncOutboxStatus.Pending);
var failed = await local.SyncOutboxUnits.CountAsync(u =>
    !u.IsDeleted && u.AggregateType == "Entity" && u.Status == SyncOutboxStatus.Failed);

Console.WriteLine("\n=== BILAN ===");
Console.WriteLine($"Branches: {branchesOk}/{branchIds.Count} Completed (fail={branchesFail}, dup incidents={branchDup})");
Console.WriteLine($"Courses: {coursesOk}/{courseItems.Count} Completed (fail={coursesFail}, dup incidents={courseDup})");
Console.WriteLine($"Failed Entity restants: {failed}");
Console.WriteLine($"Pending Entity restants: {pending}");
Console.WriteLine($"Durée totale: {batchSw.ElapsedMilliseconds} ms");
Console.WriteLine($"SchoolIds courses: {string.Join(", ", schoolIds)}");
Console.WriteLine($"Nouvelles FK (hors Branches): {newFkErrors.Count}");
foreach (var e in newFkErrors.Take(10))
{
    Console.WriteLine($"  {e}");
}

Console.WriteLine($"ACTIF final={cloudMgr.LoadConfiguration().Actif}");
return coursesFail == 0 && branchesFail == 0 ? 0 : 2;

static async Task UpsertScalarAsync<T>(SchoolDbContext remote, T source, bool remoteExists)
    where T : class, new()
{
    var stub = new T();
    var entry = remote.Entry(stub);
    entry.CurrentValues.SetValues(source);
    foreach (var nav in entry.Navigations)
    {
        if (nav.Metadata.IsCollection)
        {
            nav.CurrentValue = null;
        }
    }

    if (remoteExists)
    {
        entry.State = EntityState.Modified;
        entry.Property("Id").IsModified = false;
    }
    else
    {
        entry.State = EntityState.Added;
    }

    await Task.CompletedTask;
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
