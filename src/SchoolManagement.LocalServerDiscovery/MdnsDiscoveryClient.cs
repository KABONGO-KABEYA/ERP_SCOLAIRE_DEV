using System.Net;
using Makaretu.Dns;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.LocalServerDiscovery;

public sealed class MdnsDiscoveryClient
{
    private readonly IHealthProbe _healthProbe;
    private readonly ILogger<MdnsDiscoveryClient> _logger;

    public MdnsDiscoveryClient(IHealthProbe healthProbe, ILogger<MdnsDiscoveryClient> logger)
    {
        _healthProbe = healthProbe;
        _logger = logger;
    }

    public async Task<DiscoveryResult?> TryDiscoverAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Discovery] Recherche mDNS…");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(DiscoveryConstants.MdnsTimeout);

        try
        {
            using var mdns = new MulticastService();
            using var sd = new ServiceDiscovery(mdns);
            var tcs = new TaskCompletionSource<DiscoveryResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnFound(object? sender, ServiceInstanceDiscoveryEventArgs e)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await ResolveInstanceAsync(e, linked.Token).ConfigureAwait(false);
                        if (result is not null)
                        {
                            tcs.TrySetResult(result);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[Discovery] Résolution mDNS échouée");
                    }
                }, CancellationToken.None);
            }

            sd.ServiceInstanceDiscovered += OnFound;
            mdns.Start();
            sd.QueryServiceInstances(DiscoveryConstants.ServiceType);

            // Essai direct hostname Bonjour.
            _ = Task.Run(async () =>
            {
                try
                {
                    var hostResult = await TryHostNameAsync(linked.Token).ConfigureAwait(false);
                    if (hostResult is not null)
                    {
                        tcs.TrySetResult(hostResult);
                    }
                }
                catch
                {
                    // ignore
                }
            }, CancellationToken.None);

            var completed = await Task.WhenAny(
                    tcs.Task,
                    Task.Delay(DiscoveryConstants.MdnsTimeout, linked.Token))
                .ConfigureAwait(false);

            sd.ServiceInstanceDiscovered -= OnFound;

            if (completed == tcs.Task && tcs.Task.IsCompletedSuccessfully)
            {
                return tcs.Task.Result;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("[Discovery] mDNS : aucun service dans le délai");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discovery] mDNS indisponible");
        }

        return null;
    }

    private async Task<DiscoveryResult?> TryHostNameAsync(CancellationToken cancellationToken)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(DiscoveryConstants.HostName, cancellationToken)
                .ConfigureAwait(false);
            foreach (var ip in addresses.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
            {
                var baseUrl = HealthProbe.ToHostPortBaseUrl(ip, DiscoveryConstants.ApiPort);
                _logger.LogInformation("[Discovery] Vérification Health {Url}", baseUrl);
                var health = await _healthProbe.ProbeAsync(baseUrl, DiscoveryConstants.LastKnownTimeout, cancellationToken)
                    .ConfigureAwait(false);
                if (health is not null)
                {
                    _logger.LogInformation("[Discovery] Service trouvé (hostname)");
                    return new DiscoveryResult(
                        DiscoveryMode.Local,
                        DiscoverySource.Mdns,
                        HealthProbe.NormalizeBaseUrl(baseUrl),
                        health,
                        $"Serveur local via {DiscoveryConstants.HostName}");
                }
            }
        }
        catch
        {
            // Hostname non résolu.
        }

        return null;
    }

    private async Task<DiscoveryResult?> ResolveInstanceAsync(
        ServiceInstanceDiscoveryEventArgs e,
        CancellationToken cancellationToken)
    {
        var name = e.ServiceInstanceName?.ToString() ?? string.Empty;
        if (!name.Contains("school", StringComparison.OrdinalIgnoreCase)
            && !name.Contains(DiscoveryConstants.ServiceInstanceName, StringComparison.OrdinalIgnoreCase))
        {
            // Accepte tout service du type _school-management._tcp
        }

        _logger.LogInformation("[Discovery] Service trouvé");

        IPAddress? ip = null;
        var port = DiscoveryConstants.ApiPort;

        foreach (var record in e.Message.Answers.Concat(e.Message.AdditionalRecords))
        {
            switch (record)
            {
                case ARecord a:
                    ip = a.Address;
                    break;
                case AAAARecord:
                    break;
                case SRVRecord srv:
                    port = srv.Port;
                    break;
            }
        }

        if (ip is null)
        {
            return null;
        }

        var baseUrl = HealthProbe.ToHostPortBaseUrl(ip, port);
        _logger.LogInformation("[Discovery] Vérification Health {Url}", baseUrl);
        var health = await _healthProbe.ProbeAsync(baseUrl, DiscoveryConstants.LastKnownTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (health is null)
        {
            return null;
        }

        return new DiscoveryResult(
            DiscoveryMode.Local,
            DiscoverySource.Mdns,
            HealthProbe.NormalizeBaseUrl(baseUrl),
            health,
            $"Serveur local découvert (mDNS) — {health.School}");
    }
}
