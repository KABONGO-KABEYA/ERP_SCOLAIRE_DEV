using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SchoolManagement.LocalServerDiscovery;

public sealed class LocalServerDiscoveryOptions
{
    public const string SectionName = "LocalServerDiscovery";

    /// <summary>URL distante de secours (Cloud).</summary>
    public string RemoteBaseUrl { get; set; } = DiscoveryConstants.DefaultRemoteBaseUrl;

    /// <summary>Active le scan de sous-réseau (peut être désactivé sur réseaux très larges).</summary>
    public bool EnableSubnetScan { get; set; } = true;

    /// <summary>Vérification périodique pour revenir au local.</summary>
    public bool EnableBackgroundRecheck { get; set; } = true;
}

/// <summary>
/// Singleton orchestrant mDNS → dernière IP → scan → distant.
/// </summary>
public sealed class LocalServerDiscoveryService : ILocalServerDiscovery, IDisposable
{
    private readonly MdnsDiscoveryClient _mdns;
    private readonly SubnetScanner _subnetScanner;
    private readonly IHealthProbe _healthProbe;
    private readonly ILastKnownEndpointStore _store;
    private readonly LocalServerDiscoveryOptions _options;
    private readonly ILogger<LocalServerDiscoveryService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _runningCts;
    private DiscoveryResult _current = DiscoveryResult.Detecting();
    private bool _networkHooked;

    public LocalServerDiscoveryService(
        MdnsDiscoveryClient mdns,
        SubnetScanner subnetScanner,
        IHealthProbe healthProbe,
        ILastKnownEndpointStore store,
        IOptions<LocalServerDiscoveryOptions> options,
        ILogger<LocalServerDiscoveryService> logger)
    {
        _mdns = mdns;
        _subnetScanner = subnetScanner;
        _healthProbe = healthProbe;
        _store = store;
        _options = options.Value;
        _logger = logger;
        EnsureNetworkHook();
    }

    public DiscoveryResult Current => _current;

    public event EventHandler<DiscoveryResult>? Changed;

    public Task<DiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default) =>
        RunDiscoveryAsync(force: false, cancellationToken);

    public Task<DiscoveryResult> RediscoverAsync(CancellationToken cancellationToken = default) =>
        RunDiscoveryAsync(force: true, cancellationToken);

    private async Task<DiscoveryResult> RunDiscoveryAsync(bool force, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _runningCts?.Cancel();
            _runningCts?.Dispose();
            _runningCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ct = _runningCts.Token;

            if (!force && _current.IsLocal)
            {
                var stillAlive = await _healthProbe.ProbeAsync(
                        _current.BaseUrl!,
                        DiscoveryConstants.LastKnownTimeout,
                        ct)
                    .ConfigureAwait(false);
                if (stillAlive is not null)
                {
                    return _current;
                }

                _logger.LogInformation("[Discovery] Ancienne IP ne répond plus");
                await _store.ClearAsync(ct).ConfigureAwait(false);
            }

            Publish(DiscoveryResult.Detecting());

            // 1) mDNS
            var mdns = await _mdns.TryDiscoverAsync(ct).ConfigureAwait(false);
            if (mdns is not null)
            {
                await PersistLocalAsync(mdns, ct).ConfigureAwait(false);
                return Publish(mdns);
            }

            // 2) Dernière IP connue
            _logger.LogInformation("[Discovery] Dernière IP connue");
            var last = await _store.GetAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(last))
            {
                _logger.LogInformation("[Discovery] Vérification Health {Url}", last);
                var health = await _healthProbe.ProbeAsync(last, DiscoveryConstants.LastKnownTimeout, ct)
                    .ConfigureAwait(false);
                if (health is not null)
                {
                    var result = new DiscoveryResult(
                        DiscoveryMode.Local,
                        DiscoverySource.LastKnown,
                        HealthProbe.NormalizeBaseUrl(last),
                        health,
                        $"Serveur local (dernière IP) — {health.School}");
                    return Publish(result);
                }

                await _store.ClearAsync(ct).ConfigureAwait(false);
            }

            // 3) Scan sous-réseau
            if (_options.EnableSubnetScan)
            {
                var scanned = await _subnetScanner.ScanAsync(ct).ConfigureAwait(false);
                if (scanned is not null)
                {
                    await PersistLocalAsync(scanned, ct).ConfigureAwait(false);
                    return Publish(scanned);
                }
            }

            // 4) Distant
            var remote = string.IsNullOrWhiteSpace(_options.RemoteBaseUrl)
                ? DiscoveryConstants.DefaultRemoteBaseUrl
                : _options.RemoteBaseUrl;
            remote = HealthProbe.NormalizeBaseUrl(remote);
            _logger.LogInformation("[Discovery] Vérification Health distant {Url}", remote);
            var remoteHealth = await _healthProbe.ProbeAsync(remote, DiscoveryConstants.LastKnownTimeout, ct)
                .ConfigureAwait(false);
            if (remoteHealth is not null)
            {
                _logger.LogInformation("[Discovery] Passage en serveur distant");
                return Publish(new DiscoveryResult(
                    DiscoveryMode.Remote,
                    DiscoverySource.Remote,
                    remote,
                    remoteHealth with { Server = "cloud" },
                    $"Serveur distant — {remoteHealth.School}"));
            }

            return Publish(DiscoveryResult.Offline(
                "Aucun serveur local ni distant accessible."));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Publish(DiscoveryResult.Offline("Recherche annulée."));
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PersistLocalAsync(DiscoveryResult result, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(result.BaseUrl))
        {
            return;
        }

        _logger.LogInformation("[Discovery] Passage en serveur local");
        await _store.SaveAsync(result.BaseUrl, cancellationToken).ConfigureAwait(false);
    }

    private DiscoveryResult Publish(DiscoveryResult result)
    {
        var previous = _current;
        _current = result;
        if (!ReferenceEquals(previous, result)
            && (previous.Mode != result.Mode
                || !string.Equals(previous.BaseUrl, result.BaseUrl, StringComparison.OrdinalIgnoreCase)))
        {
            Changed?.Invoke(this, result);
        }

        return result;
    }

    private void EnsureNetworkHook()
    {
        if (_networkHooked)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _networkHooked = true;
    }

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        _logger.LogInformation("[Discovery] Changement de réseau détecté");
        _logger.LogInformation("[Discovery] Nouvelle IP détectée");
        _ = RediscoverAsync();
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _logger.LogInformation("[Discovery] Changement de réseau détecté (disponibilité={Available})", e.IsAvailable);
        _ = RediscoverAsync();
    }

    public void Dispose()
    {
        if (_networkHooked)
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            _networkHooked = false;
        }

        _runningCts?.Cancel();
        _runningCts?.Dispose();
        _gate.Dispose();
    }
}
