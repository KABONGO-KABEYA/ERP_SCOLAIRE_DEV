using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Traite N unités AggregateType=Entity (défaut 5) — ACTIF=0, pas de drain global.
/// Completed uniquement après confirmation cloud EXISTS COUNT=1.
/// </summary>
var maxUnits = 5;
if (args.Length > 1 && int.TryParse(args[1], out var parsed))
{
    maxUnits = Math.Clamp(parsed, 1, 50);
}

var apiDir = args.Length > 0 && !int.TryParse(args[0], out _)
    ? args[0]
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "SchoolManagement.API", "bin", "Debug", "net8.0"));
apiDir = Path.GetFullPath(apiDir);

var encryption = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var cloudMgr = new CloudDatabaseConfigurationManager(apiDir, encryption);
var cloudCfg = cloudMgr.LoadConfiguration();
Console.WriteLine($"=== ProcessNEntityUnits N={maxUnits} ===");
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

var units = await local.SyncOutboxUnits
    .Include(u => u.Items)
    .Where(u => !u.IsDeleted && u.AggregateType == "Entity" && u.Status == SyncOutboxStatus.Pending)
    .OrderBy(u => u.Priority)
    .ThenBy(u => u.CreatedAt)
    .Take(maxUnits)
    .ToListAsync();

Console.WriteLine($"Unités Entity Pending sélectionnées: {units.Count}");
if (units.Count == 0)
{
    Console.WriteLine("Rien à traiter.");
    return 1;
}

