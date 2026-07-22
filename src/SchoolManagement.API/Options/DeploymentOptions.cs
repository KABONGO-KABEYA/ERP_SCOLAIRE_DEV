namespace SchoolManagement.API.Options;

/// Déploiement de l'instance API : Local (école) ou Cloud (copie lecture seule).
public sealed class DeploymentOptions
{
    public const string SectionName = "Deployment";

    /// <summary>Local | Cloud</summary>
    public string Role { get; set; } = "Local";

    /// <summary>
    /// Si true (ou Role=Cloud), bloque POST/PUT/PATCH/DELETE hors exceptions.
    /// </summary>
    public bool ReadOnly { get; set; }

    public bool IsCloudReadOnly =>
        ReadOnly
        || string.Equals(Role, "Cloud", StringComparison.OrdinalIgnoreCase);
}
