using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Diagnostic isolé timeout Payments : bypass ACTIF=0, pas de HostedService, pas de drain ~1490.
/// Tests contrôlés 1 / 5 / 10 (répétitions sur le Payment local unique + EXISTS cloud).
/// </summary>
var apiDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "src", "SchoolManagement.API", "bin", "Debug", "net8.0"));
apiDir = Path.GetFullPath(apiDir);
Console.WriteLine($"=== DiagnosePaymentSyncTimeout ===");
Console.WriteLine($"API dir: {apiDir}");

var encryption = new EncryptionService();
var factory = new DatabaseConnectionFactory();

var localMgr = new DatabaseConfigurationManager(apiDir, encryption);
var localCfg = localMgr.LoadConfiguration();
var localCs = factory.BuildConnectionString(localCfg);

var cloudMgr = new CloudDatabaseConfigurationManager(apiDir, encryption);
var cloudCfg = cloudMgr.LoadConfiguration(); // ignore ACTIF for this tool
Console.WriteLine($"ACTIF (fichier)={cloudCfg.Actif} — outil force la connexion quand même.");
Console.WriteLine($"Cloud: {cloudCfg.Serveur}:{cloudCfg.Port}/{cloudCfg.Base}");
var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());

var localOpts = new DbContextOptionsBuilder<SchoolDbContext>()
    .UseSqlServer(localCs, sql => sql.CommandTimeout(30))
    .Options;
var cloudOpts120 = new DbContextOptionsBuilder<SchoolDbContext>()
    .UseSqlServer(cloudCs, sql => sql.CommandTimeout(120))
    .Options;
var cloudOpts30 = new DbContextOptionsBuilder<SchoolDbContext>()
    .UseSqlServer(cloudCs, sql => sql.CommandTimeout(30))
    .Options;

await using var local = new SchoolDbContext(localOpts) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };

var paymentId = Guid.Parse("4EFE0AEE-EFEF-49A5-A07B-E7E81AC62371");
var paymentsLocal = await local.Set<Payment>().IgnoreQueryFilters().AsNoTracking()
    .Where(p => !p.IsDeleted).Select(p => p.Id).ToListAsync();
Console.WriteLine($"Payments locaux non supprimés: {paymentsLocal.Count}");
if (paymentsLocal.Count == 0)
{
    Console.Error.WriteLine("Aucun Payment local — abandon.");
    return 2;
}

var payment = await local.Set<Payment>().IgnoreQueryFilters().AsNoTracking()
    .FirstAsync(p => p.Id == paymentId);
Console.WriteLine($"Payment cible: {payment.Id} Student={payment.StudentId} Year={payment.AcademicYearId} Bank={payment.BankId}");

Console.WriteLine("\n--- A. Latence & verrous cloud ---");
await TimeSqlAsync(cloudCs, "SELECT 1", 30);
await TimeSqlAsync(cloudCs, "SELECT COUNT_BIG(1) FROM Payments WITH (NOLOCK)", 30);
await TimeSqlAsync(cloudCs, $"SELECT CASE WHEN EXISTS(SELECT 1 FROM Payments WITH (NOLOCK) WHERE Id='{paymentId}') THEN 1 ELSE 0 END", 30);
await TimeSqlAsync(cloudCs, $"SELECT CASE WHEN EXISTS(SELECT 1 FROM Payments WHERE Id='{paymentId}') THEN 1 ELSE 0 END", 45);
await DumpLocksAsync(cloudCs);
await DumpTriggersIndexesAsync(cloudCs);

Console.WriteLine("\n--- B. AnyAsync EnsureParent-style (CommandTimeout 30 puis 120) ---");
await TimeAnyAsync<Payment>(cloudOpts30, paymentId, "Payment EXISTS TO30");
await TimeAnyAsync<Student>(cloudOpts30, payment.StudentId, "Student EXISTS TO30");
await TimeAnyAsync<AcademicYear>(cloudOpts30, payment.AcademicYearId, "AcademicYear EXISTS TO30");
await TimeAnyAsync<Payment>(cloudOpts120, paymentId, "Payment EXISTS TO120");
await TimeAnyAsync<Student>(cloudOpts120, payment.StudentId, "Student EXISTS TO120");

