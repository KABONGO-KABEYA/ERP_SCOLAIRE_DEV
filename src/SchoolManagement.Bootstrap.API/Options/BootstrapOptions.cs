namespace SchoolManagement.Bootstrap.API.Options;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string RelayApiKey { get; set; } = string.Empty;

    public List<SchoolRegistryEntryOptions> Schools { get; set; } = [];
}

public sealed class SchoolRegistryEntryOptions
{
    public Guid SchoolId { get; set; }

    public string ActivationBaseUrl { get; set; } = string.Empty;

    public string CloudBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Empreinte SHA-256 (hex) de la clé publique RSA école — alignée sur health <c>identity.publicKeyFingerprint</c>.
    /// Non utilisée en runtime v2.0.1 ; préparation registre / pinning (étape 8, cf. doc registre Bootstrap).
    /// </summary>
    public string? PublicKeyFingerprint { get; set; }

    /// <summary>Version de clé école (<c>identity.keyVersion</c>).</summary>
    public int? KeyVersion { get; set; }

    /// <summary>PEM clé publique école (optionnel) — validation relay JWT / signature health (futur).</summary>
    public string? PublicKeyPem { get; set; }

    /// <summary>Dernière instance serveur connue (audit ops, optionnel).</summary>
    public string? ServerInstanceId { get; set; }
}
