namespace SchoolManagement.Application.Configuration.Database;

/// <summary>
/// Paramètres de la base SQL distante (cloud) pour la synchronisation automatique.
/// </summary>
public sealed class CloudDatabaseConfiguration
{
    public string Serveur { get; set; } = string.Empty;

    public int Port { get; set; } = 1433;

    public string Base { get; set; } = string.Empty;

    public DatabaseAuthenticationMode Authentification { get; set; } = DatabaseAuthenticationMode.SqlServer;

    public string Utilisateur { get; set; } = string.Empty;

    /// <summary>Mot de passe déchiffré — ne jamais journaliser ni persister en clair.</summary>
    public string MotDePasse { get; set; } = string.Empty;

    /// <summary>Active la synchronisation locale → cloud quand Internet est disponible.</summary>
    public bool Actif { get; set; }

    /// <summary>Intervalle minimal entre deux tentatives de sync (minutes).</summary>
    public int IntervalleMinutes { get; set; } = 5;

    public DatabaseConfiguration ToDatabaseConfiguration() =>
        new()
        {
            Serveur = Serveur,
            Port = Port,
            Base = Base,
            Authentification = Authentification,
            Utilisateur = Utilisateur,
            MotDePasse = MotDePasse
        };

    public CloudDatabaseConfiguration Clone() =>
        new()
        {
            Serveur = Serveur,
            Port = Port,
            Base = Base,
            Authentification = Authentification,
            Utilisateur = Utilisateur,
            MotDePasse = MotDePasse,
            Actif = Actif,
            IntervalleMinutes = IntervalleMinutes
        };
}
