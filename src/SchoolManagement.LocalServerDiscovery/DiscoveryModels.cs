namespace SchoolManagement.LocalServerDiscovery;

public enum DiscoverySource
{
    Unknown = 0,
    Mdns = 1,
    LastKnown = 2,
    SubnetScan = 3,
    Remote = 4
}

public enum DiscoveryMode
{
    Detecting = 0,
    Local = 1,
    Remote = 2,
    Offline = 3
}

public sealed record HealthInfo(
    string Status,
    string Server,
    string School,
    string Version,
    DateTimeOffset Time);

public sealed record DiscoveryResult(
    DiscoveryMode Mode,
    DiscoverySource Source,
    string? BaseUrl,
    HealthInfo? Health,
    string Message)
{
    public static DiscoveryResult Detecting(string message = "Recherche du serveur…") =>
        new(DiscoveryMode.Detecting, DiscoverySource.Unknown, null, null, message);

    public static DiscoveryResult Offline(string message) =>
        new(DiscoveryMode.Offline, DiscoverySource.Unknown, null, null, message);

    public bool IsLocal => Mode == DiscoveryMode.Local && !string.IsNullOrWhiteSpace(BaseUrl);
    public bool IsRemote => Mode == DiscoveryMode.Remote && !string.IsNullOrWhiteSpace(BaseUrl);
}
