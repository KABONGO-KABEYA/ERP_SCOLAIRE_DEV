using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.CloudSync.DTOs;
using SchoolManagement.Application.Configuration;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.CloudSync;

/// <summary>
/// Moteur production : outbox persistante, sync transactionnelle par unité, journal, reprise.
/// </summary>
public sealed class CloudSyncEngine : ICloudSyncEngine
{
    private const int MaxAttemptsBeforeDeadLetter = 8;
    private static readonly TimeSpan StaleInProgressTimeout = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CloudDatabaseConfigurationManager _cloudConfigManager;
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly ILogger<CloudSyncEngine> _logger;
    private readonly string _stateFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CloudSyncEngine(
        IServiceScopeFactory scopeFactory,
        CloudDatabaseConfigurationManager cloudConfigManager,
        DatabaseConnectionFactory connectionFactory,
        ILogger<CloudSyncEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _cloudConfigManager = cloudConfigManager;
        _connectionFactory = connectionFactory;
        _logger = logger;
        _stateFilePath = Path.Combine(
            Path.GetDirectoryName(_cloudConfigManager.ConfigurationFilePath) ?? AppContext.BaseDirectory,
            "CloudSyncState.txt");
    }

    public async Task<CloudSyncRunResultDto> DrainAsync(
        bool criticalOnly = false,
        int maxUnits = 50,
        CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return new CloudSyncRunResultDto(true, false, "Une synchronisation est déjà en cours.", 0, 0, 0, 0, 0);
        }

