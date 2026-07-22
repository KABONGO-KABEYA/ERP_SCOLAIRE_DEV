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
        CancellationToken cancellationToken = default)
    {
        await _engine.TryBootstrapFullSyncIfNeededAsync(cancellationToken);

        if (!criticalOnly)
        {
            await _engine.EnqueueCatchUpAsync(cancellationToken);
        }

        return await _engine.DrainAsync(criticalOnly, maxUnits: criticalOnly ? 100 : 200, cancellationToken);
    }
}