var applyMethod = typeof(CloudSyncEngine)
    .GetMethod("ApplyItemAsync", BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("ApplyItemAsync introuvable");
var prefetchMethod = typeof(CloudSyncEngine)
    .GetMethod("PrefetchParentsForItemAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

var ok = 0;
var fail = 0;
var tableHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var batchSw = Stopwatch.StartNew();

for (var idx = 0; idx < units.Count; idx++)
{
    var unit = units[idx];
    var items = unit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.Sequence).ToList();
    Console.WriteLine($"\n########## [{idx + 1}/{units.Count}] Unit={unit.Id} ##########");
    Console.WriteLine($"AggregateType={unit.AggregateType} AggregateId={unit.AggregateId}");
    Console.WriteLine($"Priority={unit.Priority} ExpectedItemCount={unit.ExpectedItemCount} Items={items.Count}");

    await using var remote = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
    var unitSw = Stopwatch.StartNew();

    try
    {
        foreach (var i in items)
        {
            tableHits[i.TableName] = tableHits.GetValueOrDefault(i.TableName) + 1;
            Console.WriteLine($"\n--- Item seq={i.Sequence} ---");
            Console.WriteLine($"Type/Table={i.TableName}");
            Console.WriteLine($"EntityId={i.EntityId}");
            Console.WriteLine($"AggregateId == EntityId ? {unit.AggregateId == i.EntityId}");
            Console.WriteLine($"Operation outbox={i.Operation}");
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

        Console.WriteLine("\n--- PrefetchParents (hors TX) ---");
        foreach (var item in items.Where(i => i.Operation != SyncOperationType.Delete))
        {
            var sw = Stopwatch.StartNew();
            var task = (Task)prefetchMethod.Invoke(null, [local, remote, item, CancellationToken.None])!;
            await task;
            sw.Stop();
            Console.WriteLine($"  Prefetch {item.TableName}/{item.EntityId}: {sw.ElapsedMilliseconds} ms");
        }

        Console.WriteLine("--- BeginTransaction + Apply ---");
        await using var tx = await remote.Database.BeginTransactionAsync();
        var applied = 0;
        foreach (var item in items)
        {
            // EXISTS avant apply → Insert vs Update effectif
            var beforeApply = await CountOnCloudAsync(remote, item.TableName, item.EntityId);
            var effectiveOp = beforeApply > 0 ? "Update(upsert)" : "Insert(upsert)";
            Console.WriteLine($"  Apply start seq={item.Sequence} {item.TableName} effective={effectiveOp} cloudBefore={beforeApply}");

            var sw = Stopwatch.StartNew();
            var task = (Task)applyMethod.Invoke(null, [local, remote, item, CancellationToken.None])!;
            await task;
            await remote.SaveChangesAsync();
            foreach (var entry in remote.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }

            sw.Stop();
            applied++;
            Console.WriteLine($"  Apply SQL OK seq={item.Sequence} in {sw.ElapsedMilliseconds} ms");
        }

        var expected = unit.ExpectedItemCount > 0 ? unit.ExpectedItemCount : items.Count;
        if (applied != expected || applied != items.Count)
        {
            throw new InvalidOperationException($"Intégrité: attendu {expected}, traité {applied}/{items.Count}");
        }

        await tx.CommitAsync();
        unitSw.Stop();
        Console.WriteLine($"TX commit OK — durée unité: {unitSw.ElapsedMilliseconds} ms");

        Console.WriteLine("--- Confirmation Cloud ---");
        await using var verify = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
        var allConfirmed = true;
        foreach (var item in items)
        {
            var count = await CountOnCloudAsync(verify, item.TableName, item.EntityId);
            Console.WriteLine($"  AFTER {item.TableName}/{item.EntityId}: EXISTS={count > 0} COUNT={count}");
            if (count != 1)
            {
                allConfirmed = false;
                Console.WriteLine(count > 1 ? "  DOUBLON — pas Completed." : "  MISSING — pas Completed.");
            }
        }

        if (!allConfirmed)
        {
            unit.Status = SyncOutboxStatus.Failed;
            unit.LastError = "TX OK mais confirmation cloud EXISTS/COUNT échouée.";
            foreach (var item in items)
            {
                item.Status = SyncOutboxStatus.Failed;
                item.LastError = unit.LastError;
            }

            await local.SaveChangesAsync();
            fail++;
            Console.WriteLine($"Résultat [{idx + 1}]: Failed (confirmation cloud)");
            continue;
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
        ok++;
        Console.WriteLine($"Résultat [{idx + 1}]: Completed (cloud confirmé, pas de doublon) en {unitSw.ElapsedMilliseconds} ms");
    }
    catch (Exception ex)
    {
        unitSw.Stop();
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
        fail++;
        Console.WriteLine($"Résultat [{idx + 1}]: FAILED after {unitSw.ElapsedMilliseconds} ms");
        Console.WriteLine(Flatten(ex));
    }
}

batchSw.Stop();
Console.WriteLine($"\n=== Résumé N={maxUnits} ===");
Console.WriteLine($"OK Completed={ok} Failed={fail} durée batch={batchSw.ElapsedMilliseconds} ms");
var minutes = Math.Max(batchSw.Elapsed.TotalMinutes, 0.001);
Console.WriteLine($"Débit moyen: {ok / minutes:F2} unités Completed/min");
Console.WriteLine("Types/tables rencontrés:");
foreach (var kv in tableHits.OrderByDescending(k => k.Value).ThenBy(k => k.Key))
{
    Console.WriteLine($"  {kv.Key}: {kv.Value}");
}
Console.WriteLine($"ACTIF final={cloudMgr.LoadConfiguration().Actif}");

var statusLeft = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.AggregateType == "Entity"
                && (u.Status == SyncOutboxStatus.Pending
                    || u.Status == SyncOutboxStatus.InProgress
                    || u.Status == SyncOutboxStatus.Failed))
    .GroupBy(u => u.Status)
    .Select(g => new { Status = g.Key, Count = g.Count() })
    .ToListAsync();
foreach (var s in statusLeft.OrderBy(x => x.Status))
{
    Console.WriteLine($"Entity {s.Status}: {s.Count}");
}

var pendingLeft = await local.SyncOutboxUnits.CountAsync(u =>
    !u.IsDeleted && u.AggregateType == "Entity" && u.Status == SyncOutboxStatus.Pending);
Console.WriteLine($"Entity Pending restants (non drainés): {pendingLeft}");
return fail == 0 ? 0 : 2;

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
    else if (tableName.Equals("FinRetenue", StringComparison.OrdinalIgnoreCase))
    {
        var row = await local.Set<WithholdingType>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entityId);
        if (row is null)
        {
            Console.WriteLine("  Local: ABSENT");
        }
        else
        {
            Console.WriteLine($"  Local WithholdingType SchoolId={row.SchoolId} Code={row.Code} Name={row.Name}");
            Console.WriteLine($"  FK (racine referential) — SchoolId uniquement");
        }
    }
    else if (tableName.Equals("FinDestinationRepartition", StringComparison.OrdinalIgnoreCase))
    {
        var row = await local.Set<RevenueAllocationDestination>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entityId);
        if (row is null)
        {
            Console.WriteLine("  Local: ABSENT");
        }
        else
        {
            Console.WriteLine($"  Local Destination SchoolId={row.SchoolId} Code={row.Code} Name={row.Name}");
            Console.WriteLine($"  FK (racine referential) — SchoolId uniquement");
        }
    }
    else
    {
        await DescribeGenericLocalAsync(local, clr, entityId);
    }

    var before = await CountOnCloudAsync(remote, tableName, entityId);
    Console.WriteLine($"  Cloud BEFORE: EXISTS={before > 0} COUNT={before}");
}

