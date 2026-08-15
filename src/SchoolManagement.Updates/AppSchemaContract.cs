namespace SchoolManagement.Updates;

/// <summary>
/// Schéma SQL exigé par l'API compilée. Doit égaler <see cref="MigrationManifest.ToSchemaVersion"/>
/// d'une release qui embarque cette API. Baseline = <see cref="MigrationManager.BaselineSchemaVersion"/>.
/// </summary>
public static class AppSchemaContract
{
    /// <summary>Doit rester égal à <see cref="MigrationManager.BaselineSchemaVersion"/>.</summary>
    public const int RequiredSchemaVersion = 1;

    public const string ApiManifestFileName = "api-manifest.json";

    public const string RuntimeWinX64 = "win-x64";

    public static readonly string[] ApiPublishSecretFileNames =
    [
        "ServeurDonnees.txt",
        "ServeurDonneesCloud.txt",
        "ServeurFichiers.txt",
    ];

    public static bool IsExcludedFromApiZip(string fileName) =>
        ApiPublishSecretFileNames.Any(n => string.Equals(n, fileName, StringComparison.OrdinalIgnoreCase));
}
