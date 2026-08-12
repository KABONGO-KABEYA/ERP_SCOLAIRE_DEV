namespace SchoolManagement.Application.CloudSync;

/// <summary>
/// Options pour un drain outbox (production, diagnostic / test).
/// Valeurs par défaut du record = comportement minimal sans protections renforcées.
/// </summary>
public sealed record CloudSyncDrainControl(
    bool BypassActif = false,
    bool PendingOnly = false,
    bool VerifyCloudAfterCommit = false,
    bool RetryPendingOnDependencyError = false,
    bool SoftDeleteOrphanedLocalEntity = false)
{
    /// <summary>Profil utilisé par HostedService et Facade en production.</summary>
    public static CloudSyncDrainControl Production { get; } = new(
        BypassActif: false,
        PendingOnly: true,
        VerifyCloudAfterCommit: true,
        RetryPendingOnDependencyError: true,
        SoftDeleteOrphanedLocalEntity: true);
}
