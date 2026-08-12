using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Premier test contrôlé du véritable moteur outbox (CloudSyncEngine.DrainAsync).
/// ACTIF=0 conservé — bypassActif via CloudSyncDrainControl uniquement.
/// Traite max N unités Pending réelles (défaut 50, plafond 500), confirmation cloud post-commit.
/// </summary>
var maxUnits = 50;
if (args.Length > 1 && int.TryParse(args[1], out var parsed))
{
    maxUnits = Math.Clamp(parsed, 1, 500);
}
else if (args.Length > 0 && int.TryParse(args[0], out var onlyArg))
{
    maxUnits = Math.Clamp(onlyArg, 1, 500);
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

Console.WriteLine($"=== ControlledOutboxDrain maxUnits={maxUnits} ===");
Console.WriteLine($"API dir: {apiDir}");
Console.WriteLine($"ACTIF={cloudCfg.Actif} (doit rester false — bypass via CloudSyncDrainControl)");

var localCfg = new DatabaseConfigurationManager(apiDir, encryption).LoadConfiguration();
var localCs = factory.BuildConnectionString(localCfg);
var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());

var services = new ServiceCollection();
services.AddLogging(b =>
{
    b.AddConsole();
    b.SetMinimumLevel(LogLevel.Information);
});
services.AddSingleton(encryption);
services.AddSingleton(factory);
services.AddSingleton(cloudMgr);
services.AddDbContext<SchoolDbContext>(opts =>
    opts.UseSqlServer(localCs, sql => sql.CommandTimeout(30)));
services.AddScoped<ICloudSyncEngine, CloudSyncEngine>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var engine = scope.ServiceProvider.GetRequiredService<ICloudSyncEngine>();

await using var local = new SchoolDbContext(
    new DbContextOptionsBuilder<SchoolDbContext>()
        .UseSqlServer(localCs, sql => sql.CommandTimeout(30))
        .Options)
{
    SuppressCloudSyncEnqueue = true,
    IgnoreSchoolScope = true
};

var pendingBeforeAll = await local.SyncOutboxUnits.CountAsync(u =>
    !u.IsDeleted && u.Status == SyncOutboxStatus.Pending);
Console.WriteLine($"Pending total avant drain: {pendingBeforeAll}");

var batchUnitIds = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.Status == SyncOutboxStatus.Pending)
    .OrderBy(u => u.Priority)
    .ThenBy(u => u.CreatedAt)
    .Take(maxUnits)
    .Select(u => u.Id)
    .ToListAsync();

Console.WriteLine($"Unités ciblées (snapshot): {batchUnitIds.Count}");
if (batchUnitIds.Count == 0)
{
    Console.WriteLine("Rien à traiter.");
    return 1;
}

var control = new CloudSyncDrainControl(
    BypassActif: true,
    PendingOnly: true,
    VerifyCloudAfterCommit: true,
    RetryPendingOnDependencyError: true);

var sw = Stopwatch.StartNew();
var result = await engine.DrainAsync(
    criticalOnly: false,
    maxUnits: maxUnits,
    control: control);
sw.Stop();

Console.WriteLine($"\n--- Résultat moteur ---");
Console.WriteLine($"Skipped={result.Skipped} Success={result.Success}");
Console.WriteLine($"Message: {result.Message}");
Console.WriteLine($"UnitsSucceeded={result.UnitsSucceeded} UnitsFailed={result.UnitsFailed}");
Console.WriteLine($"RecordsSucceeded={result.RecordsSucceeded} RecordsFailed={result.RecordsFailed}");
Console.WriteLine($"DurationMs={result.DurationMs}");

var batchUnits = await local.SyncOutboxUnits
    .Include(u => u.Items)
    .Where(u => batchUnitIds.Contains(u.Id))
    .ToListAsync();

var completed = batchUnits.Count(u => u.Status == SyncOutboxStatus.Completed);
var pendingRetry = batchUnits.Count(u =>
    u.Status == SyncOutboxStatus.Pending
    && u.AttemptCount > 0
    && u.LastAttemptAt is not null);
var stillPendingFresh = batchUnits.Count(u =>
    u.Status == SyncOutboxStatus.Pending && u.AttemptCount == 0);
var failed = batchUnits.Count(u => u.Status == SyncOutboxStatus.Failed);
var inProgress = batchUnits.Count(u => u.Status == SyncOutboxStatus.InProgress);
var deadLetter = batchUnits.Count(u => u.Status == SyncOutboxStatus.DeadLetter);

var tableHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
foreach (var unit in batchUnits)
{
    foreach (var item in unit.Items.Where(i => !i.IsDeleted))
    {
        tableHits[item.TableName] = tableHits.GetValueOrDefault(item.TableName) + 1;
    }
}

var fkOrMappingIssues = new List<string>();
var timeoutIssues = new List<string>();
var duplicateIssues = new List<string>();

foreach (var unit in batchUnits.Where(u => u.Status != SyncOutboxStatus.Completed))
{
    if (string.IsNullOrWhiteSpace(unit.LastError))
    {
        continue;
    }

    var err = unit.LastError;
    if (err.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
        || err.Contains("Table sync inconnue", StringComparison.OrdinalIgnoreCase)
        || err.Contains("REFERENCE constraint", StringComparison.OrdinalIgnoreCase))
    {
        fkOrMappingIssues.Add($"{unit.AggregateType}/{unit.AggregateId}: {Truncate(err, 300)}");
    }

    if (err.Contains("timeout", StringComparison.OrdinalIgnoreCase)
        || err.Contains("time-out", StringComparison.OrdinalIgnoreCase)
        || err.Contains("Execution Timeout", StringComparison.OrdinalIgnoreCase))
    {
        timeoutIssues.Add($"{unit.Id}: {Truncate(err, 300)}");
    }
}

