using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Makaretu.Dns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.LocalServerDiscovery;

/// <summary>
/// Publie le service mDNS <c>school-server._school-management._tcp</c>.
/// Identité stable (nom/type) ; les IPv4 LAN sont dynamiques et republicées
/// quand le réseau change (Wi‑Fi / Ethernet / DHCP).
/// </summary>
public sealed class MdnsServiceAdvertiser : IHostedService, IDisposable
{
    private static readonly TimeSpan RepublishDebounce = TimeSpan.FromSeconds(2);

    private readonly ILogger<MdnsServiceAdvertiser> _logger;
    private readonly IConfiguration _configuration;
    private readonly object _sync = new();

    private MulticastService? _mdns;
    private ServiceDiscovery? _discovery;
    private ServiceProfile? _profile;
    private int _port;
    private string _publishedFingerprint = string.Empty;
    private CancellationTokenSource? _debounceCts;
    private bool _networkHooked;
    private bool _disposed;

    public MdnsServiceAdvertiser(ILogger<MdnsServiceAdvertiser> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _port = ResolveAdvertisedPort();
        PublishOrRefresh(force: true);
        HookNetworkChanges();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        UnhookNetworkChanges();
        CancelDebounce();
        TearDownPublication();
        return Task.CompletedTask;
    }

    private void HookNetworkChanges()
    {
        if (_networkHooked)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _networkHooked = true;
        _logger.LogInformation("[Discovery] mDNS : écoute des changements d'interfaces / adresses");
    }

    private void UnhookNetworkChanges()
    {
        if (!_networkHooked)
        {
            return;
        }

        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _networkHooked = false;
    }

