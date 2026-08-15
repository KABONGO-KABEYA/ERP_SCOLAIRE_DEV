using SchoolManagement.Updates;

namespace SchoolManagement.UpdateAgent;

public sealed class AgentReleasePlan
{
    public required Guid ReleaseId { get; init; }

    public required string Version { get; init; }

    public required int ProtocolVersion { get; init; }

    public required int FromSchemaVersion { get; init; }

    public required int SchemaVersion { get; init; }

    public required AgentArtifactDto Api { get; init; }

    public required AgentArtifactDto Migration { get; init; }
}

public static class ReleaseCheckGuard
{
    public static AgentReleasePlan? Accept(AgentCheckResult check)
    {
        if (check.StatusCode == System.Net.HttpStatusCode.NoContent || check.Body is null)
        {
            return null;
        }

        var body = check.Body;
        if (IsDesktopOnly(body))
        {
            throw new AgentException("Release Desktop seule : aucune installation.");
        }

        if (body.Api is null || body.Migration is null)
        {
            throw new AgentException("Release serveur incomplète (Api + Migration requis).");
        }

        if (!IsType(body.Api, "Api") || !IsType(body.Migration, "Migration"))
        {
            throw new AgentException("Types d'artifacts Api/Migration invalides.");
        }

        var version = VersionManager.Parse(body.Version).ToNormalizedString();
        EnsureArtifactMatchesRelease(body.Api, version, body.ReleaseId);
        EnsureArtifactMatchesRelease(body.Migration, version, body.ReleaseId);
        if (!string.Equals(
                VersionManager.Parse(body.Api.Version).ToNormalizedString(),
                VersionManager.Parse(body.Migration.Version).ToNormalizedString(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentException("ReleaseId / version d'artifact incohérents entre Api et Migration.");
        }

        if (string.IsNullOrWhiteSpace(body.Api.Sha256) || string.IsNullOrWhiteSpace(body.Migration.Sha256))
        {
            throw new AgentException("SHA256 catalogue manquant.");
        }

        return new AgentReleasePlan
        {
            ReleaseId = body.ReleaseId,
            Version = version,
            ProtocolVersion = body.ProtocolVersion,
            FromSchemaVersion = body.FromSchemaVersion,
            SchemaVersion = body.SchemaVersion,
            Api = body.Api,
            Migration = body.Migration,
        };
    }

    private static bool IsDesktopOnly(AgentReleaseCheckDto body)
    {
        if (body.Artifact is not null && IsType(body.Artifact, "Desktop")
            && body.Api is null && body.Migration is null)
        {
            return true;
        }

        return IsType(body.Api, "Desktop") || IsType(body.Migration, "Desktop");
    }

    private static bool IsType(AgentArtifactDto? artifact, string type) =>
        artifact is not null
        && string.Equals(artifact.Type, type, StringComparison.OrdinalIgnoreCase);

    private static void EnsureArtifactMatchesRelease(AgentArtifactDto artifact, string releaseVersion, Guid releaseId)
    {
        if (artifact.ReleaseId is { } artifactReleaseId
            && artifactReleaseId != Guid.Empty
            && artifactReleaseId != releaseId)
        {
            throw new AgentException("ReleaseId d'artifact différent de la release.");
        }

        var artifactVersion = VersionManager.Parse(artifact.Version).ToNormalizedString();
        if (!string.Equals(artifactVersion, releaseVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentException("Version d'artifact différente de la release.");
        }
    }
}
