namespace SchoolManagement.Bootstrap.API.Contracts;

public sealed class CreateUpdateReleaseRequest
{
    public string Version { get; set; } = string.Empty;

    public string Channel { get; set; } = "PROD";

    public int ProtocolVersion { get; set; } = 1;

    public int FromSchemaVersion { get; set; } = 1;

    public int SchemaVersion { get; set; }

    public string MinimumDesktopVersion { get; set; } = "0.0.0";

    public string MinimumApiVersion { get; set; } = "0.0.0";

    public bool Mandatory { get; set; }

    public List<string> ReleaseNotes { get; set; } = [];

    public string? CreatedBy { get; set; }

    public List<CreateUpdateReleaseArtifactRequest> Artifacts { get; set; } = [];

    public List<CreateUpdateReleaseTargetRequest> Targets { get; set; } = [];
}

public sealed class CreateUpdateReleaseArtifactRequest
{
    public string Type { get; set; } = string.Empty;

    public string? Version { get; set; }

    public string Url { get; set; } = string.Empty;

    public long? Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string? Signature { get; set; }
}

public sealed class CreateUpdateReleaseTargetRequest
{
    public Guid? SchoolId { get; set; }
}

public sealed class UpdateReleaseStatusRequest
{
    public string Status { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class UpdateReleaseResponse
{
    public required Guid ReleaseId { get; init; }

    public required string Version { get; init; }

    public required string Channel { get; init; }

    public required int ProtocolVersion { get; init; }

    public required int FromSchemaVersion { get; init; }

    public required int SchemaVersion { get; init; }

    public required string MinimumDesktopVersion { get; init; }

    public required string MinimumApiVersion { get; init; }

    public required bool Mandatory { get; init; }

    public required string Status { get; init; }

    public required IReadOnlyList<string> ReleaseNotes { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? PublishedAtUtc { get; init; }

    public DateTime? BlockedAtUtc { get; init; }

    public string? BlockedReason { get; init; }

    public string? CreatedBy { get; init; }

    public required IReadOnlyList<UpdateReleaseArtifactResponse> Artifacts { get; init; }

    public required IReadOnlyList<UpdateReleaseTargetResponse> Targets { get; init; }
}

public sealed class UpdateReleaseArtifactResponse
{
    public required Guid ArtifactId { get; init; }

    public required string Type { get; init; }

    public required string Version { get; init; }

    public required string Url { get; init; }

    public long? Size { get; init; }

    public required string Sha256 { get; init; }

    public string? Signature { get; init; }
}

public sealed class UpdateReleaseTargetResponse
{
    public required Guid TargetId { get; init; }

    public Guid? SchoolId { get; init; }
}

public sealed class UpdateReleaseCheckResponse
{
    public required Guid ReleaseId { get; init; }

    public required string Version { get; init; }

    public required string Channel { get; init; }

    public required string Status { get; init; }

    public required int ProtocolVersion { get; init; }

    public required int FromSchemaVersion { get; init; }

    public required int SchemaVersion { get; init; }

    public required string MinimumDesktopVersion { get; init; }

    public required string MinimumApiVersion { get; init; }

    public required bool Mandatory { get; init; }

    public DateTime? PublishedAtUtc { get; init; }

    public required IReadOnlyList<string> ReleaseNotes { get; init; }

    public required UpdateReleaseArtifactResponse Artifact { get; init; }
}

/// <summary>
/// Check agent : une seule release, artifacts Api + Migration de cette ligne.
/// Le check public Desktop reste <see cref="UpdateReleaseCheckResponse"/>.
/// </summary>
public sealed class UpdateAgentReleaseCheckResponse
{
    public required Guid ReleaseId { get; init; }

    public required string Version { get; init; }

    public required string Channel { get; init; }

    public required string Status { get; init; }

    public required int ProtocolVersion { get; init; }

    public required int FromSchemaVersion { get; init; }

    public required int SchemaVersion { get; init; }

    public required string MinimumDesktopVersion { get; init; }

    public required string MinimumApiVersion { get; init; }

    public required bool Mandatory { get; init; }

    public DateTime? PublishedAtUtc { get; init; }

    public required IReadOnlyList<string> ReleaseNotes { get; init; }

    public required UpdateReleaseArtifactResponse Api { get; init; }

    public required UpdateReleaseArtifactResponse Migration { get; init; }
}
