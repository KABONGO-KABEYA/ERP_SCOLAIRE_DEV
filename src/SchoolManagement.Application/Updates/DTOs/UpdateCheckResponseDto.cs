namespace SchoolManagement.Application.Updates.DTOs;

public sealed class UpdateCheckResponseDto
{
    public string LatestVersion { get; init; } = "0.0.0";
    public string MinimumVersion { get; init; } = "0.0.0";
    public bool Mandatory { get; init; }
    public DateOnly ReleaseDate { get; init; }
    public IReadOnlyList<string> ReleaseNotes { get; init; } = Array.Empty<string>();
    public string? DesktopUrl { get; init; }
    public string? MobileUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? Sha256 { get; init; }
    public long? Size { get; init; }
    public int SchemaVersion { get; init; }
}
