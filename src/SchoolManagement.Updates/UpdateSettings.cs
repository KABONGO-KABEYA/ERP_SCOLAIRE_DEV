using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolManagement.Updates;

public sealed class UpdateSettings
{
    public bool AutoCheckEnabled { get; set; } = true;
    public bool AutoDownloadOptional { get; set; }
    public bool AutoInstallOnNextRestart { get; set; }
    public string? LastCheckUtc { get; set; }
    public string? LastUpdateUtc { get; set; }
    public string? LastFoundVersion { get; set; }
    public string? SnoozeUntilUtc { get; set; }
    public string CurrentVersion { get; set; } = "1.0.0";
    public int CheckIntervalHours { get; set; } = 6;
    public string CheckEndpoint { get; set; } = "/api/v1/update/check";
    public List<string> AllowedHosts { get; set; } = ["localhost", "127.0.0.1", "169.58.93.203"];
}

public sealed class UpdateSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;
    private readonly object _gate = new();

    public UpdateSettingsStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "update-settings.json");
    }

    public UpdateSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_filePath))
            {
                var defaults = new UpdateSettings();
                Save(defaults);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<UpdateSettings>(json, JsonOptions) ?? new UpdateSettings();
            }
            catch
            {
                return new UpdateSettings();
            }
        }
    }

    public void Save(UpdateSettings settings)
    {
        lock (_gate)
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
    }
}
