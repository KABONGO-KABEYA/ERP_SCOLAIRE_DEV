namespace SchoolManagement.Bootstrap.API.Options;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string RelayApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Clé dédiée à la publication du catalogue de releases (<c>X-Bootstrap-Release-Key</c>).
    /// Distincte de <see cref="RelayApiKey"/>. Ne jamais journaliser sa valeur.
    /// </summary>
    public string ReleasePublishApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Clé dédiée au provisionnement des credentials Update Agent
    /// (<c>X-Bootstrap-Agent-Provision-Key</c>). Distincte de <see cref="RelayApiKey"/>
    /// et <see cref="ReleasePublishApiKey"/>. Ne jamais journaliser sa valeur.
    /// Absente → 503. Pas de valeur par défaut en Production.
    /// </summary>
    public string AgentProvisionApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Clé HMAC-SHA256 dédiée à la signature des JWT <c>update_agent</c>.
    /// Distincte de <see cref="AgentProvisionApiKey"/>, des clés HTTP et des secrets établissement.
    /// Minimum 32 octets UTF-8. Absente ou trop courte → 503.
    /// Jamais générée au démarrage. Production : Coolify / secret manager uniquement.
    /// Ne jamais journaliser sa valeur.
    /// </summary>
    public string AgentJwtSigningKey { get; set; } = string.Empty;

    /// <summary>TTL des JWT agent (minutes). Borné 5–60, défaut 30.</summary>
    public int AgentJwtMinutes { get; set; } = 30;

    /// <summary>
    /// Chaîne SQL du registre Bootstrap (DB dédiée <c>SchoolManagementBootstrap</c>).
    /// Env Coolify : <c>Bootstrap__ConnectionString</c> ou <c>BOOTSTRAP_CONNECTION_STRING</c>.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Phase 8 : défaut <c>false</c> — le registre SQL fait foi.
    /// Si <c>true</c>, fallback lecture <c>Bootstrap:Schools</c> uniquement si l'école est absente du SQL.
    /// Ne pas réactiver pour contourner un échec cutover : identifier la dépendance.
    /// </summary>
    public bool AllowLegacyEnvSchoolRegistry { get; set; }

    /// <summary>TTL session establish start→complete (minutes). Spec : 10–15.</summary>
    public int EstablishmentSessionMinutes { get; set; } = 15;

    /// <summary>
    /// Legacy Coolify env — migrés au démarrage vers SQL puis à retirer (<c>Bootstrap__Schools__*</c>).
    /// </summary>
    public List<SchoolRegistryEntryOptions> Schools { get; set; } = [];
}

public sealed class SchoolRegistryEntryOptions
{
    public Guid SchoolId { get; set; }

    public string ActivationBaseUrl { get; set; } = string.Empty;

    public string CloudBaseUrl { get; set; } = string.Empty;

    public string? PublicKeyFingerprint { get; set; }

    public int? KeyVersion { get; set; }

    public string? PublicKeyPem { get; set; }

    public string? ServerInstanceId { get; set; }
}
