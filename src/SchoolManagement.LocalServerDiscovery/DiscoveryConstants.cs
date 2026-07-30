namespace SchoolManagement.LocalServerDiscovery;

/// <summary>
/// Contrat partagé Desktop / Mobile / API pour la découverte locale.
/// </summary>
public static class DiscoveryConstants
{
    public const string ServiceInstanceName = "school-server";
    public const string ServiceType = "_school-management._tcp";
    public const string ServiceTypeLocal = "_school-management._tcp.local.";
    public const string HostName = "school-server.local";
    public const int ApiPort = 5096;
    public const string HealthPath = "/api/health";
    public const string DefaultRemoteBaseUrl = "http://169.58.93.203:1804";
    public const string PlaceholderBaseUrl = "http://discovery.local/";

    public static readonly TimeSpan MdnsTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan LastKnownTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan ScanProbeTimeout = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan BackgroundRecheckInterval = TimeSpan.FromSeconds(30);
    public const int ScanMaxParallelism = 32;
}
