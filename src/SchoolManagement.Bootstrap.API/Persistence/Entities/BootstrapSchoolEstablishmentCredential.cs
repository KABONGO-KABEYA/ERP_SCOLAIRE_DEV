namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

/// <summary>Credential QR établissement (hash only — statut Active/Revoked fait foi).</summary>
public sealed class BootstrapSchoolEstablishmentCredential
{
    /// <summary>Identifiant credential (= JWT <c>jti</c>).</summary>
    public Guid Id { get; set; }

    public Guid SchoolId { get; set; }

    public int CredentialVersion { get; set; }

    public string TokenType { get; set; } = EstablishmentTokenTypes.SchoolEstablishment;

    /// <summary>SHA-256 hex du secret établissement (jamais le secret en clair).</summary>
    public string SecretHash { get; set; } = string.Empty;

    public string Status { get; set; } = EstablishmentCredentialStatuses.Active;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }

    public string? CreatedBy { get; set; }

    public BootstrapSchoolRegistryEntry School { get; set; } = null!;
}
