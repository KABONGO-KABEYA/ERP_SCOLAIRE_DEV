using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.CloudSync.DTOs;

namespace SchoolManagement.Infrastructure.CloudSync;

/// <summary>Façade publique — isole le moteur des contrôleurs / UI.</summary>
public sealed class CloudSyncFacade : ICloudSyncFacade
{
    private readonly ICloudSyncEngine _engine;

    public CloudSyncFacade(ICloudSyncEngine engine)
    {
        _engine = engine;
    }

    public Task<CloudSyncStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        _engine.GetStatusAsync(cancellationToken);

    public async Task<CloudSyncRunResultDto> SynchronizeNowAsync(
        bool criticalOnly = false,
        bool requeueDeadLetters = true,
        CancellationToken cancellationToken = default)
    {
        if (requeueDeadLetters)
        {
            await _engine.RequeueFailedUnitsAsync(cancellationToken);
        }

        await _engine.TryBootstrapFullSyncIfNeededAsync(cancellationToken);

        if (!criticalOnly)
        {
            await _engine.EnqueueCatchUpAsync(cancellationToken);
        }

        // Beaucoup d'unités peuvent être en file après un requeue (paiements).
        // Lots plus petits pour éviter les timeouts HTTP côté client Desktop.
        return await _engine.DrainAsync(
            criticalOnly,
            maxUnits: criticalOnly ? 25 : 80,
            cancellationToken);
    }
}
