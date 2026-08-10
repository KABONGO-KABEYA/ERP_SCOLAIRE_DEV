namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

/// <summary>Session éphémère establish start→complete (ne stocke pas le JWT).</summary>
public sealed class BootstrapEstablishmentSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SchoolId { get; set; }

    public Guid CredentialId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string Status { get; set; } = EstablishmentSessionStatuses.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public BootstrapSchoolRegistryEntry School { get; set; } = null!;

    public BootstrapSchoolEstablishmentCredential Credential { get; set; } = null!;
}
