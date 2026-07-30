namespace SchoolManagement.Application.Updates.DTOs;

public sealed class ApplicationVersionAdminDto
{
    public Guid Id { get; init; }
    public string Version { get; init; } = string.Empty;
    public string MinimumVersion { get; init; } = "1.0.0";
    public bool Mandatory { get; init; }
    public DateOnly ReleaseDate { get; init; }
    public IReadOnlyList<string> ReleaseNotes { get; init; } = Array.Empty<string>();
    public string? DesktopUrl { get; init; }
    public string? MobileUrl { get; init; }
    public string? Sha256 { get; init; }
    public long? Size { get; init; }
    public int SchemaVersion { get; init; }
    public bool Active { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class PublishApplicationVersionRequest
{
    public string Version { get; set; } = string.Empty;
    public string MinimumVersion { get; set; } = "1.0.0";
    public bool Mandatory { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public List<string> ReleaseNotes { get; set; } = [];
    public string? DesktopUrl { get; set; }
    public string? MobileUrl { get; set; }
    public string? Sha256 { get; set; }
    public long? Size { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public bool Active { get; set; } = true;
    public bool DeactivateOthers { get; set; } = true;
}
