namespace SchoolManagement.Updates;

/// <summary>Manifest d'un package de migration local (jamais téléchargé par ce moteur).</summary>
public sealed class MigrationManifest
{
    public int SchemaVersion { get; set; }

    public int FromSchemaVersion { get; set; }

    public int ToSchemaVersion { get; set; }

    /// <summary>Version SemVer de la release Bootstrap (Lot 2B-2, optionnel en 2B-1).</summary>
    public string? ReleaseVersion { get; set; }

    public List<string> Migrations { get; set; } = [];

    /// <summary>SHA256 par fichier SQL. Requis pour une release publiée ; optionnel pour les tests 2B-1.</summary>
    public List<MigrationFileHash> Files { get; set; } = [];
}

public sealed record MigrationApplyResult(
    int PreviousVersion,
    int CurrentVersion,
    IReadOnlyList<string> AppliedMigrations);
