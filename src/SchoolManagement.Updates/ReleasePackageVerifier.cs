namespace SchoolManagement.Updates;

/// <summary>
/// Cohérence locale API + Migration (avant POST catalogue / avant exécution SQL).
/// Ne télécharge rien.
/// </summary>
public static class ReleasePackageVerifier
{
    public static void VerifyPair(
        string apiDirectory,
        string migrationDirectory,
        string releaseVersion,
        int fromSchemaVersion,
        int toSchemaVersion,
        int protocolVersion)
    {
        var api = ApiArtifactManifest.Load(apiDirectory);
        var migration = MigrationPackage.Load(migrationDirectory);
        VerifyLoaded(api, migration.Manifest, releaseVersion, fromSchemaVersion, toSchemaVersion, protocolVersion);
        migration.RequireFileHashes();
    }

    internal static void VerifyLoaded(
        ApiArtifactManifest api,
        MigrationManifest migration,
        string releaseVersion,
        int fromSchemaVersion,
        int toSchemaVersion,
        int protocolVersion)
    {
        var expectedVersion = VersionManager.Parse(releaseVersion);
        if (expectedVersion.IsZero)
        {
            throw new MigrationException("Version de release SemVer invalide.");
        }

        var expected = expectedVersion.ToNormalizedString();
        var apiVersion = VersionManager.Parse(api.ReleaseVersion).ToNormalizedString();
        if (!string.Equals(apiVersion, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException(
                $"api-manifest.releaseVersion {api.ReleaseVersion} ≠ release {expected}.");
        }

        if (string.IsNullOrWhiteSpace(migration.ReleaseVersion)
            || !string.Equals(
                VersionManager.Parse(migration.ReleaseVersion).ToNormalizedString(),
                expected,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException(
                $"manifest.releaseVersion {migration.ReleaseVersion} ≠ release {expected}.");
        }

        if (migration.FromSchemaVersion != fromSchemaVersion)
        {
            throw new MigrationException(
                $"fromSchemaVersion catalogue {fromSchemaVersion} ≠ manifest {migration.FromSchemaVersion}.");
        }

        if (migration.ToSchemaVersion != toSchemaVersion
            || migration.SchemaVersion != toSchemaVersion)
        {
            throw new MigrationException(
                $"toSchemaVersion catalogue {toSchemaVersion} ≠ manifest {migration.ToSchemaVersion}.");
        }

        if (api.RequiredSchemaVersion != toSchemaVersion)
        {
            throw new MigrationException(
                $"requiredSchemaVersion {api.RequiredSchemaVersion} ≠ toSchema {toSchemaVersion}.");
        }

        if (api.ProtocolVersion != protocolVersion)
        {
            throw new MigrationException(
                $"protocolVersion API {api.ProtocolVersion} ≠ catalogue {protocolVersion}.");
        }

        if (toSchemaVersion < AppSchemaContract.RequiredSchemaVersion)
        {
            throw new MigrationException(
                $"toSchema {toSchemaVersion} inférieur à RequiredSchemaVersion {AppSchemaContract.RequiredSchemaVersion}.");
        }
    }
}
