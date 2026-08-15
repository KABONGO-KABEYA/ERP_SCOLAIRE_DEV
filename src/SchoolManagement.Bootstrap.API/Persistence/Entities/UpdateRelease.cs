namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

public sealed class UpdateRelease
{
    public Guid ReleaseId { get; set; } = Guid.NewGuid();

    public string Version { get; set; } = string.Empty;

    public string Channel { get; set; } = UpdateReleaseChannels.Prod;

    public int ProtocolVersion { get; set; } = 1;

    public int FromSchemaVersion { get; set; } = 1;

    public int SchemaVersion { get; set; }

    public string MinimumDesktopVersion { get; set; } = "0.0.0";

    public string MinimumApiVersion { get; set; } = "0.0.0";

    public bool Mandatory { get; set; }

    public string Status { get; set; } = UpdateReleaseStatuses.Draft;

    public string ReleaseNotes { get; set; } = "[]";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? PublishedAtUtc { get; set; }

    public DateTime? BlockedAtUtc { get; set; }

    public string? BlockedReason { get; set; }

    public string? CreatedBy { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<UpdateReleaseArtifact> Artifacts { get; set; } = [];

    public ICollection<UpdateReleaseTarget> Targets { get; set; } = [];
}
