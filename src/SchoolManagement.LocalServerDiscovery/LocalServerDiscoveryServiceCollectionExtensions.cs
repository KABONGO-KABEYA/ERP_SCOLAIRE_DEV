using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SchoolManagement.LocalServerDiscovery;

public sealed class LocalServerDiscoveryHostedService : BackgroundService
{
    private readonly ILocalServerDiscovery _discovery;
    private readonly LocalServerDiscoveryOptions _options;
    private readonly ILogger<LocalServerDiscoveryHostedService> _logger;

    public LocalServerDiscoveryHostedService(
        ILocalServerDiscovery discovery,
        IOptions<LocalServerDiscoveryOptions> options,
        ILogger<LocalServerDiscoveryHostedService> logger)
    {
        _discovery = discovery;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _discovery.DiscoverAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[Discovery] Échec découverte initiale");
        }

        if (!_options.EnableBackgroundRecheck)
        {
            return;
        }

        using var timer = new PeriodicTimer(DiscoveryConstants.BackgroundRecheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                // Si distant (ou offline), tenter de revenir au local.
                if (_discovery.Current.Mode is DiscoveryMode.Remote or DiscoveryMode.Offline)
                {
                    _logger.LogDebug("[Discovery] Recheck arrière-plan (mode={Mode})", _discovery.Current.Mode);
                    await _discovery.RediscoverAsync(stoppingToken).ConfigureAwait(false);
                }
                else if (_discovery.Current.IsLocal && !string.IsNullOrWhiteSpace(_discovery.Current.BaseUrl))
                {
                    // Vérifie que le local répond encore ; sinon rediscovery.
                    await _discovery.DiscoverAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[Discovery] Recheck arrière-plan échoué");
            }
        }
    }
}

public static class LocalServerDiscoveryServiceCollectionExtensions
{
    public static IServiceCollection AddLocalServerDiscovery(
        this IServiceCollection services,
        Action<LocalServerDiscoveryOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<LocalServerDiscoveryOptions>();
        }

        services.AddHttpClient("LocalServerDiscovery", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddSingleton<IHealthProbe, HealthProbe>();
        services.AddSingleton<ILastKnownEndpointStore, FileLastKnownEndpointStore>();
        services.AddSingleton<MdnsDiscoveryClient>();
        services.AddSingleton<SubnetScanner>();
        services.AddSingleton<LocalServerDiscoveryService>();
        services.AddSingleton<ILocalServerDiscovery>(sp => sp.GetRequiredService<LocalServerDiscoveryService>());
        services.AddHostedService<LocalServerDiscoveryHostedService>();
        return services;
    }
}
