using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.Configuration.Database;

namespace SchoolManagement.Infrastructure.CloudSync;

/// <summary>
/// Boucle d'arrière-plan :
/// - drain critique fréquent (paiements) ;
/// - catch-up + drain complet selon INTERVALLE_MINUTES.
/// </summary>
public sealed class CloudSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CloudDatabaseConfigurationManager _cloudConfigManager;
    private readonly ILogger<CloudSyncHostedService> _logger;

    public CloudSyncHostedService(
        IServiceScopeFactory scopeFactory,
        CloudDatabaseConfigurationManager cloudConfigManager,
        ILogger<CloudSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _cloudConfigManager = cloudConfigManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogInformation("Service de synchronisation cloud (outbox) démarré.");

        var lastFullDrain = DateTime.UtcNow.AddMinutes(-60);
        var tick = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var criticalDelay = TimeSpan.FromSeconds(30);
            var fullIntervalMinutes = 5;

            try
            {
                var config = _cloudConfigManager.FileExists
                    ? _cloudConfigManager.LoadConfigurationWithoutPassword()
                    : null;

                if (config is not { Actif: true })
                {
                    if (tick % 10 == 0)
                    {
                        _logger.LogInformation(
                            "Sync cloud inactive (ACTIF=0 ou fichier ServeurDonneesCloud.txt absent).");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
                    tick++;
                    continue;
                }

                fullIntervalMinutes = Math.Clamp(config.IntervalleMinutes, 1, 1440);

                await using var scope = _scopeFactory.CreateAsyncScope();
                var engine = scope.ServiceProvider.GetRequiredService<ICloudSyncEngine>();

                // Drain critique quasi immédiat
                var critical = await engine.DrainAsync(criticalOnly: true, maxUnits: 80, stoppingToken);
                if (!critical.Skipped && (critical.UnitsSucceeded > 0 || critical.UnitsFailed > 0))
                {
                    _logger.LogInformation(
                        "Sync critique : {Message}", critical.Message);
                }

                // Catch-up + drain complet périodique
                if (DateTime.UtcNow - lastFullDrain >= TimeSpan.FromMinutes(fullIntervalMinutes))
                {
                    await engine.TryBootstrapFullSyncIfNeededAsync(stoppingToken);
                    await engine.EnqueueCatchUpAsync(stoppingToken);
                    var full = await engine.DrainAsync(criticalOnly: false, maxUnits: 150, stoppingToken);
                    lastFullDrain = DateTime.UtcNow;

                    if (full.Skipped)
                    {
                        _logger.LogInformation("Sync périodique reportée : {Message}", full.Message);
                    }
                    else if (full.Success)
                    {
                        _logger.LogInformation("{Message}", full.Message);
                    }
                    else
                    {
                        _logger.LogWarning("Sync périodique : {Message}", full.Message);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans la boucle de synchronisation cloud.");
            }

            tick++;
            try
            {
                await Task.Delay(criticalDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
