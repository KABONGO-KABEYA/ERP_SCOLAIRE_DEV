namespace SchoolManagement.Application.Configuration.FileStorage;

/// <summary>Paramètres de stockage des dossiers élèves (ServeurFichiers.txt).</summary>
public sealed class FileStorageConfiguration
{
    /// <summary>Chemin absolu ou UNC vers le dossier partagé Dossier_Elève.</summary>
    public string Racine { get; set; } = string.Empty;

    public FileStorageConfiguration Clone() => new() { Racine = Racine };
}
