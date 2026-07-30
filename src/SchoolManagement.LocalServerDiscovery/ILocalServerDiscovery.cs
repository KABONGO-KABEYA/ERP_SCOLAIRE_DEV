namespace SchoolManagement.LocalServerDiscovery;

/// <summary>
/// Porte d'entrée unique pour découvrir le serveur API local ou distant.
/// </summary>
public interface ILocalServerDiscovery
{
    DiscoveryResult Current { get; }

    event EventHandler<DiscoveryResult>? Changed;

    Task<DiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Relance une découverte complète (annule la précédente).
    /// </summary>
    Task<DiscoveryResult> RediscoverAsync(CancellationToken cancellationToken = default);
}

public interface ILastKnownEndpointStore
{
    Task<string?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string baseUrl, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface IHealthProbe
{
    Task<HealthInfo?> ProbeAsync(string baseUrl, TimeSpan timeout, CancellationToken cancellationToken = default);
}