await using var cloudVerify = new SchoolDbContext(
    new DbContextOptionsBuilder<SchoolDbContext>()
        .UseSqlServer(cloudCs, sql => sql.CommandTimeout(30))
        .Options)
{
    SuppressCloudSyncEnqueue = true
};

foreach (var unit in batchUnits.Where(u => u.Status == SyncOutboxStatus.Completed))
{
    foreach (var item in unit.Items.Where(i => !i.IsDeleted && i.Operation != SyncOperationType.Delete))
    {
        try
        {
            var count = await CountOnCloudAsync(cloudVerify, item.TableName, item.EntityId);
            if (count != 1)
            {
                duplicateIssues.Add($"{item.TableName}/{item.EntityId}: COUNT={count}");
            }
        }
        catch (Exception ex)
        {
            fkOrMappingIssues.Add($"Vérif cloud {item.TableName}/{item.EntityId}: {ex.Message}");
        }
    }
}

var pendingAfterAll = await local.SyncOutboxUnits.CountAsync(u =>
    !u.IsDeleted && u.Status == SyncOutboxStatus.Pending);

var minutes = Math.Max(sw.Elapsed.TotalMinutes, 0.001);
var throughput = completed / minutes;

Console.WriteLine($"\n=== BILAN CONTROLLED DRAIN ({batchUnitIds.Count} unités ciblées) ===");
Console.WriteLine($"Completed: {completed}/{batchUnitIds.Count}");
Console.WriteLine($"Pending/Retry (tentative effectuée, remis Pending): {pendingRetry}");
Console.WriteLine($"Pending sans tentative dans ce batch: {stillPendingFresh}");
Console.WriteLine($"Failed: {failed}");
Console.WriteLine($"InProgress: {inProgress}");
Console.WriteLine($"DeadLetter: {deadLetter}");
Console.WriteLine($"Durée totale: {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalSeconds:F1} s)");
Console.WriteLine($"Débit: {throughput:F2} Completed/min");
Console.WriteLine($"ACTIF final={cloudMgr.LoadConfiguration().Actif}");
Console.WriteLine($"Pending total restant: {pendingAfterAll} (delta {pendingBeforeAll - pendingAfterAll})");

Console.WriteLine("\nTypes de tables rencontrés:");
foreach (var kv in tableHits.OrderByDescending(k => k.Value).ThenBy(k => k.Key))
{
    Console.WriteLine($"  {kv.Key}: {kv.Value} item(s)");
}

Console.WriteLine("\nFK / mappings manquants ou erreurs dépendance:");
if (fkOrMappingIssues.Count == 0)
{
    Console.WriteLine("  (aucun)");
}
else
{
    foreach (var line in fkOrMappingIssues.Distinct().Take(20))
    {
        Console.WriteLine($"  {line}");
    }
}

Console.WriteLine("\nTimeouts éventuels:");
if (timeoutIssues.Count == 0)
{
    Console.WriteLine("  (aucun)");
}
else
{
    foreach (var line in timeoutIssues.Distinct().Take(20))
    {
        Console.WriteLine($"  {line}");
    }
}

Console.WriteLine("\nDoublons éventuels (COUNT != 1 sur Completed):");
if (duplicateIssues.Count == 0)
{
    Console.WriteLine("  (aucun)");
}
else
{
    foreach (var line in duplicateIssues.Distinct())
    {
        Console.WriteLine($"  {line}");
    }
}

if (failed > 0 || pendingRetry > 0)
{
    Console.WriteLine("\nDétail unités non Completed:");
    foreach (var unit in batchUnits.Where(u => u.Status != SyncOutboxStatus.Completed))
    {
        Console.WriteLine($"  {unit.Id} Status={unit.Status} Attempt={unit.AttemptCount} Type={unit.AggregateType}");
        if (!string.IsNullOrWhiteSpace(unit.LastError))
        {
            Console.WriteLine($"    Error: {Truncate(unit.LastError, 400)}");
        }
    }
}

return failed > 0 ? 2 : 0;

static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

static async Task<int> CountOnCloudAsync(SchoolDbContext remote, string tableName, Guid entityId)
{
    if (!TryResolveClrType(tableName, out var clrType))
    {
        throw new InvalidOperationException($"Table non mappée: {tableName}");
    }

    var method = typeof(CloudCountHelpers)
        .GetMethod(nameof(CloudCountHelpers.CountByIdAsync), BindingFlags.Public | BindingFlags.Static)!
        .MakeGenericMethod(clrType);
    var task = (Task<int>)method.Invoke(null, [remote, entityId])!;
    return await task;
}

static bool TryResolveClrType(string tableName, out Type clrType)
{
    var catalogType = typeof(CloudSyncEngine).Assembly
        .GetType("SchoolManagement.Infrastructure.CloudSync.CloudSyncCatalog", throwOnError: true)!;
    var syncOrderObj = catalogType
        .GetField("SyncOrder", BindingFlags.Public | BindingFlags.Static)!
        .GetValue(null);
    var syncOrder = (IReadOnlyList<(string Table, Type ClrType)>)syncOrderObj!;
    foreach (var entry in syncOrder)
    {
        if (entry.Table.Equals(tableName, StringComparison.OrdinalIgnoreCase))
        {
            clrType = entry.ClrType;
            return true;
        }
    }

    clrType = null!;
    return false;
}

static class CloudCountHelpers
{
    public static async Task<int> CountByIdAsync<T>(SchoolDbContext remote, Guid entityId)
        where T : AuditableEntity
        => await remote.Set<T>().IgnoreQueryFilters().AsNoTracking()
            .CountAsync(e => e.Id == entityId);
}
