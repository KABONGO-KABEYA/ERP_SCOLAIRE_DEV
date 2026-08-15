namespace SchoolManagement.Application.ServerIdentity;

/// <summary>Identité publique d'une installation serveur (snapshot au démarrage).</summary>
public sealed record ServerIdentitySnapshot(
    Guid ServerInstanceId,
    Guid? SchoolId,
    string SchoolName,
    Guid? LicenseId,
    string PublicKeyFingerprint,
    int KeyVersion,
    string SoftwareVersion,
    string ApiVersion,
    int ProtocolVersion,
    string ServerRole,
    int SchemaVersion);
