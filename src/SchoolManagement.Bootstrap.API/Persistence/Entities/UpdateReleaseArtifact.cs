namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

public sealed class UpdateReleaseArtifact
{
    public Guid ArtifactId { get; set; } = Guid.NewGuid();

    public Guid ReleaseId { get; set; }

    public string Type { get; set; } = UpdateReleaseArtifactTypes.Desktop;

    public string Version { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public long? Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string? Signature { get; set; }

    public UpdateRelease Release { get; set; } = null!;
}
