namespace SchoolManagement.Updates;

/// <summary>Hash SHA256 d'un fichier SQL du package (en plus du SHA du zip).</summary>
public sealed class MigrationFileHash
{
    public string Name { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;
}
