namespace SchoolManagement.Application.CloudSync;

/// <summary>
/// Façade publique pour l'API / Desktop — ne révèle pas le détail outbox.
/// </summary>
public interface ICloudSyncFacade
{
    Task<DTOs.CloudSyncStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<DTOs.CloudSyncRunResultDto> SynchronizeNowAsync(
        bool criticalOnly = false,
        CancellationToken cancellationToken = default);
}
