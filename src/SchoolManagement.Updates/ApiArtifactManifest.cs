using System.Text.Json;

namespace SchoolManagement.Updates;

/// <summary>Manifest interne du zip API (<c>api-manifest.json</c>).</summary>
public sealed class ApiArtifactManifest
{
    public string ArtifactType { get; set; } = UpdateArtifactTypes.Api;

    public string ReleaseVersion { get; set; } = string.Empty;

    public int RequiredSchemaVersion { get; set; } = AppSchemaContract.RequiredSchemaVersion;

    public int ProtocolVersion { get; set; }

    public string Runtime { get; set; } = AppSchemaContract.RuntimeWinX64;

    public static ApiArtifactManifest Load(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || directory.Contains("://", StringComparison.Ordinal))
        {
            throw new MigrationException("Le manifeste API doit être un dossier local.");
        }

        var full = Path.GetFullPath(directory.Trim());
        var path = Path.Combine(full, AppSchemaContract.ApiManifestFileName);
        if (!File.Exists(path))
        {
            throw new MigrationException($"Manifest API absent ({AppSchemaContract.ApiManifestFileName}).");
        }

        ApiArtifactManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ApiArtifactManifest>(
                File.ReadAllText(path),
                ReleasePackageJson.Options);
        }
        catch (JsonException ex)
        {
            throw new MigrationException("api-manifest.json invalide.", ex);
        }

        if (manifest is null)
        {
            throw new MigrationException("api-manifest.json vide.");
        }

        Validate(manifest);
        return manifest;
    }

    internal static void Validate(ApiArtifactManifest manifest)
    {
        if (!string.Equals(manifest.ArtifactType, UpdateArtifactTypes.Api, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException("api-manifest.artifactType doit être Api.");
        }

        if (string.IsNullOrWhiteSpace(manifest.ReleaseVersion)
            || VersionManager.Parse(manifest.ReleaseVersion).IsZero)
        {
            throw new MigrationException("api-manifest.releaseVersion SemVer invalide.");
        }

        if (manifest.RequiredSchemaVersion < MigrationManager.BaselineSchemaVersion)
        {
            throw new MigrationException(
                $"api-manifest.requiredSchemaVersion doit être ≥ {MigrationManager.BaselineSchemaVersion}.");
        }

        if (manifest.ProtocolVersion < 1)
        {
            throw new MigrationException("api-manifest.protocolVersion doit être ≥ 1.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Runtime))
        {
            throw new MigrationException("api-manifest.runtime requis.");
        }
    }
}

public static class UpdateArtifactTypes
{
    public const string Api = "Api";
    public const string Migration = "Migration";
}

internal static class ReleasePackageJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };
}
