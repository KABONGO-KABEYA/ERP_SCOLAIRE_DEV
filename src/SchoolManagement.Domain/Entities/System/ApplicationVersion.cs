namespace SchoolManagement.Domain.Entities.System;

public sealed class ApplicationVersion
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string MinimumVersion { get; set; } = "1.0.0";
    public bool Mandatory { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public string ReleaseNotes { get; set; } = "[]";
    public string? DesktopUrl { get; set; }
    public string? MobileUrl { get; set; }
    public string? Sha256 { get; set; }
    public long? Size { get; set; }
    public int SchemaVersion { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