Console.WriteLine("\n--- C. Volume UpsertAll finance (batchSize=1 → N SaveChanges/entité) ---");
await CountLocalAsync<School>(local, "School");
await CountLocalAsync<AcademicYear>(local, "AcademicYear");
await CountLocalAsync<ClassFeeAmount>(local, "ClassFeeAmount");
await CountLocalAsync<RevenueAllocationDestination>(local, "RevenueAllocationDestination");
await CountLocalAsync<RevenueAllocationKey>(local, "RevenueAllocationKey");
await CountLocalAsync<RevenueAllocationKeyDetail>(local, "RevenueAllocationKeyDetail");
await CountLocalAsync<WithholdingConfiguration>(local, "WithholdingConfiguration");
await CountLocalAsync<Student>(local, "Student");
await CountLocalAsync<Payment>(local, "Payment");

Console.WriteLine("\n--- D. Tests contrôlés (upsert Payment idemp. 1 / 5 / 10) ---");
Console.WriteLine("Note: 1 seul Payment local existe → répétitions idempotentes sur le même Id (simule pression batch).");

foreach (var n in new[] { 1, 5, 10 })
{
    Console.WriteLine($"\n### Batch simulé N={n}");
    var swBatch = Stopwatch.StartNew();
    var ok = 0;
    var fail = 0;
    for (var i = 1; i <= n; i++)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await UpsertPaymentOnceAsync(local, cloudOpts120, payment);
            sw.Stop();
            ok++;
            Console.WriteLine($"  [{i}/{n}] OK en {sw.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            fail++;
            Console.WriteLine($"  [{i}/{n}] FAIL en {sw.ElapsedMilliseconds} ms : {Flatten(ex)}");
            if (sw.ElapsedMilliseconds >= 100_000)
            {
                Console.WriteLine("  → arrêt anticipé (timeout long détecté).");
                break;
            }
        }
    }

    swBatch.Stop();
    Console.WriteLine($"### N={n} terminé: ok={ok} fail={fail} total={swBatch.ElapsedMilliseconds} ms");
}

Console.WriteLine("\n--- E. État Payment cloud après tests ---");
await using (var cloud = new SchoolDbContext(cloudOpts30) { SuppressCloudSyncEnqueue = true })
{
    var exists = await cloud.Set<Payment>().IgnoreQueryFilters().AsNoTracking()
        .AnyAsync(p => p.Id == paymentId);
    Console.WriteLine($"Payment cloud EXISTS={exists}");
}

Console.WriteLine("\n=== FIN diagnostic (ACTIF fichier inchangé, outbox non drainée) ===");
return 0;

static async Task TimeSqlAsync(string cs, string sql, int timeoutSec)
{
    var sw = Stopwatch.StartNew();
    try
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSec };
        var result = await cmd.ExecuteScalarAsync();
        sw.Stop();
        Console.WriteLine($"SQL OK {sw.ElapsedMilliseconds} ms (TO={timeoutSec}s): {Truncate(sql, 90)} → {result}");
    }
    catch (Exception ex)
    {
        sw.Stop();
        Console.WriteLine($"SQL FAIL {sw.ElapsedMilliseconds} ms (TO={timeoutSec}s): {Truncate(sql, 90)} → {Flatten(ex)}");
    }
}

static async Task DumpLocksAsync(string cs)
{
    const string sql = """
        SELECT TOP 20
          r.session_id, r.status, r.wait_type, r.wait_time, r.blocking_session_id,
          r.command, DB_NAME(r.database_id) db,
          LEFT(REPLACE(REPLACE(t.text, CHAR(13),' '), CHAR(10),' '), 160) text
        FROM sys.dm_exec_requests r
        OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) t
        WHERE r.session_id <> @@SPID
          AND (r.blocking_session_id <> 0 OR r.wait_type IS NOT NULL)
        ORDER BY r.wait_time DESC;
        """;
    try
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = 0;
        while (await reader.ReadAsync())
        {
            rows++;
            Console.WriteLine(
                $"LOCK/WAIT sid={reader["session_id"]} status={reader["status"]} wait={reader["wait_type"]} " +
                $"wt={reader["wait_time"]} blocking={reader["blocking_session_id"]} cmd={reader["command"]} text={reader["text"]}");
        }

        if (rows == 0)
        {
            Console.WriteLine("Aucun wait/blocking notable dans dm_exec_requests.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Dump locks impossible: {Flatten(ex)}");
    }
}

