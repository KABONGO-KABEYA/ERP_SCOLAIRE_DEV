namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

/// <summary>Entrée registre Bootstrap (routage école + métadonnées binding).</summary>
public sealed class BootstrapSchoolRegistryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SchoolId { get; set; }

    public string SchoolName { get; set; } = string.Empty;

    public string ActivationBaseUrl { get; set; } = string.Empty;

    public string CloudBaseUrl { get; set; } = string.Empty;

    public string? PublicKeyFingerprint { get; set; }

    public int? KeyVersion { get; set; }

    public Guid? ServerInstanceId { get; set; }

    public Guid? LicenseId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public byte[] RowVersion { get; set; } = [];

    public ICollection<BootstrapSchoolEstablishmentCredential> Credentials { get; set; } = [];

    public ICollection<BootstrapEstablishmentSession> Sessions { get; set; } = [];
}
