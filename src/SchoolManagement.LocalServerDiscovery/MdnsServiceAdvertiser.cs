using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Makaretu.Dns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.LocalServerDiscovery;

/// <summary>
/// Publie le service mDNS school-management (à enregistrer côté API).
/// </summary>
public sealed class MdnsServiceAdvertiser : IHostedService, IDisposable
{
    private readonly ILogger<MdnsServiceAdvertiser> _logger;
    private readonly IConfiguration _configuration;
    private MulticastService? _mdns;
    private ServiceDiscovery? _discovery;
    private ServiceProfile? _profile;

    public MdnsServiceAdvertiser(ILogger<MdnsServiceAdvertiser> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var port = ResolveAdvertisedPort();
            var addresses = GetLocalIpv4Addresses().ToArray();
            if (addresses.Length == 0)
            {
                _logger.LogWarning("[Discovery] mDNS : aucune IP IPv4 locale à publier");
                return Task.CompletedTask;
            }

            _mdns = new MulticastService();
            _discovery = new ServiceDiscovery(_mdns);
            _profile = new ServiceProfile(
                DiscoveryConstants.ServiceInstanceName,
                DiscoveryConstants.ServiceType,
                (ushort)port,
                addresses);
            _profile.AddProperty("path", DiscoveryConstants.HealthPath);
            _profile.AddProperty("app", "school-management");

            _discovery.Advertise(_profile);
            _mdns.Start();

            _logger.LogInformation(
                "[Discovery] Service mDNS publié : {Instance} ({Type}) port={Port} ips={Ips}",
                DiscoveryConstants.ServiceInstanceName,
                DiscoveryConstants.ServiceType,
                port,
                string.Join(", ", addresses.Select(a => a.ToString())));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Discovery] Impossible de publier mDNS");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
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

        Dispose();
        return Task.CompletedTask;
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
                // Préfère 5096 s'il est listé.
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

    private static IEnumerable<IPAddress> GetLocalIpv4Addresses()
    {
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

                yield return uni.Address;
            }
        }
    }

    public void Dispose()
    {
        try { _discovery?.Dispose(); } catch { /* ignore */ }
        try { _mdns?.Dispose(); } catch { /* ignore */ }
        _discovery = null;
        _mdns = null;
    }
}