    private void OnNetworkChanged(object? sender, EventArgs e) => ScheduleRepublish("NetworkAddressChanged");

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) =>
        ScheduleRepublish($"NetworkAvailabilityChanged available={e.IsAvailable}");

    private void ScheduleRepublish(string reason)
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenSource cts;
        lock (_sync)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            cts = _debounceCts;
        }

        _logger.LogDebug("[Discovery] mDNS : republication planifiée ({Reason})", reason);
        _ = DebouncedRepublishAsync(cts.Token);
    }

    private async Task DebouncedRepublishAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(RepublishDebounce, token).ConfigureAwait(false);
            if (token.IsCancellationRequested || _disposed)
            {
                return;
            }

            PublishOrRefresh(force: false);
        }
        catch (OperationCanceledException)
        {
            // Nouvelle planification — ignorer.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discovery] mDNS : échec republication différée");
        }
    }

    private void CancelDebounce()
    {
        lock (_sync)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }

    private void PublishOrRefresh(bool force)
    {
        if (_disposed)
        {
            return;
        }

        lock (_sync)
        {
            try
            {
                var addresses = SelectAdvertisableIpv4Addresses();
                var fingerprint = BuildFingerprint(_port, addresses);
                if (!force && fingerprint == _publishedFingerprint)
                {
                    _logger.LogDebug("[Discovery] mDNS : adresses inchangées — pas de republication");
                    return;
                }

                TearDownPublicationUnlocked();

                if (addresses.Count == 0)
                {
                    _publishedFingerprint = string.Empty;
                    _logger.LogWarning("[Discovery] mDNS : aucune IP IPv4 LAN utilisable à publier");
                    return;
                }

                _mdns = new MulticastService();
                _discovery = new ServiceDiscovery(_mdns);
                _profile = new ServiceProfile(
                    DiscoveryConstants.ServiceInstanceName,
                    DiscoveryConstants.ServiceType,
                    (ushort)_port,
                    addresses);
                _profile.AddProperty("path", DiscoveryConstants.HealthPath);
                _profile.AddProperty("app", "school-management");

                _discovery.Advertise(_profile);
                _mdns.Start();
                _publishedFingerprint = fingerprint;

                _logger.LogInformation(
                    "[Discovery] Service mDNS publié : {Instance} ({Type}) port={Port} ips={Ips}",
                    DiscoveryConstants.ServiceInstanceName,
                    DiscoveryConstants.ServiceType,
                    _port,
                    string.Join(", ", addresses.Select(a => a.ToString())));
            }
            catch (Exception ex)
            {
                _publishedFingerprint = string.Empty;
                TearDownPublicationUnlocked();
                _logger.LogWarning(ex, "[Discovery] Impossible de publier mDNS");
            }
        }
    }

    private void TearDownPublication()
    {
        lock (_sync)
        {
            TearDownPublicationUnlocked();
            _publishedFingerprint = string.Empty;
        }
    }

    private void TearDownPublicationUnlocked()
    {
        try
        {
            if (_discovery is not null && _profile is not null)
            {
                _discovery.Unadvertise(_profile);
            }
        }
        catch
        {
            // ignore
        }

        try { _discovery?.Dispose(); } catch { /* ignore */ }
        try { _mdns?.Dispose(); } catch { /* ignore */ }
        _discovery = null;
        _mdns = null;
        _profile = null;
    }

    private int ResolveAdvertisedPort()
    {
        var configured = _configuration["LocalDiscovery:Port"];
        if (int.TryParse(configured, out var p) && p > 0)
        {
            return p;
        }

        var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
                   ?? _configuration["urls"]
                   ?? string.Empty;
        foreach (var part in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = part
                .Replace("0.0.0.0", "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                .Replace("*", "127.0.0.1", StringComparison.OrdinalIgnoreCase);
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Port > 0)
            {
                if (uri.Port == DiscoveryConstants.ApiPort)
                {
                    return uri.Port;
                }
            }
        }

        foreach (var part in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = part
                .Replace("0.0.0.0", "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                .Replace("*", "127.0.0.1", StringComparison.OrdinalIgnoreCase);
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Port > 0)
            {
                return uri.Port;
            }
        }

        return DiscoveryConstants.ApiPort;
    }

    /// <summary>
    /// Sélectionne les IPv4 privées utilisables pour la découverte LAN.
    /// Priorise Wi‑Fi / Ethernet. Les adaptateurs virtuels (VBox, Hyper‑V, VPN…)
    /// ne sont publiés que s'il n'existe aucune IP LAN préférée — ils ne doivent
    /// jamais masquer le Wi‑Fi/Ethernet réel.
    /// </summary>
    internal static IReadOnlyList<IPAddress> SelectAdvertisableIpv4Addresses()
    {
        var preferred = new List<(IPAddress Ip, int Rank)>();
        var otherLan = new List<IPAddress>();
        var virtualOnly = new List<IPAddress>();

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

            var virtualNic = IsLikelyVirtualAdapter(nic);
            var preferredNic = IsPreferredLanAdapter(nic);

            foreach (var uni in nic.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork
                    || IPAddress.IsLoopback(uni.Address))
                {
                    continue;
                }

                if (IsLinkLocal(uni.Address) || !IsPrivateIpv4(uni.Address))
                {
                    continue;
                }

                if (virtualNic || IsLikelyVirtualIpv4(uni.Address))
                {
                    virtualOnly.Add(uni.Address);
                    continue;
                }

                if (preferredNic)
                {
                    preferred.Add((uni.Address, PreferredRank(nic)));
                }
                else
                {
                    otherLan.Add(uni.Address);
                }
            }
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<IPAddress>();

        void Add(IPAddress ip)
        {
            var key = ip.ToString();
            if (seen.Add(key))
            {
                result.Add(ip);
            }
        }

        foreach (var item in preferred.OrderBy(p => p.Rank).ThenBy(p => p.Ip.ToString(), StringComparer.Ordinal))
        {
            Add(item.Ip);
        }

        foreach (var ip in otherLan.OrderBy(a => a.ToString(), StringComparer.Ordinal))
        {
            Add(ip);
        }

        // Virtuelles uniquement si aucun LAN réel (évite le cas « seulement VBox/Hyper-V »).
        if (result.Count == 0)
        {
            foreach (var ip in virtualOnly.OrderBy(a => a.ToString(), StringComparer.Ordinal))
            {
                Add(ip);
            }
        }

        return result;
    }

    private static int PreferredRank(NetworkInterface nic) =>
        nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => 0,
            NetworkInterfaceType.Ethernet => 1,
            NetworkInterfaceType.GigabitEthernet => 1,
            NetworkInterfaceType.FastEthernetFx => 2,
            NetworkInterfaceType.FastEthernetT => 2,
            _ => 5
        };

    private static bool IsPreferredLanAdapter(NetworkInterface nic) =>
        nic.NetworkInterfaceType is NetworkInterfaceType.Wireless80211
            or NetworkInterfaceType.Ethernet
            or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.FastEthernetT;

    private static bool IsLikelyVirtualAdapter(NetworkInterface nic)
    {
        var name = $"{nic.Name} {nic.Description}";
        return name.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase)
               || name.Contains("VMware", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
               || name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
               || name.Contains("WSL", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Docker", StringComparison.OrdinalIgnoreCase)
               || name.Contains("HotspotShield", StringComparison.OrdinalIgnoreCase)
               || name.Contains("VPN", StringComparison.OrdinalIgnoreCase)
               || name.Contains("TAP-Windows", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Loopback", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyVirtualIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        // VirtualBox host-only / NAT typiques 192.168.56–59.x
        if (bytes[0] == 192 && bytes[1] == 168 && bytes[2] is >= 56 and <= 59)
        {
            return true;
        }

        // Plages Hyper-V / WSL fréquentes 172.27–29.x
        if (bytes[0] == 172 && bytes[1] is >= 27 and <= 29)
        {
            return true;
        }

        return false;
    }

    private static bool IsLinkLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        return bytes[0] == 10
               || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
               || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static string BuildFingerprint(int port, IReadOnlyList<IPAddress> addresses) =>
        $"{port}|{string.Join(',', addresses.Select(a => a.ToString()).OrderBy(s => s, StringComparer.Ordinal))}";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnhookNetworkChanges();
        CancelDebounce();
        TearDownPublication();
    }
}