static async Task DumpTriggersIndexesAsync(string cs)
{
    const string sql = """
        SELECT 'TRIG' kind, t.name obj
        FROM sys.triggers tr
        INNER JOIN sys.tables t ON tr.parent_id = t.object_id
        WHERE t.name IN ('Payments','PaymentLines','FinRepartitionRecette','FinRetenueApplication')
        UNION ALL
        SELECT 'IDX', i.name
        FROM sys.indexes i
        INNER JOIN sys.tables t ON i.object_id = t.object_id
        WHERE t.name = 'Payments' AND i.name IS NOT NULL
        ORDER BY 1, 2;
        """;
    try
    {
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        await using var reader = await cmd.ExecuteReaderAsync();
        var n = 0;
        while (await reader.ReadAsync())
        {
            n++;
            Console.WriteLine($"{reader["kind"]}: {reader["obj"]}");
        }

        Console.WriteLine(n == 0
            ? "Aucun trigger/index listé (tables absentes?)."
            : $"Triggers/indexes Payments listés: {n}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Dump triggers/indexes: {Flatten(ex)}");
    }
}

static async Task TimeAnyAsync<T>(DbContextOptions<SchoolDbContext> opts, Guid id, string label)
    where T : class
{
    var sw = Stopwatch.StartNew();
    try
    {
        await using var ctx = new SchoolDbContext(opts) { SuppressCloudSyncEnqueue = true };
        var set = ctx.Set<T>();
        var exists = await set.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(e => EF.Property<Guid>(e, "Id") == id);
        sw.Stop();
        Console.WriteLine($"{label}: {(exists ? "EXISTS" : "MISSING")} en {sw.ElapsedMilliseconds} ms");
    }
    catch (Exception ex)
    {
        sw.Stop();
        Console.WriteLine($"{label}: FAIL en {sw.ElapsedMilliseconds} ms → {Flatten(ex)}");
    }
}

static async Task CountLocalAsync<T>(SchoolDbContext local, string name) where T : class
{
    var n = await local.Set<T>().IgnoreQueryFilters().AsNoTracking().CountAsync();
    Console.WriteLine($"  local {name}: {n} lignes → jusqu'à {n} SaveChanges (batchSize=1) par drain");
}

static async Task UpsertPaymentOnceAsync(
    SchoolDbContext local,
    DbContextOptions<SchoolDbContext> cloudOpts,
    Payment payment)
{
    await using var parentCtx = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };

    async Task EnsureScalarParentAsync<TParent>(Guid? id) where TParent : class, new()
    {
        if (id is null || id == Guid.Empty)
        {
            return;
        }

        await using var ctx = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
        var exists = await ctx.Set<TParent>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(e => EF.Property<Guid>(e, "Id") == id.Value);
        if (exists)
        {
            return;
        }

        var localParent = await local.Set<TParent>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id.Value);
        if (localParent is null)
        {
            return;
        }

        await using var write = new SchoolDbContext(cloudOpts) { SuppressCloudSyncEnqueue = true };
        var stub = new TParent();
        var entry = write.Entry(stub);
        entry.CurrentValues.SetValues(localParent);
        foreach (var nav in entry.Navigations)
        {
            if (nav.Metadata.IsCollection)
            {
                nav.CurrentValue = null;
            }
        }

        entry.State = EntityState.Added;
        await write.SaveChangesAsync();
    }

    await EnsureScalarParentAsync<Student>(payment.StudentId);
    await EnsureScalarParentAsync<AcademicYear>(payment.AcademicYearId);
    await EnsureScalarParentAsync<Bank>(payment.BankId);

    var remoteExists = await parentCtx.Set<Payment>().IgnoreQueryFilters().AsNoTracking()
        .AnyAsync(p => p.Id == payment.Id);

    var stub = new Payment();
    var entry = parentCtx.Entry(stub);
    entry.CurrentValues.SetValues(payment);
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
        entry.Property(e => e.Id).IsModified = false;
    }
    else
    {
        entry.State = EntityState.Added;
    }

    await parentCtx.SaveChangesAsync();
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

    return string.Join(" -> ", parts.Distinct()).Truncate(280);
}

static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..max] + "…";

file static class StrExt
{
    public static string Truncate(this string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
