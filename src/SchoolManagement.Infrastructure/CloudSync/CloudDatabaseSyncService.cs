using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.Configuration;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.CloudSync;

/// <summary>
/// Synchronisation unidirectionnelle : base locale (LAN) → base cloud.
/// Upsert par Id ; ne remplace pas le cloud par des données plus anciennes.
/// </summary>
public sealed class CloudDatabaseSyncService : ICloudDatabaseSyncService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CloudDatabaseConfigurationManager _cloudConfigManager;
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly ILogger<CloudDatabaseSyncService> _logger;
    private readonly string _stateFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CloudDatabaseSyncService(
        IServiceScopeFactory scopeFactory,
        CloudDatabaseConfigurationManager cloudConfigManager,
        DatabaseConnectionFactory connectionFactory,
        ILogger<CloudDatabaseSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _cloudConfigManager = cloudConfigManager;
        _connectionFactory = connectionFactory;
        _logger = logger;
        _stateFilePath = Path.Combine(
            Path.GetDirectoryName(_cloudConfigManager.ConfigurationFilePath) ?? AppContext.BaseDirectory,
            "CloudSyncState.txt");
    }

    public async Task<CloudSyncResult> TrySyncAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return CloudSyncResult.Skip("Une synchronisation est déjà en cours.");
        }

        try
        {
            if (!_cloudConfigManager.FileExists)
            {
                return CloudSyncResult.Skip("ServeurDonneesCloud.txt absent — sync cloud désactivée.");
            }

            CloudDatabaseConfiguration cloudConfig;
            try
            {
                cloudConfig = _cloudConfigManager.LoadConfiguration();
            }
            catch (Exception ex)
            {
                return CloudSyncResult.Fail($"Impossible de lire ServeurDonneesCloud.txt : {ex.Message}");
            }

            if (!cloudConfig.Actif)
            {
                return CloudSyncResult.Skip("Sync cloud désactivée (ACTIF=0).");
            }

            var validation = _cloudConfigManager.Validate(cloudConfig, cloudConfig.MotDePasse);
            if (!validation.IsValid)
            {
                return CloudSyncResult.Fail(string.Join(" ", validation.FieldErrors.Values));
            }

            if (!await IsRemoteReachableAsync(cloudConfig, cancellationToken))
            {
                WriteState(success: false, "Distant injoignable (pas Internet ou SQL cloud down).");
                return CloudSyncResult.Skip("Serveur cloud injoignable — nouvelle tentative plus tard.");
            }

            var remoteCs = _connectionFactory.BuildConnectionString(cloudConfig.ToDatabaseConfiguration());
            var sw = Stopwatch.StartNew();
            var tables = 0;
            var rows = 0;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var local = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();

            var remoteOptions = new DbContextOptionsBuilder<SchoolDbContext>()
                .UseSqlServer(remoteCs, sql => sql.EnableRetryOnFailure(2))
                .Options;

            await using var remote = new SchoolDbContext(remoteOptions);

            try
            {
                if (!await remote.Database.CanConnectAsync(cancellationToken))
                {
                    WriteState(success: false, "Connexion SQL cloud refusée.");
                    return CloudSyncResult.Fail("Impossible de se connecter à la base cloud.");
                }
            }
            catch (Exception ex)
            {
                WriteState(success: false, ex.Message);
                return CloudSyncResult.Fail($"Connexion cloud : {ex.Message}");
            }

            foreach (var syncAction in BuildSyncPipeline())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var upserted = await syncAction(local, remote, cancellationToken);
                tables++;
                rows += upserted;
            }

            sw.Stop();
            var result = CloudSyncResult.Ok(tables, rows, sw.Elapsed);
            WriteState(success: true, result.Message);
            _logger.LogInformation("Sync cloud terminée : {Message}", result.Message);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Échec de la synchronisation cloud.");
            WriteState(success: false, ex.Message);
            return CloudSyncResult.Fail(ex.Message);
        }
        finally
        {
            _gate.Release();
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

    private delegate Task<int> SyncTableAsync(
        SchoolDbContext local,
        SchoolDbContext remote,
        CancellationToken cancellationToken);

    /// <summary>Ordre respectant les dépendances FK — aligné sur <see cref="CloudSyncCatalog.SyncOrder"/>.</summary>
    private static IEnumerable<SyncTableAsync> BuildSyncPipeline()
    {
        foreach (var (_, clrType) in CloudSyncCatalog.SyncOrder)
        {
            var entityType = clrType;
            yield return async (local, remote, cancellationToken) =>
            {
                var method = typeof(CloudDatabaseSyncService)
                    .GetMethod(nameof(UpsertAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType);
                var task = (Task<int>)method.Invoke(null, [local, remote, cancellationToken])!;
                return await task.ConfigureAwait(false);
            };
        }
    }

    private static async Task<int> UpsertAsync<TEntity>(
        SchoolDbContext local,
        SchoolDbContext remote,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var localRows = await local.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (localRows.Count == 0)
        {
            return 0;
        }

        var remoteMap = await remote.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var changed = 0;
        foreach (var localRow in localRows)
        {
            await CloudSyncNaturalKey.RemapForeignKeysAsync(local, remote, localRow, cancellationToken);

            if (remoteMap.TryGetValue(localRow.Id, out var remoteRow))
            {
                var localStamp = localRow.UpdatedAt ?? localRow.CreatedAt;
                var remoteStamp = remoteRow.UpdatedAt ?? remoteRow.CreatedAt;
                if (localStamp < remoteStamp)
                {
                    continue;
                }

                remote.Set<TEntity>().Update(localRow);
            }
            else if (await CloudSyncNaturalKey.ExistsByNaturalKeyAsync(remote, localRow, cancellationToken))
            {
                continue;
            }
            else
            {
                remote.Set<TEntity>().Add(localRow);
            }

            changed++;
        }

        if (changed > 0)
        {
            await remote.SaveChangesAsync(cancellationToken);
            // Détacher pour éviter le tracking entre tables
            foreach (var entry in remote.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        return changed;
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
}
