namespace SchoolManagement.Application.Configuration.Database;

/// <summary>
/// Représente les paramètres de connexion SQL Server lus depuis ServeurDonnees.txt.
/// Le mot de passe est conservé en mémoire uniquement après déchiffrement.
/// </summary>
public sealed class DatabaseConfiguration
{
    public string Serveur { get; set; } = string.Empty;

    public int Port { get; set; } = 1433;

    public string Base { get; set; } = string.Empty;

    public DatabaseAuthenticationMode Authentification { get; set; } = DatabaseAuthenticationMode.SqlServer;

    public string Utilisateur { get; set; } = string.Empty;

    /// <summary>Mot de passe déchiffré — ne jamais journaliser ni persister en clair.</summary>
    public string MotDePasse { get; set; } = string.Empty;

    public DatabaseConfiguration Clone() =>
        new()
        {
            Serveur = Serveur,
            Port = Port,
            Base = Base,
            Authentification = Authentification,
            Utilisateur = Utilisateur,
            MotDePasse = MotDePasse
        };
}
