namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

/// <summary>
/// Credential Update Agent (hash only). <see cref="Id"/> = ClientId public.
/// Le JWT <c>jti</c> n'est PAS cet identifiant : il est généré à chaque émission.
/// </summary>
public sealed class UpdateAgentCredential
{
    /// <summary>ClientId (claim JWT <c>sub</c>).</summary>
    public Guid Id { get; set; }

    public Guid SchoolId { get; set; }

    public int CredentialVersion { get; set; }

    /// <summary>SHA-256 hex du secret brut (jamais le secret en clair).</summary>
    public string SecretHash { get; set; } = string.Empty;

    public string Status { get; set; } = UpdateAgentCredentialStatuses.Active;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }

    public string? CreatedBy { get; set; }

    public BootstrapSchoolRegistryEntry School { get; set; } = null!;
}
