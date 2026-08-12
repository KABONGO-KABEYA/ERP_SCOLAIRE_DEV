using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Retry ciblé des 3 Failed (2 Adresse + 1 StudentGuardians) :
/// sync parents manquants → confirmation → retente enfant → Completed seulement si cloud OK.
/// ACTIF doit rester 0.
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

var targetUnitIds = new[]
{
    Guid.Parse("13F8E04D-25CB-41FB-B861-FB60AE6DFFD0"), // Adresse
    Guid.Parse("A4DCFC70-3F64-45EA-8412-65A53157FEBF"), // Adresse
    Guid.Parse("D3FFA56D-16FF-4DA5-9A96-5DBEC56FAA33"), // StudentGuardians
};

var applyMethod = typeof(CloudSyncEngine)
    .GetMethod("ApplyItemAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
var prefetchMethod = typeof(CloudSyncEngine)
    .GetMethod("PrefetchParentsForItemAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

var ok = 0;
var fail = 0;

foreach (var unitId in targetUnitIds)
{
    var unit = await local.SyncOutboxUnits.Include(u => u.Items)
        .FirstAsync(u => u.Id == unitId);
    var item = unit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.Sequence).First();
    Console.WriteLine($"\n########## RETRY Unit={unit.Id} Status={unit.Status} {item.TableName}/{item.EntityId} ##########");

    await using var remote = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };

    try
    {
        if (item.TableName.Equals("Adresse", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureGeographyChainAsync(local, remote, cloudOpts, item.EntityId);
        }
        else if (item.TableName.Equals("StudentGuardians", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureGuardianForLinkAsync(local, remote, cloudOpts, item.EntityId);
        }

        // Retry unité via même pipeline ProcessUnit
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
        Console.WriteLine($"TX commit OK en {sw.ElapsedMilliseconds} ms");

        await using var verify = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
        var count = item.TableName.Equals("Adresse", StringComparison.OrdinalIgnoreCase)
            ? await verify.Set<PostalAddress>().IgnoreQueryFilters().AsNoTracking().CountAsync(a => a.Id == item.EntityId)
            : await verify.Set<StudentGuardian>().IgnoreQueryFilters().AsNoTracking().CountAsync(a => a.Id == item.EntityId);

        Console.WriteLine($"Cloud AFTER COUNT={count}");
        if (count != 1)
        {
            throw new InvalidOperationException($"Confirmation cloud échouée COUNT={count}");
        }

        unit.Status = SyncOutboxStatus.Completed;
        unit.CompletedAt = DateTime.UtcNow;
        unit.LastError = null;
        item.Status = SyncOutboxStatus.Completed;
        item.LastError = null;
        await local.SaveChangesAsync();
        ok++;
        Console.WriteLine("→ Completed (cloud confirmé)");
    }
    catch (Exception ex)
    {
        unit.Status = SyncOutboxStatus.Failed;
        unit.LastError = Truncate(Flatten(ex), 2000);
        item.Status = SyncOutboxStatus.Failed;
        item.LastError = unit.LastError;
        await local.SaveChangesAsync();
        fail++;
        Console.WriteLine($"→ Failed: {Flatten(ex)}");
    }
}

var pending = await local.SyncOutboxUnits.CountAsync(u =>
    !u.IsDeleted && u.AggregateType == "Entity" && u.Status == SyncOutboxStatus.Pending);
var failed = await local.SyncOutboxUnits.CountAsync(u =>
    !u.IsDeleted && u.AggregateType == "Entity" && u.Status == SyncOutboxStatus.Failed);
Console.WriteLine($"\n=== Résumé retries: OK={ok} Fail={fail} ===");
Console.WriteLine($"Entity Pending={pending} Failed={failed}");
Console.WriteLine($"ACTIF final={cloudMgr.LoadConfiguration().Actif}");
return fail == 0 ? 0 : 2;

static async Task EnsureGeographyChainAsync(
    SchoolDbContext local,
    SchoolDbContext remote,
    DbContextOptions<SchoolDbContext> cloudOpts,
    Guid addressId)
{
    var addr = await local.Set<PostalAddress>().IgnoreQueryFilters().AsNoTracking()
        .FirstOrDefaultAsync(a => a.Id == addressId)
        ?? throw new InvalidOperationException($"Adresse locale absente {addressId}");

    Console.WriteLine($"Adresse CountryId={addr.CountryId} ProvinceId={addr.ProvinceId} CityId={addr.CityId} CommuneId={addr.CommuneId}");

    // Chaîne parents géo : Pays → Province → Ville → Commune
    await UpsertIfMissingAsync<Country>(local, cloudOpts, addr.CountryId, "Pays/Country");
    await UpsertIfMissingAsync<Province>(local, cloudOpts, addr.ProvinceId, "Province");
    await UpsertIfMissingAsync<City>(local, cloudOpts, addr.CityId, "Ville/City");
    await UpsertIfMissingAsync<Commune>(local, cloudOpts, addr.CommuneId, "Commune");
}

static async Task EnsureGuardianForLinkAsync(
    SchoolDbContext local,
    SchoolDbContext remote,
    DbContextOptions<SchoolDbContext> cloudOpts,
    Guid linkId)
{
    var link = await local.Set<StudentGuardian>().IgnoreQueryFilters().AsNoTracking()
        .FirstAsync(x => x.Id == linkId);
    Console.WriteLine($"StudentGuardian StudentId={link.StudentId} GuardianId={link.GuardianId}");

    var gCount = await remote.Set<Guardian>().IgnoreQueryFilters().AsNoTracking()
        .CountAsync(g => g.Id == link.GuardianId);
    Console.WriteLine($"Guardian cloud BEFORE COUNT={gCount}");
    if (gCount == 0)
    {
        // Si Guardian a une Adresse, tenter chaîne géo puis Guardian
        var guardian = await local.Set<Guardian>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == link.GuardianId)
            ?? throw new InvalidOperationException($"Guardian local absent {link.GuardianId}");
        if (guardian.AddressId is Guid aid && aid != Guid.Empty)
        {
            try
            {
                await EnsureGeographyChainAsync(local, remote, cloudOpts, aid);
                await UpsertIfMissingAsync<PostalAddress>(local, cloudOpts, aid, "Adresse");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Note: Adresse guardian non poussée ({Flatten(ex)}) — upsert Guardian avec AddressId null si besoin.");
            }
        }

        await UpsertGuardianAsync(local, cloudOpts, guardian);
        await using var v = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
        gCount = await v.Set<Guardian>().IgnoreQueryFilters().AsNoTracking()
            .CountAsync(g => g.Id == link.GuardianId);
        Console.WriteLine($"Guardian cloud AFTER ensure COUNT={gCount}");
        if (gCount != 1)
        {
            throw new InvalidOperationException("Guardian toujours absent cloud après ensure.");
        }
    }
    else
    {
        Console.WriteLine("Guardian déjà présent cloud — retry lien seul.");
    }

    var sCount = await remote.Set<Student>().IgnoreQueryFilters().AsNoTracking()
        .CountAsync(s => s.Id == link.StudentId);
    Console.WriteLine($"Student cloud COUNT={sCount}");
    if (sCount == 0)
    {
        throw new InvalidOperationException($"Student parent absent cloud: {link.StudentId}");
    }
}

static async Task UpsertIfMissingAsync<T>(
    SchoolDbContext local,
    DbContextOptions<SchoolDbContext> cloudOpts,
    Guid? id,
    string label)
    where T : AuditableEntity, new()
{
    if (id is null || id == Guid.Empty)
    {
        Console.WriteLine($"  {label}: (null) skip");
        return;
    }

    await using var check = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
    var exists = await check.Set<T>().IgnoreQueryFilters().AsNoTracking()
        .AnyAsync(e => e.Id == id.Value);
    Console.WriteLine($"  {label} {id}: cloud EXISTS={exists}");
    if (exists)
    {
        return;
    }

    var localRow = await local.Set<T>().IgnoreQueryFilters().AsNoTracking()
        .FirstOrDefaultAsync(e => e.Id == id.Value);
    if (localRow is null)
    {
        throw new InvalidOperationException($"{label} local absent: {id}");
    }

    // Province/City/Commune: parents plus haut déjà poussés
    await using var write = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
    var stub = new T();
    var entry = write.Entry(stub);
    entry.CurrentValues.SetValues(localRow);
    foreach (var nav in entry.Navigations)
    {
        if (nav.Metadata.IsCollection)
        {
            nav.CurrentValue = null;
        }
    }

    entry.State = EntityState.Added;
    await write.SaveChangesAsync();

    await using var verify = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
    var ok = await verify.Set<T>().IgnoreQueryFilters().AsNoTracking().CountAsync(e => e.Id == id.Value);
    Console.WriteLine($"  {label} upserté — COUNT={ok}");
    if (ok != 1)
    {
        throw new InvalidOperationException($"Confirmation {label} échouée COUNT={ok}");
    }
}

static async Task UpsertGuardianAsync(
    SchoolDbContext local,
    DbContextOptions<SchoolDbContext> cloudOpts,
    Guardian guardian)
{
    await using var write = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
    var exists = await write.Set<Guardian>().IgnoreQueryFilters().AsNoTracking()
        .AnyAsync(g => g.Id == guardian.Id);
    // Clone pour pouvoir nuller AddressId si nécessaire
    var copy = new Guardian();
    var entry = write.Entry(copy);
    entry.CurrentValues.SetValues(guardian);
    foreach (var nav in entry.Navigations)
    {
        if (nav.Metadata.IsCollection)
        {
            nav.CurrentValue = null;
        }
    }

    if (copy.AddressId is Guid aid)
    {
        var addrOk = await write.Set<PostalAddress>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(a => a.Id == aid);
        if (!addrOk)
        {
            Console.WriteLine($"  Guardian: AddressId {aid} absent cloud → null");
            copy.AddressId = null;
        }
    }

    entry.State = exists ? EntityState.Modified : EntityState.Added;
    if (exists)
    {
        entry.Property(e => e.Id).IsModified = false;
    }

    await write.SaveChangesAsync();
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
