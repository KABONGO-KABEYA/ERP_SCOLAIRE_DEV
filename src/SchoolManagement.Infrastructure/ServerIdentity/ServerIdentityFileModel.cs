namespace SchoolManagement.Infrastructure.ServerIdentity;

internal sealed class ServerIdentityFileModel
{
    public Guid ServerInstanceId { get; set; }

    public int KeyVersion { get; set; } = 1;

    public string PublicKeyBase64 { get; set; } = string.Empty;

    public string PublicKeyFingerprint { get; set; } = string.Empty;

    /// <summary>Clé privée chiffrée (IEncryptionService, préfixe ENC:).</summary>
    public string PrivateKeyProtected { get; set; } = string.Empty;

    public DateTime InstalledAtUtc { get; set; }
}