        var sw = Stopwatch.StartNew();
        var startedAt = DateTime.UtcNow;
        var unitsSucceeded = 0;
        var unitsFailed = 0;
        var recordsSucceeded = 0;
        var recordsFailed = 0;
        var tablesTouched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        try
        {
            var open = await TryOpenRemoteAsync(cancellationToken);
            if (open.Skipped)
            {
                await WriteJournalAsync(
                    startedAt, sw, skipped: true, success: false,
                    unitsAttempted: 0, unitsSucceeded: 0, unitsFailed: 0,
                    recordsSent: 0, recordsSucceeded: 0, recordsFailed: 0,
                    tablesTouched: null, errorSummary: open.Message, detailJson: null,
                    cancellationToken);
                WriteState(success: false, open.Message);
                return new CloudSyncRunResultDto(true, false, open.Message, 0, 0, 0, 0, (int)sw.ElapsedMilliseconds);
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var local = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
            local.SuppressCloudSyncEnqueue = true;

            await RecoverStaleInProgressAsync(local, cancellationToken);

            var query = local.SyncOutboxUnits
                .Include(u => u.Items)
                .Where(u => !u.IsDeleted
                            && (u.Status == SyncOutboxStatus.Pending || u.Status == SyncOutboxStatus.Failed));

            if (criticalOnly)
            {
                query = query.Where(u => u.Priority == SyncPriority.Critical);
            }

            var units = await query
                .OrderBy(u => u.Priority)
                .ThenBy(u => u.CreatedAt)
                .Take(Math.Clamp(maxUnits, 1, 500))
                .ToListAsync(cancellationToken);

            if (units.Count == 0)
            {
                var msg = criticalOnly
                    ? "Aucune unité critique en attente."
                    : "File outbox vide.";
                await WriteJournalAsync(
                    startedAt, sw, skipped: true, success: true,
                    unitsAttempted: 0, unitsSucceeded: 0, unitsFailed: 0,
                    recordsSent: 0, recordsSucceeded: 0, recordsFailed: 0,
                    tablesTouched: null, errorSummary: msg, detailJson: null,
                    cancellationToken);
                WriteState(success: true, msg);
                return new CloudSyncRunResultDto(true, true, msg, 0, 0, 0, 0, (int)sw.ElapsedMilliseconds);
            }

            await using var remote = open.Remote!;
            remote.SuppressCloudSyncEnqueue = true;

            foreach (var unit in units)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outcome = await ProcessUnitAsync(local, remote, unit, cancellationToken);
                foreach (var t in outcome.Tables)
                {
                    tablesTouched.Add(t);
                }

                if (outcome.Success)
                {
                    unitsSucceeded++;
                    recordsSucceeded += outcome.RecordsOk;
                }
                else
                {
                    unitsFailed++;
                    recordsFailed += outcome.RecordsFail;
                    if (!string.IsNullOrWhiteSpace(outcome.Error))
                    {
                        errors.Add($"{unit.AggregateType}/{unit.AggregateId}: {outcome.Error}");
                    }
                }
            }

            sw.Stop();
            var success = unitsFailed == 0;
            var summary = success
                ? $"Sync OK : {unitsSucceeded} unité(s), {recordsSucceeded} enregistrement(s) en {sw.ElapsedMilliseconds} ms."
                : $"Sync partielle : {unitsSucceeded} OK, {unitsFailed} échec(s).";
            var errorSummary = errors.Count == 0 ? null : string.Join(" | ", errors.Take(8));

            await WriteJournalAsync(
                startedAt, sw, skipped: false, success,
                units.Count, unitsSucceeded, unitsFailed,
                recordsSucceeded + recordsFailed, recordsSucceeded, recordsFailed,
                string.Join(',', tablesTouched.OrderBy(t => t).Take(40)),
                errorSummary,
                JsonSerializer.Serialize(new { errors }),
                cancellationToken);

            WriteState(success, summary);
            _logger.LogInformation("Drain sync cloud : {Message}", summary);
            return new CloudSyncRunResultDto(
                false, success, summary, unitsSucceeded, unitsFailed,
                recordsSucceeded, recordsFailed, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec drain sync cloud.");
            await WriteJournalAsync(
                startedAt, sw, skipped: false, success: false,
                unitsSucceeded + unitsFailed, unitsSucceeded, unitsFailed,
                recordsSucceeded + recordsFailed, recordsSucceeded, recordsFailed,
                null, ex.Message, null, cancellationToken);
            WriteState(success: false, ex.Message);
            return new CloudSyncRunResultDto(
                false, false, ex.Message, unitsSucceeded, unitsFailed,
                recordsSucceeded, recordsFailed, (int)sw.ElapsedMilliseconds);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EnqueueCatchUpAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var local = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
        local.SuppressCloudSyncEnqueue = true;

        var watermarks = await local.SyncWatermarks
            .Where(w => !w.IsDeleted)
            .ToDictionaryAsync(w => w.TableName, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var writer = new CloudSyncOutboxWriter();
        var changes = new List<CloudSyncChange>();
        var now = DateTime.UtcNow;

        foreach (var (tableName, clrType) in CloudSyncCatalog.SyncOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            watermarks.TryGetValue(tableName, out var watermark);
            var since = watermark?.LastSyncedAt ?? DateTime.UtcNow.AddDays(-1);

            try
            {
                var method = typeof(CloudSyncEngine)
                    .GetMethod(nameof(CollectCatchUpChangesAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(clrType);

                var task = (Task<List<CloudSyncChange>>)method.Invoke(null, [local, tableName, since, cancellationToken])!;
                var batch = await task;
                changes.AddRange(batch);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Catch-up ignoré pour la table {Table} (schéma local incompatible ou lecture impossible).",
                    tableName);
                continue;
            }

            if (watermark is null)
            {
                local.SyncWatermarks.Add(new SyncWatermark
                {
                    TableName = tableName,
                    LastSyncedAt = since,
                    CreatedAt = now
                });
            }
        }

        if (changes.Count > 0)
        {
            await writer.EnqueueAsync(local, changes, cancellationToken);
            _logger.LogInformation("Catch-up outbox : {Count} changement(s) enfilé(s).", changes.Count);
        }

        await local.SaveChangesAsync(cancellationToken);
    }

    public async Task<CloudSyncStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var configured = _cloudConfigManager.FileExists;
        CloudDatabaseConfiguration? preview = null;
        var enabled = false;
        string? server = null;

        if (configured)
        {
            try
            {
                preview = _cloudConfigManager.LoadConfigurationWithoutPassword();
                enabled = preview.Actif;
                server = preview.Serveur;
            }
            catch
            {
                configured = true;
            }
        }

        var reachable = false;
        if (enabled && preview is not null)
        {
            try
            {
                var full = _cloudConfigManager.LoadConfiguration();
                reachable = await IsRemoteReachableAsync(full, cancellationToken);
            }
            catch
            {
                reachable = false;
            }
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var local = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();

        var pendingUnits = await local.SyncOutboxUnits.CountAsync(
            u => !u.IsDeleted && (u.Status == SyncOutboxStatus.Pending || u.Status == SyncOutboxStatus.InProgress),
            cancellationToken);
        var pendingCritical = await local.SyncOutboxUnits.CountAsync(
            u => !u.IsDeleted
                 && u.Priority == SyncPriority.Critical
                 && (u.Status == SyncOutboxStatus.Pending || u.Status == SyncOutboxStatus.InProgress || u.Status == SyncOutboxStatus.Failed),
            cancellationToken);
        var failedUnits = await local.SyncOutboxUnits.CountAsync(
            u => !u.IsDeleted && u.Status == SyncOutboxStatus.Failed, cancellationToken);
        var deadLetter = await local.SyncOutboxUnits.CountAsync(
            u => !u.IsDeleted && u.Status == SyncOutboxStatus.DeadLetter, cancellationToken);

        var recent = await local.SyncJournalEntries
            .Where(j => !j.IsDeleted)
            .OrderByDescending(j => j.StartedAt)
            .Take(15)
            .Select(j => new CloudSyncJournalLineDto(
                j.Id,
                j.StartedAt,
                j.DurationMs,
                j.Success,
                j.Skipped,
                j.UnitsSucceeded,
                j.UnitsFailed,
                j.RecordsSucceeded,
                j.RecordsFailed,
                j.TablesTouched,
                j.ErrorSummary))
            .ToListAsync(cancellationToken);

        var lastSuccess = await local.SyncJournalEntries
            .Where(j => !j.IsDeleted && j.Success && !j.Skipped)
            .OrderByDescending(j => j.StartedAt)
            .Select(j => (DateTime?)j.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastAttempt = await local.SyncJournalEntries
            .Where(j => !j.IsDeleted)
            .OrderByDescending(j => j.StartedAt)
            .Select(j => (DateTime?)j.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastMessage = await local.SyncJournalEntries
            .Where(j => !j.IsDeleted)
            .OrderByDescending(j => j.StartedAt)
            .Select(j => j.ErrorSummary ?? (j.Success ? "OK" : "Échec"))
            .FirstOrDefaultAsync(cancellationToken);

        double? avgDuration = null;
        var durations = await local.SyncJournalEntries
            .Where(j => !j.IsDeleted && !j.Skipped && j.DurationMs > 0)
            .OrderByDescending(j => j.StartedAt)
            .Take(30)
            .Select(j => j.DurationMs)
            .ToListAsync(cancellationToken);
        if (durations.Count > 0)
        {
            avgDuration = durations.Average();
        }

        // Compléter depuis fichier d'état si journal vide
        if (lastSuccess is null || lastAttempt is null)
        {
            var fileState = ReadStateFile();
            lastSuccess ??= fileState.LastSuccessUtc;
            lastAttempt ??= fileState.LastAttemptUtc;
            lastMessage ??= fileState.LastMessage;
        }

        return new CloudSyncStatusDto(
            configured,
            enabled,
            reachable,
            server,
            lastSuccess,
            lastAttempt,
            lastMessage,
            pendingUnits,
            pendingCritical,
            failedUnits,
            deadLetter,
            avgDuration,
            recent);
    }

    /// <summary>Bootstrap unique : copie complète si outbox vide et aucun watermark (migration v1 → v2).</summary>
    public async Task<bool> TryBootstrapFullSyncIfNeededAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var local = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
        var hasWatermark = await local.SyncWatermarks.AnyAsync(w => !w.IsDeleted, cancellationToken);
        var hasOutbox = await local.SyncOutboxUnits.AnyAsync(u => !u.IsDeleted, cancellationToken);
        if (hasWatermark || hasOutbox)
        {
            return false;
        }

        var legacy = scope.ServiceProvider.GetRequiredService<ICloudDatabaseSyncService>();
        var result = await legacy.TrySyncAsync(cancellationToken);
        if (!result.Success || result.Skipped)
        {
            return false;
        }

        local.SuppressCloudSyncEnqueue = true;
        var now = DateTime.UtcNow;
        foreach (var (tableName, _) in CloudSyncCatalog.SyncOrder)
        {
            local.SyncWatermarks.Add(new SyncWatermark
            {
                TableName = tableName,
                LastSyncedAt = now,
                CreatedAt = now
            });
        }

        await local.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Bootstrap sync cloud terminé — watermarks initialisés.");
        return true;
    }

    private async Task<(bool Skipped, string Message, SchoolDbContext? Remote)> TryOpenRemoteAsync(
        CancellationToken cancellationToken)
    {
        if (!_cloudConfigManager.FileExists)
        {
            return (true, "ServeurDonneesCloud.txt absent — sync cloud désactivée.", null);
        }

        CloudDatabaseConfiguration cloudConfig;
        try
        {
            cloudConfig = _cloudConfigManager.LoadConfiguration();
        }
        catch (Exception ex)
        {
            return (true, $"Impossible de lire ServeurDonneesCloud.txt : {ex.Message}", null);
        }

        if (!cloudConfig.Actif)
        {
            return (true, "Sync cloud désactivée (ACTIF=0).", null);
        }

        var validation = _cloudConfigManager.Validate(cloudConfig, cloudConfig.MotDePasse);
        if (!validation.IsValid)
        {
            return (true, string.Join(" ", validation.FieldErrors.Values), null);
        }

        if (!await IsRemoteReachableAsync(cloudConfig, cancellationToken))
        {
            return (true, "Serveur cloud injoignable — nouvelle tentative plus tard.", null);
        }

        var remoteCs = _connectionFactory.BuildConnectionString(cloudConfig.ToDatabaseConfiguration());
        var remoteOptions = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(remoteCs)
            .Options;
        var remote = new SchoolDbContext(remoteOptions) { SuppressCloudSyncEnqueue = true };

        try
        {
            if (!await remote.Database.CanConnectAsync(cancellationToken))
            {
                await remote.DisposeAsync();
                return (true, "Impossible de se connecter à la base cloud.", null);
            }
        }
        catch (Exception ex)
        {
            await remote.DisposeAsync();
            return (true, $"Connexion cloud : {ex.Message}", null);
        }

        return (false, "OK", remote);
    }

    private async Task RecoverStaleInProgressAsync(SchoolDbContext local, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - StaleInProgressTimeout;
        var stale = await local.SyncOutboxUnits
            .Include(u => u.Items)
            .Where(u => !u.IsDeleted && u.Status == SyncOutboxStatus.InProgress && u.LastAttemptAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var unit in stale)
        {
            unit.Status = SyncOutboxStatus.Pending;
            unit.LastError = "Reprise après interruption (InProgress périmé).";
            foreach (var item in unit.Items.Where(i => i.Status == SyncOutboxStatus.InProgress))
            {
                item.Status = SyncOutboxStatus.Pending;
            }
        }

        if (stale.Count > 0)
        {
            await local.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<UnitOutcome> ProcessUnitAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        SyncOutboxUnit unit,
        CancellationToken cancellationToken)
    {
        var items = unit.Items
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.Sequence)
            .ToList();

        unit.Status = SyncOutboxStatus.InProgress;
        unit.AttemptCount++;
        unit.LastAttemptAt = DateTime.UtcNow;
        unit.LastError = null;
        foreach (var item in items)
        {
            item.Status = SyncOutboxStatus.InProgress;
            item.LastError = null;
        }

        await local.SaveChangesAsync(cancellationToken);

        var tables = items.Select(i => i.TableName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var applied = 0;

        try
        {
            await using var tx = await remote.Database.BeginTransactionAsync(cancellationToken);

            foreach (var item in items)
            {
                await ApplyItemAsync(local, remote, item, cancellationToken);
                applied++;
            }

            // Intégrité : nombre d'items traités == attendu
            var expected = unit.ExpectedItemCount > 0 ? unit.ExpectedItemCount : items.Count;
            if (applied != expected || applied != items.Count)
            {
                throw new InvalidOperationException(
                    $"Intégrité sync : attendu {expected}, traité {applied} / {items.Count}.");
            }

            await remote.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            foreach (var entry in remote.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }

            unit.Status = SyncOutboxStatus.Completed;
            unit.CompletedAt = DateTime.UtcNow;
            foreach (var item in items)
            {
                item.Status = SyncOutboxStatus.Completed;
            }

            await AdvanceWatermarksAsync(local, items, cancellationToken);
            await local.SaveChangesAsync(cancellationToken);

            return new UnitOutcome(true, applied, 0, null, tables);
        }
        catch (Exception ex)
        {
            try
            {
                foreach (var entry in remote.ChangeTracker.Entries().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
            catch
            {
                // ignore
            }

            var dead = unit.AttemptCount >= MaxAttemptsBeforeDeadLetter;
            unit.Status = dead ? SyncOutboxStatus.DeadLetter : SyncOutboxStatus.Failed;
            unit.LastError = Truncate(ex.Message, 2000);
            foreach (var item in items)
            {
                item.Status = unit.Status;
                item.LastError = unit.LastError;
            }

            await local.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(ex, "Échec unité sync {UnitId} ({Aggregate}).", unit.Id, unit.AggregateType);
            return new UnitOutcome(false, 0, items.Count, ex.Message, tables);
        }
    }

    private static async Task ApplyItemAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        SyncOutboxItem item,
        CancellationToken cancellationToken)
    {
        if (!CloudSyncCatalog.TryGetClrType(item.TableName, out var clrType))
        {
            throw new InvalidOperationException($"Table sync inconnue : {item.TableName}");
        }

        var method = typeof(CloudSyncEngine)
            .GetMethod(nameof(ApplyTypedItemAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(clrType);

        var task = (Task)method.Invoke(null, [local, remote, item, cancellationToken])!;
        await task;
    }

    private static async Task ApplyTypedItemAsync<TEntity>(
        SchoolDbContext local,
        SchoolDbContext remote,
        SyncOutboxItem item,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var localEntity = await local.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == item.EntityId, cancellationToken);

        var remoteEntity = await remote.Set<TEntity>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == item.EntityId, cancellationToken);

        if (item.Operation == SyncOperationType.Delete)
        {
            if (localEntity is not null)
            {
                if (remoteEntity is null)
                {
                    remote.Set<TEntity>().Add(localEntity);
                }
                else
                {
                    remote.Entry(remoteEntity).CurrentValues.SetValues(localEntity);
                }
            }
            else if (remoteEntity is not null)
            {
                remoteEntity.IsDeleted = true;
                remoteEntity.DeletedAt ??= DateTime.UtcNow;
            }

            return;
        }

        if (localEntity is null)
        {
            // Ligne disparue localement : rien à pousser (idempotent).
            return;
        }

        if (remoteEntity is null)
        {
            remote.Set<TEntity>().Add(localEntity);
            return;
        }

        // Source de vérité = local : on écrase toujours le cloud.
        remote.Entry(remoteEntity).CurrentValues.SetValues(localEntity);
    }

    private static async Task AdvanceWatermarksAsync(
        SchoolDbContext local,
        List<SyncOutboxItem> items,
        CancellationToken cancellationToken)
    {
        var byTable = items.GroupBy(i => i.TableName, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        foreach (var group in byTable)
        {
            var watermark = await local.SyncWatermarks
                .FirstOrDefaultAsync(w => !w.IsDeleted && w.TableName == group.Key, cancellationToken);
            if (watermark is null)
            {
                local.SyncWatermarks.Add(new SyncWatermark
                {
                    TableName = group.Key,
                    LastSyncedAt = now,
                    LastSyncedEntityId = group.Last().EntityId,
                    CreatedAt = now
                });
            }
            else
            {
                watermark.LastSyncedAt = now;
                watermark.LastSyncedEntityId = group.Last().EntityId;
                watermark.UpdatedAt = now;
            }
        }
    }

    private static async Task<List<CloudSyncChange>> CollectCatchUpChangesAsync<TEntity>(
        SchoolDbContext local,
        string tableName,
        DateTime since,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var rows = await local.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e =>
                e.CreatedAt > since
                || (e.UpdatedAt != null && e.UpdatedAt > since)
                || (e.DeletedAt != null && e.DeletedAt > since))
            .OrderBy(e => e.UpdatedAt ?? e.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var result = new List<CloudSyncChange>(rows.Count);
        foreach (var row in rows)
        {
            var op = row.IsDeleted ? SyncOperationType.Delete
                : row.UpdatedAt is null || row.UpdatedAt <= row.CreatedAt
                    ? SyncOperationType.Insert
                    : SyncOperationType.Update;
            var (aggType, aggId) = CloudSyncCatalog.ResolveAggregate(tableName, row.Id, row);
            result.Add(new CloudSyncChange(
                tableName,
                row.Id,
                op,
                aggType,
                aggId,
                CloudSyncCatalog.ResolvePriority(tableName)));
        }

        return result;
    }

    private async Task WriteJournalAsync(
        DateTime startedAt,
        Stopwatch sw,
        bool skipped,
        bool success,
        int unitsAttempted,
        int unitsSucceeded,
        int unitsFailed,
        int recordsSent,
        int recordsSucceeded,
        int recordsFailed,
        string? tablesTouched,
        string? errorSummary,
        string? detailJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var local = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
            local.SuppressCloudSyncEnqueue = true;
            local.SyncJournalEntries.Add(new SyncJournalEntry
            {
                StartedAt = startedAt,
                EndedAt = DateTime.UtcNow,
                DurationMs = (int)sw.ElapsedMilliseconds,
                Success = success,
                Skipped = skipped,
                UnitsAttempted = unitsAttempted,
                UnitsSucceeded = unitsSucceeded,
                UnitsFailed = unitsFailed,
                RecordsSent = recordsSent,
                RecordsSucceeded = recordsSucceeded,
                RecordsFailed = recordsFailed,
                TablesTouched = Truncate(tablesTouched, 2000),
                ErrorSummary = Truncate(errorSummary, 4000),
                DetailJson = detailJson,
                CreatedAt = DateTime.UtcNow
            });
            await local.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'écrire SyncJournal.");
        }
    }

    private static async Task<bool> IsRemoteReachableAsync(
        CloudDatabaseConfiguration config,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(config.Serveur.Trim(), config.Port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private void WriteState(bool success, string message)
    {
        try
        {
            var lines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LAST_ATTEMPT_UTC"] = DateTime.UtcNow.ToString("O"),
                ["LAST_SUCCESS"] = success ? "1" : "0",
                ["LAST_MESSAGE"] = message.Replace('\r', ' ').Replace('\n', ' ')
            };
            if (success)
            {
                lines["LAST_SUCCESS_UTC"] = DateTime.UtcNow.ToString("O");
            }

            File.WriteAllText(
                _stateFilePath,
                TextConfigurationFileParser.Serialize(
                    lines,
                    "# État de la dernière synchronisation cloud"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'écrire CloudSyncState.txt.");
        }
    }

    private (DateTime? LastSuccessUtc, DateTime? LastAttemptUtc, string? LastMessage) ReadStateFile()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return (null, null, null);
            }

            var map = TextConfigurationFileParser.Parse(File.ReadAllText(_stateFilePath));
            DateTime? success = null;
            DateTime? attempt = null;
            if (map.TryGetValue("LAST_SUCCESS_UTC", out var s) && DateTime.TryParse(s, out var sd))
            {
                success = DateTime.SpecifyKind(sd, DateTimeKind.Utc);
            }

            if (map.TryGetValue("LAST_ATTEMPT_UTC", out var a) && DateTime.TryParse(a, out var ad))
            {
                attempt = DateTime.SpecifyKind(ad, DateTimeKind.Utc);
            }

            map.TryGetValue("LAST_MESSAGE", out var msg);
            return (success, attempt, msg);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];

    private sealed record UnitOutcome(
        bool Success,
        int RecordsOk,
        int RecordsFail,
        string? Error,
        IReadOnlyList<string> Tables);
}
