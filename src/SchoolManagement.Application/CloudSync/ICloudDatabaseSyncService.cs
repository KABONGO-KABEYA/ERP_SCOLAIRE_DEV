namespace SchoolManagement.Application.CloudSync;

public sealed class CloudSyncResult
{
    public bool Skipped { get; init; }

    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public int TablesSynced { get; init; }

    public int RowsUpserted { get; init; }

    public TimeSpan Duration { get; init; }

    public static CloudSyncResult Skip(string message) =>
        new() { Skipped = true, Success = true, Message = message };

    public static CloudSyncResult Ok(int tables, int rows, TimeSpan duration) =>
        new()
        {
            Success = true,
            TablesSynced = tables,
            RowsUpserted = rows,
            Duration = duration,
            Message = $"Sync OK — {tables} table(s), {rows} ligne(s) en {duration.TotalSeconds:0.0}s."
        };

    public static CloudSyncResult Fail(string message) =>
        new() { Success = false, Message = message };
}

public interface ICloudDatabaseSyncService
{
    /// <summary>
    /// Vérifie l'accès Internet / SQL distant, puis pousse les données locales vers le cloud.
    /// Sens unique : local (réseau) → distant (en ligne).
    /// </summary>
    Task<CloudSyncResult> TrySyncAsync(CancellationToken cancellationToken = default);
}
