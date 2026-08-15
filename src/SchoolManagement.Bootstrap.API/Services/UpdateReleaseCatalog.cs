using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.ServerIdentity;
using SchoolManagement.Bootstrap.API.Contracts;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;
using SchoolManagement.Bootstrap.API.Security;

namespace SchoolManagement.Bootstrap.API.Services;

public interface IUpdateReleaseCatalog
{
    Task<UpdateReleaseResponse> CreateDraftAsync(CreateUpdateReleaseRequest request, CancellationToken cancellationToken);

    Task<UpdateReleaseResponse?> GetByIdAsync(Guid releaseId, bool includeNonPublished, CancellationToken cancellationToken);

    Task<UpdateReleaseCheckResponse?> CheckAsync(
        string? channel,
        Guid? schoolId,
        string? artifactType,
        CancellationToken cancellationToken);

    Task<UpdateAgentReleaseCheckResponse?> CheckForAgentAsync(
        string? channel,
        Guid? schoolId,
        CancellationToken cancellationToken);

    Task<UpdateReleaseResponse> ChangeStatusAsync(
        Guid releaseId,
        UpdateReleaseStatusRequest request,
        CancellationToken cancellationToken);

    Task DeleteDraftAsync(Guid releaseId, CancellationToken cancellationToken);
}

public sealed class UpdateReleaseCatalog : IUpdateReleaseCatalog
{
    private readonly BootstrapDbContext _db;

    public UpdateReleaseCatalog(BootstrapDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateReleaseResponse> CreateDraftAsync(
        CreateUpdateReleaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "Corps de requête requis.");
        }

