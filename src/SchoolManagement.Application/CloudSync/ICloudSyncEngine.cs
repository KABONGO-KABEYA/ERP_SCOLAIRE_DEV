using SchoolManagement.Application.CloudSync.DTOs;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.CloudSync;

/// <summary>
/// Contrat du moteur de synchronisation Local → Cloud.
/// Remplaçable sans toucher au reste de l'application.
/// </summary>
public interface ICloudSyncEngine
{
    /// <summary>Vide la file outbox (priorité Critical d'abord), transaction par unité.</summary>
    Task<CloudSyncRunResultDto> DrainAsync(
        bool criticalOnly = false,
        int maxUnits = 50,
        CloudSyncDrainControl? control = null,
        CancellationToken cancellationToken = default);

    Task<CloudSyncStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Catch-up watermark → outbox pour les changements non fileés.</summary>
    Task EnqueueCatchUpAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Une seule fois : sync complète legacy si aucune outbox/watermark (migration v1 → v2).
    /// </summary>
    Task<bool> TryBootstrapFullSyncIfNeededAsync(CancellationToken cancellationToken = default);

    /// <summary>Repasse Failed / DeadLetter en Pending pour rejouer après correctif.</summary>
    Task<int> RequeueFailedUnitsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Enfilement des changements métier vers l'outbox (appelé après SaveChanges).</summary>
public interface ICloudSyncOutboxWriter
{
    Task EnqueueFromChangeSetAsync(
        IReadOnlyList<CloudSyncChange> changes,
        CancellationToken cancellationToken = default);
}

public sealed record CloudSyncChange(
    string TableName,
    Guid EntityId,
    SyncOperationType Operation,
    string? AggregateType,
    Guid? AggregateId,
    SyncPriority Priority);
