namespace SchoolManagement.Bootstrap.API.Options;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string RelayApiKey { get; set; } = string.Empty;

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
