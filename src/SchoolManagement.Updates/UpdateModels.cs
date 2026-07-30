namespace SchoolManagement.Updates;

public enum UpdateClientPlatform
{
    Desktop = 0,
    Mobile = 1
}

public sealed class UpdateManifest
{
    public string LatestVersion { get; init; } = "0.0.0";
    public string MinimumVersion { get; init; } = "0.0.0";
    public bool Mandatory { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IReadOnlyList<string> ReleaseNotes { get; init; } = Array.Empty<string>();
    public string? DesktopUrl { get; init; }
    public string? MobileUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? Sha256 { get; init; }
    public long? Size { get; init; }
    public int? SchemaVersion { get; init; }
}

public enum UpdateAvailability
{
    UpToDate,
    Optional,
    Mandatory
}

public sealed class UpdateCheckOutcome
{
    public required UpdateAvailability Availability { get; init; }
    public required string CurrentVersion { get; init; }
    public UpdateManifest? Manifest { get; init; }
    public string? Message { get; init; }
}