static async Task DescribeGenericLocalAsync(SchoolDbContext local, Type clr, Guid entityId)
{
    var method = typeof(ProgramHelpers)
        .GetMethod(nameof(ProgramHelpers.TryLoadAsync), BindingFlags.Public | BindingFlags.Static)!
        .MakeGenericMethod(clr);
    var task = (Task<(bool found, string summary)>)method.Invoke(null, [local, entityId])!;
    var (found, summary) = await task;
    Console.WriteLine(found ? $"  Local: {summary}" : "  Local: ABSENT");
}

static Type ResolveClr(string tableName) => tableName.ToUpperInvariant() switch
{
    "CLASSFEEAMOUNTS" => typeof(ClassFeeAmount),
    "SCHOOLS" => typeof(School),
    "ACADEMICYEARS" => typeof(AcademicYear),
    "PEDAGOGICALCLASSES" => typeof(PedagogicalClass),
    "FEETYPES" => typeof(FeeType),
    "FEEINSTALLMENTS" => typeof(FeeInstallment),
    "FEEPRICINGCATEGORIES" => typeof(FeePricingCategory),
    "BANKS" => typeof(Bank),
    "FINRETENUE" => typeof(WithholdingType),
    "FINDESTINATIONREPARTITION" => typeof(RevenueAllocationDestination),
    "FINCLEREPARTITION" => typeof(RevenueAllocationKey),
    "FINCLEREPARTITIONDETAIL" => typeof(RevenueAllocationKeyDetail),
    "FINRETENUECONFIGURATION" => typeof(WithholdingConfiguration),
    "ADRESSE" => typeof(PostalAddress),
    "STUDENTS" => typeof(Student),
    "GUARDIANS" => typeof(Guardian),
    "STUDENTGUARDIANS" => typeof(StudentGuardian),
    "USERACCOUNTS" => typeof(UserAccount),
    "USERROLEASSIGNMENTS" => typeof(UserRoleAssignment),
    "ROLEPERMISSIONS" => typeof(RolePermission),
    _ => throw new InvalidOperationException($"Table non mappée ResolveClr: {tableName}")
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

    public static async Task<(bool found, string summary)> TryLoadAsync<T>(SchoolDbContext local, Guid entityId)
        where T : AuditableEntity
    {
        var row = await local.Set<T>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entityId);
        if (row is null)
        {
            return (false, "");
        }

        var fkParts = typeof(T).GetProperties()
            .Where(p => p.Name.EndsWith("Id", StringComparison.Ordinal)
                        && (p.PropertyType == typeof(Guid) || p.PropertyType == typeof(Guid?))
                        && p.Name is not "Id")
            .Select(p => $"{p.Name}={p.GetValue(row)}")
            .Take(12);
        return (true, $"{typeof(T).Name} {string.Join(" ", fkParts)}");
    }
}
