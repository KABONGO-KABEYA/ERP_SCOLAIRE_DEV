using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.Configuration.Database;

namespace SchoolManagement.Infrastructure.CloudSync;

/// <summary>
/// Boucle d'arrière-plan : dès qu'Internet / SQL cloud est joignable, synchronise local → distant.
/// </summary>
public sealed class CloudDatabaseSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CloudDatabaseConfigurationManager _cloudConfigManager;
    private readonly ILogger<CloudDatabaseSyncHostedService> _logger;

    public CloudDatabaseSyncHostedService(
        IServiceScopeFactory scopeFactory,
        CloudDatabaseConfigurationManager cloudConfigManager,
        ILogger<CloudDatabaseSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _cloudConfigManager = cloudConfigManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Laisser l'API démarrer complètement avant la première sync.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogInformation("Service de synchronisation cloud démarré.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMinutes(5);
            try
            {
                var config = _cloudConfigManager.FileExists
                    ? _cloudConfigManager.LoadConfigurationWithoutPassword()
                    : null;

                if (config is { Actif: true })
                {
                    delay = TimeSpan.FromMinutes(Math.Clamp(config.IntervalleMinutes, 1, 1440));

                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var sync = scope.ServiceProvider.GetRequiredService<ICloudDatabaseSyncService>();
                    var result = await sync.TrySyncAsync(stoppingToken);

                    if (result.Skipped)
                    {
                        _logger.LogInformation("Sync cloud reportée : {Message}", result.Message);
                    }
                    else if (result.Success)
                    {
                        _logger.LogInformation("{Message}", result.Message);
                    }
                    else
                    {
                        _logger.LogWarning("Sync cloud échouée : {Message}", result.Message);
                    }
                }
                else
                {
                    _logger.LogInformation("Sync cloud inactive (ACTIF=0 ou fichier ServeurDonneesCloud.txt absent).");
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

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
