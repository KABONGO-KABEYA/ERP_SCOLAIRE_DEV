using System.Text.Json;

namespace SchoolManagement.Updates;

/// <summary>
/// Package local déjà vérifié (SHA côté agent). Ce type ne télécharge rien.
/// </summary>
public sealed class MigrationPackage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public const string ManifestFileName = "manifest.json";

    public string DirectoryPath { get; }

    public MigrationManifest Manifest { get; }

    private MigrationPackage(string directoryPath, MigrationManifest manifest)
    {
        DirectoryPath = directoryPath;
        Manifest = manifest;
    }

    public static MigrationPackage Load(string packageDirectory)
    {
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            throw new MigrationException("Chemin de package de migration requis.");
        }

        var trimmed = packageDirectory.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal)
            || trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("ftp", StringComparison.OrdinalIgnoreCase))
        {
            throw new MigrationException("Le package de migration doit être un dossier local (aucun téléchargement SQL).");
        }

        var full = Path.GetFullPath(trimmed);
        if (!Directory.Exists(full))
        {
            throw new MigrationException($"Package de migration introuvable : {full}");
        }

        var manifestPath = Path.Combine(full, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new MigrationException($"Manifest absent ({ManifestFileName}) dans le package.");
        }

        MigrationManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<MigrationManifest>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new MigrationException("Manifest JSON invalide.", ex);
        }

        if (manifest is null)
        {
            throw new MigrationException("Manifest JSON vide.");
        }

        Validate(full, manifest);
        var package = new MigrationPackage(full, manifest);
        if (manifest.Files is { Count: > 0 })
        {
            package.RequireFileHashes();
        }

        return package;
    }

    public string GetMigrationPath(string fileName) => Path.Combine(DirectoryPath, fileName);

    /// <summary>
    /// Vérifie chaque SQL contre <see cref="MigrationManifest.Files"/>. Refus avant toute exécution.
    /// </summary>
    public void RequireFileHashes()
    {
        var expected = ExpectedFileNames(Manifest);
        var listed = (Manifest.Files ?? [])
            .Select(f => new MigrationFileHash
            {
                Name = (f.Name ?? string.Empty).Trim(),
                Sha256 = f.Sha256 ?? string.Empty,
            })
            .Where(f => f.Name.Length > 0)
            .ToList();

        if (listed.Count != expected.Count
            || listed.Where((f, i) => !string.Equals(f.Name, expected[i], StringComparison.OrdinalIgnoreCase)).Any())
        {
            throw new MigrationException(
                "files[] doit lister exactement la chaîne N→N+1 avec un SHA256 par fichier : "
                + string.Join(", ", expected));
        }

        foreach (var entry in listed)
        {
            var path = GetMigrationPath(entry.Name);
            var actual = ArtifactHash.Sha256File(path);
            var expectedHash = ArtifactHash.Normalize(entry.Sha256);
            if (!ArtifactHash.EqualsHex(expectedHash, actual))
            {
                throw new MigrationException($"SHA256 invalide pour {entry.Name}.");
            }
        }
    }

    internal static IReadOnlyList<string> ExpectedFileNames(MigrationManifest manifest)
    {
        var expected = new List<string>();
        for (var from = manifest.FromSchemaVersion; from < manifest.ToSchemaVersion; from++)
        {
            expected.Add(MigrationManager.FileNameFor(from, from + 1));
        }

        return expected;
    }

    internal static void Validate(string packageDirectory, MigrationManifest manifest)
    {
        if (manifest.FromSchemaVersion < MigrationManager.BaselineSchemaVersion)
        {
            throw new MigrationException(
                $"fromSchemaVersion doit être ≥ baseline {MigrationManager.BaselineSchemaVersion}.");
        }

        if (manifest.ToSchemaVersion < manifest.FromSchemaVersion)
        {
            throw new MigrationException("toSchemaVersion ne peut pas être inférieur à fromSchemaVersion.");
        }

        if (manifest.SchemaVersion != manifest.ToSchemaVersion)
        {
            throw new MigrationException("schemaVersion doit être égal à toSchemaVersion.");
        }

        var expected = ExpectedFileNames(manifest).ToList();

        var listed = (manifest.Migrations ?? []).Select(m => m.Trim()).Where(m => m.Length > 0).ToList();
        if (listed.Count != expected.Count
            || listed.Where((name, i) => !string.Equals(name, expected[i], StringComparison.OrdinalIgnoreCase)).Any())
        {
            throw new MigrationException(
                "Le manifest doit lister exactement la chaîne N→N+1, sans rupture : "
                + string.Join(", ", expected));
        }

        foreach (var name in expected)
        {
            var path = Path.Combine(packageDirectory, name);
            if (!File.Exists(path))
            {
                throw new MigrationException($"Migration manquante dans le package : {name}");
            }
        }

        var unexpected = Directory.GetFiles(packageDirectory, "Migration*.sql")
            .Select(Path.GetFileName)
            .Where(name => name is not null
                           && !expected.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (unexpected.Count > 0)
        {
            throw new MigrationException(
                "Fichiers Migration*.sql hors chaîne du manifest : " + string.Join(", ", unexpected));
        }
    }
}
