using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolManagement.Updates;

public enum UpdateHistoryResult
{
    CheckOk,
    CheckFailed,
    UpToDate,
    OptionalAvailable,
    MandatoryAvailable,
    DownloadStarted,
    DownloadSucceeded,
    DownloadCancelled,
    DownloadFailed,
    HashInvalid,
    InstallStarted,
    InstallSucceeded,
    InstallFailed,
    MigrationFailed,
    Snoozed
}

public sealed class UpdateHistoryEntry
{
    public DateTime Utc { get; init; } = DateTime.UtcNow;
    public string? VersionFound { get; init; }
    public UpdateHistoryResult Result { get; init; }
    public string? Detail { get; init; }
}

public sealed class UpdateHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly object _gate = new();
    private const int MaxEntries = 200;

    public UpdateHistoryStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "update-history.json");
    }

    public IReadOnlyList<UpdateHistoryEntry> Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<UpdateHistoryEntry>();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<UpdateHistoryEntry>>(json, JsonOptions)
                       ?? new List<UpdateHistoryEntry>();
            }
            catch
            {
                return Array.Empty<UpdateHistoryEntry>();
            }
        }
    }

    public void Append(UpdateHistoryEntry entry)
    {
        lock (_gate)
        {
            var list = Load().ToList();
            list.Insert(0, entry);
            if (list.Count > MaxEntries)
            {
                list = list.Take(MaxEntries).ToList();
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(list, JsonOptions));
        }
    }
}