        if (!UpdateReleaseChannels.IsKnown(request.Channel))
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "Channel invalide. Utilisez DEV ou PROD.");
        }

        var channel = UpdateReleaseChannels.Normalize(request.Channel);
        if (!ReleaseSemVer.TryNormalize(request.Version, out var version))
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "Version SemVer invalide.");
        }

        if (!ReleaseSemVer.TryNormalize(request.MinimumDesktopVersion, out var minDesktop))
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "MinimumDesktopVersion SemVer invalide.");
        }

        if (!ReleaseSemVer.TryNormalize(request.MinimumApiVersion, out var minApi))
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "MinimumApiVersion SemVer invalide.");
        }

        if (request.ProtocolVersion < 0 || request.SchemaVersion < 0)
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "ProtocolVersion et SchemaVersion doivent être ≥ 0.");
        }

        if (request.FromSchemaVersion < 1)
        {
            throw new CatalogException(
                StatusCodes.Status400BadRequest,
                "FromSchemaVersion doit être ≥ 1 (baseline AppSchemaVersion).");
        }

        if (request.SchemaVersion < request.FromSchemaVersion)
        {
            throw new CatalogException(
                StatusCodes.Status400BadRequest,
                "SchemaVersion (to) ne peut pas être inférieur à FromSchemaVersion.");
        }

        var artifacts = BuildArtifacts(request.Artifacts, version, channel);
        EnsureApiMigrationPair(artifacts);
        if (artifacts.Any(a => a.Type == UpdateReleaseArtifactTypes.Api)
            && request.ProtocolVersion != ConnectionProtocolConstants.ProtocolVersion)
        {
            throw new CatalogException(
                StatusCodes.Status400BadRequest,
                $"ProtocolVersion doit être {ConnectionProtocolConstants.ProtocolVersion} lorsqu'un artifact Api est présent.");
        }

        var duplicate = await _db.UpdateReleases.AnyAsync(
            r => r.Channel == channel && r.Version == version,
            cancellationToken);
        if (duplicate)
        {
            throw new CatalogException(
                StatusCodes.Status409Conflict,
                $"Une release {channel} {version} existe déjà.");
        }

        var targets = await BuildTargetsAsync(request.Targets, cancellationToken);

        var release = new UpdateRelease
        {
            ReleaseId = Guid.NewGuid(),
            Version = version,
            Channel = channel,
            ProtocolVersion = request.ProtocolVersion,
            FromSchemaVersion = request.FromSchemaVersion,
            SchemaVersion = request.SchemaVersion,
            MinimumDesktopVersion = minDesktop,
            MinimumApiVersion = minApi,
            Mandatory = request.Mandatory,
            Status = UpdateReleaseStatuses.Draft,
            ReleaseNotes = SerializeNotes(request.ReleaseNotes),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = string.IsNullOrWhiteSpace(request.CreatedBy) ? null : request.CreatedBy.Trim(),
            Artifacts = artifacts,
            Targets = targets,
        };

        _db.UpdateReleases.Add(release);
        await _db.SaveChangesAsync(cancellationToken);
        return MapRelease(release);
    }

    public async Task<UpdateReleaseResponse?> GetByIdAsync(
        Guid releaseId,
        bool includeNonPublished,
        CancellationToken cancellationToken)
    {
        var release = await LoadReleaseAsync(releaseId, cancellationToken);
        if (release is null)
        {
            return null;
        }

        if (!includeNonPublished
            && !string.Equals(release.Status, UpdateReleaseStatuses.Published, StringComparison.Ordinal))
        {
            return null;
        }

        return MapRelease(release);
    }

    public async Task<UpdateReleaseCheckResponse?> CheckAsync(
        string? channel,
        Guid? schoolId,
        string? artifactType,
        CancellationToken cancellationToken)
    {
        var resolvedChannel = string.IsNullOrWhiteSpace(channel)
            ? UpdateReleaseChannels.Prod
            : channel.Trim();
        if (!UpdateReleaseChannels.IsKnown(resolvedChannel))
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "Channel invalide. Utilisez DEV ou PROD.");
        }

        resolvedChannel = UpdateReleaseChannels.Normalize(resolvedChannel);

        var type = string.IsNullOrWhiteSpace(artifactType)
            ? UpdateReleaseArtifactTypes.Desktop
            : artifactType.Trim();
        if (!UpdateReleaseArtifactTypes.IsKnown(type))
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "Type d'artifact invalide.");
        }

        type = UpdateReleaseArtifactTypes.Normalize(type);

        // schoolId is a targeting filter only — never an identity proof or deploy grant.
        var candidates = await _db.UpdateReleases
            .AsNoTracking()
            .Include(r => r.Artifacts)
            .Include(r => r.Targets)
            .Where(r => r.Channel == resolvedChannel && r.Status == UpdateReleaseStatuses.Published)
            .ToListAsync(cancellationToken);

        var matching = candidates
            .Where(r => MatchesTarget(r, schoolId) && r.Artifacts.Any(a => a.Type == type))
            .OrderByDescending(r => r, Comparer<UpdateRelease>.Create((a, b) =>
            {
                var cmp = ReleaseSemVer.Compare(a.Version, b.Version);
                if (cmp != 0)
                {
                    return cmp;
                }

                return Nullable.Compare(a.PublishedAtUtc, b.PublishedAtUtc);
            }))
            .FirstOrDefault();

        if (matching is null)
        {
            return null;
        }

        var artifact = matching.Artifacts.First(a => a.Type == type);
        return new UpdateReleaseCheckResponse
        {
            ReleaseId = matching.ReleaseId,
            Version = matching.Version,
            Channel = matching.Channel,
            Status = matching.Status,
            ProtocolVersion = matching.ProtocolVersion,
            FromSchemaVersion = matching.FromSchemaVersion,
            SchemaVersion = matching.SchemaVersion,
            MinimumDesktopVersion = matching.MinimumDesktopVersion,
            MinimumApiVersion = matching.MinimumApiVersion,
            Mandatory = matching.Mandatory,
            PublishedAtUtc = matching.PublishedAtUtc,
            ReleaseNotes = ParseNotes(matching.ReleaseNotes),
            Artifact = MapArtifact(artifact),
        };
    }

    public async Task<UpdateAgentReleaseCheckResponse?> CheckForAgentAsync(
        string? channel,
        Guid? schoolId,
        CancellationToken cancellationToken)
    {
        var resolvedChannel = string.IsNullOrWhiteSpace(channel)
            ? UpdateReleaseChannels.Prod
            : channel.Trim();
        if (!UpdateReleaseChannels.IsKnown(resolvedChannel))
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "Channel invalide. Utilisez DEV ou PROD.");
        }

        resolvedChannel = UpdateReleaseChannels.Normalize(resolvedChannel);

        var candidates = await _db.UpdateReleases
            .AsNoTracking()
            .Include(r => r.Artifacts)
            .Include(r => r.Targets)
            .Where(r => r.Channel == resolvedChannel && r.Status == UpdateReleaseStatuses.Published)
            .ToListAsync(cancellationToken);

        var matching = candidates
            .Where(r =>
                MatchesTarget(r, schoolId)
                && r.Artifacts.Any(a => a.Type == UpdateReleaseArtifactTypes.Api)
                && r.Artifacts.Any(a => a.Type == UpdateReleaseArtifactTypes.Migration))
            .OrderByDescending(r => r, Comparer<UpdateRelease>.Create((a, b) =>
            {
                var cmp = ReleaseSemVer.Compare(a.Version, b.Version);
                if (cmp != 0)
                {
                    return cmp;
                }

                return Nullable.Compare(a.PublishedAtUtc, b.PublishedAtUtc);
            }))
            .FirstOrDefault();

        if (matching is null)
        {
            return null;
        }

        var api = matching.Artifacts.First(a => a.Type == UpdateReleaseArtifactTypes.Api);
        var migration = matching.Artifacts.First(a => a.Type == UpdateReleaseArtifactTypes.Migration);
        return new UpdateAgentReleaseCheckResponse
        {
            ReleaseId = matching.ReleaseId,
            Version = matching.Version,
            Channel = matching.Channel,
            Status = matching.Status,
            ProtocolVersion = matching.ProtocolVersion,
            FromSchemaVersion = matching.FromSchemaVersion,
            SchemaVersion = matching.SchemaVersion,
            MinimumDesktopVersion = matching.MinimumDesktopVersion,
            MinimumApiVersion = matching.MinimumApiVersion,
            Mandatory = matching.Mandatory,
            PublishedAtUtc = matching.PublishedAtUtc,
            ReleaseNotes = ParseNotes(matching.ReleaseNotes),
            Api = MapArtifact(api),
            Migration = MapArtifact(migration),
        };
    }

    public async Task<UpdateReleaseResponse> ChangeStatusAsync(
        Guid releaseId,
        UpdateReleaseStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || !UpdateReleaseStatuses.IsKnown(request.Status))
        {
            throw new CatalogException(
                StatusCodes.Status400BadRequest,
                "Statut invalide. Utilisez Published ou Blocked.");
        }

        var targetStatus = UpdateReleaseStatuses.Normalize(request.Status);
        var release = await LoadReleaseAsync(releaseId, cancellationToken)
                      ?? throw new CatalogException(StatusCodes.Status404NotFound, "Release introuvable.");

        if (string.Equals(release.Status, targetStatus, StringComparison.Ordinal))
        {
            return MapRelease(release);
        }

        if (string.Equals(release.Status, UpdateReleaseStatuses.Published, StringComparison.Ordinal)
            && string.Equals(targetStatus, UpdateReleaseStatuses.Draft, StringComparison.Ordinal))
        {
            throw new CatalogException(StatusCodes.Status409Conflict, "Une release publiée est immutable (Published → Draft interdit).");
        }

        if (string.Equals(release.Status, UpdateReleaseStatuses.Blocked, StringComparison.Ordinal)
            && string.Equals(targetStatus, UpdateReleaseStatuses.Published, StringComparison.Ordinal))
        {
            throw new CatalogException(StatusCodes.Status409Conflict, "Blocked → Published est interdit.");
        }

        if (string.Equals(targetStatus, UpdateReleaseStatuses.Published, StringComparison.Ordinal))
        {
            if (!string.Equals(release.Status, UpdateReleaseStatuses.Draft, StringComparison.Ordinal))
            {
                throw new CatalogException(StatusCodes.Status409Conflict, "Seule une release Draft peut être publiée.");
            }

            ValidateReadyToPublish(release);
            release.Status = UpdateReleaseStatuses.Published;
            release.PublishedAtUtc = DateTime.UtcNow;
        }
        else if (string.Equals(targetStatus, UpdateReleaseStatuses.Blocked, StringComparison.Ordinal))
        {
            if (string.Equals(release.Status, UpdateReleaseStatuses.Draft, StringComparison.Ordinal)
                || string.Equals(release.Status, UpdateReleaseStatuses.Published, StringComparison.Ordinal))
            {
                if (string.Equals(release.Status, UpdateReleaseStatuses.Published, StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(request.Reason))
                {
                    throw new CatalogException(
                        StatusCodes.Status400BadRequest,
                        "La raison est obligatoire pour bloquer une release publiée.");
                }

                release.Status = UpdateReleaseStatuses.Blocked;
                release.BlockedAtUtc = DateTime.UtcNow;
                release.BlockedReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            }
            else
            {
                throw new CatalogException(StatusCodes.Status409Conflict, "Transition de statut interdite.");
            }
        }
        else
        {
            throw new CatalogException(StatusCodes.Status409Conflict, "Transition de statut interdite.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return MapRelease(release);
    }

    public async Task DeleteDraftAsync(Guid releaseId, CancellationToken cancellationToken)
    {
        var release = await _db.UpdateReleases.FirstOrDefaultAsync(r => r.ReleaseId == releaseId, cancellationToken)
                      ?? throw new CatalogException(StatusCodes.Status404NotFound, "Release introuvable.");

        if (!string.Equals(release.Status, UpdateReleaseStatuses.Draft, StringComparison.Ordinal))
        {
            throw new CatalogException(
                StatusCodes.Status409Conflict,
                "Suppression physique d'une release publiée ou bloquée interdite.");
        }

        _db.UpdateReleases.Remove(release);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<UpdateRelease?> LoadReleaseAsync(Guid releaseId, CancellationToken cancellationToken) =>
        await _db.UpdateReleases
            .Include(r => r.Artifacts)
            .Include(r => r.Targets)
            .FirstOrDefaultAsync(r => r.ReleaseId == releaseId, cancellationToken);

    private static List<UpdateReleaseArtifact> BuildArtifacts(
        IReadOnlyList<CreateUpdateReleaseArtifactRequest> items,
        string releaseVersion,
        string channel)
    {
        if (items is null || items.Count == 0)
        {
            throw new CatalogException(StatusCodes.Status400BadRequest, "Au moins un artifact est requis.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var artifacts = new List<UpdateReleaseArtifact>();
        foreach (var item in items)
        {
            if (!UpdateReleaseArtifactTypes.IsKnown(item.Type))
            {
                throw new CatalogException(StatusCodes.Status400BadRequest, "Type d'artifact invalide.");
            }

            var type = UpdateReleaseArtifactTypes.Normalize(item.Type);
            if (!seen.Add(type))
            {
                throw new CatalogException(StatusCodes.Status400BadRequest, $"Type d'artifact dupliqué : {type}.");
            }

            if (!ReleaseSemVer.TryNormalizeSha256(item.Sha256, out var sha, out var shaError))
            {
                throw new CatalogException(StatusCodes.Status400BadRequest, shaError!);
            }

            if (!ReleaseArtifactUrlGuard.TryValidate(item.Url, channel, out var urlError))
            {
                throw new CatalogException(StatusCodes.Status400BadRequest, urlError!);
            }

            if (item.Size is < 0)
            {
                throw new CatalogException(StatusCodes.Status400BadRequest, "La taille de l'artifact doit être ≥ 0.");
            }

            var artifactVersion = string.IsNullOrWhiteSpace(item.Version)
                ? releaseVersion
                : item.Version.Trim();
            if (!ReleaseSemVer.TryNormalize(artifactVersion, out var normalizedArtifactVersion))
            {
                throw new CatalogException(StatusCodes.Status400BadRequest, "Version d'artifact SemVer invalide.");
            }

            if (!string.Equals(normalizedArtifactVersion, releaseVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new CatalogException(
                    StatusCodes.Status400BadRequest,
                    "La version d'artifact doit être identique à la version de release.");
            }

            artifacts.Add(new UpdateReleaseArtifact
            {
                ArtifactId = Guid.NewGuid(),
                Type = type,
                Version = normalizedArtifactVersion,
                Url = item.Url.Trim(),
                Size = item.Size,
                Sha256 = sha,
                Signature = string.IsNullOrWhiteSpace(item.Signature) ? null : item.Signature.Trim(),
            });
        }

        return artifacts;
    }

    private async Task<List<UpdateReleaseTarget>> BuildTargetsAsync(
        IReadOnlyList<CreateUpdateReleaseTargetRequest>? items,
        CancellationToken cancellationToken)
    {
        if (items is null || items.Count == 0)
        {
            return [new UpdateReleaseTarget { TargetId = Guid.NewGuid(), SchoolId = null }];
        }

        var hasGlobal = false;
        var schools = new HashSet<Guid>();
        var targets = new List<UpdateReleaseTarget>();
        foreach (var item in items)
        {
            if (item.SchoolId is null)
            {
                if (hasGlobal)
                {
                    throw new CatalogException(StatusCodes.Status400BadRequest, "Une seule cible globale (SchoolId null) est autorisée.");
                }

                hasGlobal = true;
                targets.Add(new UpdateReleaseTarget { TargetId = Guid.NewGuid(), SchoolId = null });
                continue;
            }

            if (!schools.Add(item.SchoolId.Value))
            {
                throw new CatalogException(StatusCodes.Status400BadRequest, "SchoolId de cible dupliqué.");
            }

            var exists = await _db.SchoolRegistry.AnyAsync(
                s => s.SchoolId == item.SchoolId.Value,
                cancellationToken);
            if (!exists)
            {
                throw new CatalogException(
                    StatusCodes.Status400BadRequest,
                    $"École {item.SchoolId.Value:D} introuvable dans le registre Bootstrap.");
            }

            targets.Add(new UpdateReleaseTarget { TargetId = Guid.NewGuid(), SchoolId = item.SchoolId.Value });
        }

        if (hasGlobal && schools.Count > 0)
        {
            throw new CatalogException(
                StatusCodes.Status400BadRequest,
                "Une cible globale ne peut pas être combinée avec des SchoolId spécifiques.");
        }

        return targets;
    }

    private static void EnsureApiMigrationPair(IReadOnlyList<UpdateReleaseArtifact> artifacts)
    {
        var hasApi = artifacts.Any(a => a.Type == UpdateReleaseArtifactTypes.Api);
        var hasMigration = artifacts.Any(a => a.Type == UpdateReleaseArtifactTypes.Migration);
        if (hasApi != hasMigration)
        {
            throw new CatalogException(
                StatusCodes.Status400BadRequest,
                "Les artifacts Api et Migration doivent être fournis ensemble.");
        }
    }

    private static void ValidateReadyToPublish(UpdateRelease release)
    {
        var hasDesktop = release.Artifacts.Any(a => a.Type == UpdateReleaseArtifactTypes.Desktop);
        var hasApi = release.Artifacts.Any(a => a.Type == UpdateReleaseArtifactTypes.Api);
        var hasMigration = release.Artifacts.Any(a => a.Type == UpdateReleaseArtifactTypes.Migration);
        if (!hasDesktop && !(hasApi && hasMigration))
        {
            throw new CatalogException(
                StatusCodes.Status400BadRequest,
                "Une release publiée doit posséder un artifact Desktop, ou la paire Api+Migration.");
        }

        EnsureApiMigrationPair(release.Artifacts.ToList());

        foreach (var artifact in release.Artifacts)
        {
            if (!ReleaseSemVer.TryNormalizeSha256(artifact.Sha256, out _, out var shaError))
            {
                throw new CatalogException(StatusCodes.Status400BadRequest, shaError!);
            }

            if (!ReleaseArtifactUrlGuard.TryValidate(artifact.Url, release.Channel, out var urlError))
            {
                throw new CatalogException(StatusCodes.Status400BadRequest, urlError!);
            }
        }
    }

    private static bool MatchesTarget(UpdateRelease release, Guid? schoolId)
    {
        var targets = release.Targets;
        var isGlobal = targets.Count == 0 || targets.Any(t => t.SchoolId is null);
        if (schoolId is null)
        {
            return isGlobal;
        }

        return isGlobal || targets.Any(t => t.SchoolId == schoolId);
    }

    private static UpdateReleaseResponse MapRelease(UpdateRelease release) =>
        new()
        {
            ReleaseId = release.ReleaseId,
            Version = release.Version,
            Channel = release.Channel,
            ProtocolVersion = release.ProtocolVersion,
            FromSchemaVersion = release.FromSchemaVersion,
            SchemaVersion = release.SchemaVersion,
            MinimumDesktopVersion = release.MinimumDesktopVersion,
            MinimumApiVersion = release.MinimumApiVersion,
            Mandatory = release.Mandatory,
            Status = release.Status,
            ReleaseNotes = ParseNotes(release.ReleaseNotes),
            CreatedAtUtc = release.CreatedAtUtc,
            PublishedAtUtc = release.PublishedAtUtc,
            BlockedAtUtc = release.BlockedAtUtc,
            BlockedReason = release.BlockedReason,
            CreatedBy = release.CreatedBy,
            Artifacts = release.Artifacts.Select(MapArtifact).ToList(),
            Targets = release.Targets.Select(t => new UpdateReleaseTargetResponse
            {
                TargetId = t.TargetId,
                SchoolId = t.SchoolId,
            }).ToList(),
        };

    private static UpdateReleaseArtifactResponse MapArtifact(UpdateReleaseArtifact artifact) =>
        new()
        {
            ArtifactId = artifact.ArtifactId,
            Type = artifact.Type,
            Version = artifact.Version,
            Url = artifact.Url,
            Size = artifact.Size,
            Sha256 = artifact.Sha256,
            Signature = artifact.Signature,
        };

    private static string SerializeNotes(IEnumerable<string>? notes) =>
        JsonSerializer.Serialize(
            (notes ?? []).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList());

    private static IReadOnlyList<string> ParseNotes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw) ?? [];
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
