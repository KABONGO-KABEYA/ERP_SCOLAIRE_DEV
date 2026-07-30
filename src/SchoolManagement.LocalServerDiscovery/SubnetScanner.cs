using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.LocalServerDiscovery;

public sealed class SubnetScanner
{
    private readonly IHealthProbe _healthProbe;
    private readonly ILogger<SubnetScanner> _logger;

    public SubnetScanner(IHealthProbe healthProbe, ILogger<SubnetScanner> logger)
    {
        _healthProbe = healthProbe;
        _logger = logger;
    }

    public async Task<DiscoveryResult?> ScanAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Discovery] Scan réseau");

        var prefixes = GetLocalIpv4Prefixes();
        if (prefixes.Count == 0)
        {
            _logger.LogInformation("[Discovery] Aucun sous-réseau local IPv4 détecté");
            return null;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var found = new TaskCompletionSource<DiscoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        var candidates = prefixes
            .SelectMany(ExpandHosts)
            .Distinct()
            .ToList();

        _logger.LogInformation(
            "[Discovery] Scan de {Count} adresses sur {Prefixes}",
            candidates.Count,
            string.Join(", ", prefixes.Select(p => $"{p}/24")));

        try
        {
            await Parallel.ForEachAsync(
                candidates,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = DiscoveryConstants.ScanMaxParallelism,
                    CancellationToken = linked.Token
                },
                async (ip, ct) =>
                {
                    if (found.Task.IsCompleted)
                    {
                        return;
                    }

                    var baseUrl = HealthProbe.ToHostPortBaseUrl(ip, DiscoveryConstants.ApiPort);
                    var health = await _healthProbe.ProbeAsync(baseUrl, DiscoveryConstants.ScanProbeTimeout, ct)
                        .ConfigureAwait(false);
                    if (health is null)
                    {
                        return;
                    }

                    var result = new DiscoveryResult(
                        DiscoveryMode.Local,
                        DiscoverySource.SubnetScan,
                        HealthProbe.NormalizeBaseUrl(baseUrl),
                        health,
                        $"Serveur local trouvé par scan — {ip}");

                    if (found.TrySetResult(result))
                    {
                        _logger.LogInformation("[Discovery] Serveur trouvé {Url}", baseUrl);
                        try { linked.Cancel(); } catch { /* ignore */ }
                    }
                }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Arrêt anticipé après découverte.
        }

        return found.Task.IsCompletedSuccessfully ? found.Task.Result : null;
    }

    private static List<string> GetLocalIpv4Prefixes()
    {
        var list = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            foreach (var uni in nic.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(uni.Address))
                {
                    continue;
                }

                var bytes = uni.Address.GetAddressBytes();
                if (!IsPrivate(bytes))
                {
                    continue;
                }

                var prefix = $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
                if (!list.Contains(prefix, StringComparer.Ordinal))
                {
                    list.Add(prefix);
                }
            }
        }

        return list;
    }

    private static IEnumerable<IPAddress> ExpandHosts(string prefix)
    {
        for (var i = 1; i <= 254; i++)
        {
            if (IPAddress.TryParse($"{prefix}.{i}", out var ip))
            {
                yield return ip;
            }
        }
    }

    private static bool IsPrivate(byte[] b) =>
        b[0] == 10
        || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
        || (b[0] == 192 && b[1] == 168)
        || (b[0] == 169 && b[1] == 254);
}
