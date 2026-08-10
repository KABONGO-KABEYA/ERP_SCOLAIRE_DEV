using SchoolManagement.Bootstrap.API.Persistence.Entities;

namespace SchoolManagement.Bootstrap.API.Persistence;

public sealed class BootstrapSchoolRegistryUpsertRequest
{
    public required Guid SchoolId { get; init; }

    public required string SchoolName { get; init; }

    public required string ActivationBaseUrl { get; init; }

    public required string CloudBaseUrl { get; init; }

    public string? PublicKeyFingerprint { get; init; }

    public int? KeyVersion { get; init; }

    public Guid? ServerInstanceId { get; init; }

    public Guid? LicenseId { get; init; }

    /// <summary>Credential à activer (hash only). Si null, met à jour le registre sans toucher aux credentials.</summary>
    public BootstrapCredentialUpsert? Credential { get; init; }
}

public sealed class BootstrapCredentialUpsert
{
    public required Guid CredentialId { get; init; }

    public required int CredentialVersion { get; init; }

    public required string SecretHash { get; init; }

    public string TokenType { get; init; } = EstablishmentTokenTypes.SchoolEstablishment;

    public string? CreatedBy { get; init; }
}

public interface IBootstrapSchoolRegistryRepository
{
    Task<BootstrapSchoolRegistryEntry?> GetBySchoolIdAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<BootstrapSchoolEstablishmentCredential?> GetActiveCredentialAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<BootstrapSchoolEstablishmentCredential?> GetCredentialByIdAsync(
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>Insère ou met à jour l'école ; active le credential fourni (révoque l'ancien Active si besoin).</summary>
    Task<BootstrapSchoolRegistryEntry> UpsertSchoolAsync(
        BootstrapSchoolRegistryUpsertRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Révoque le credential Active et active le nouveau.</summary>
    Task<(BootstrapSchoolEstablishmentCredential Revoked, BootstrapSchoolEstablishmentCredential Active)> RotateCredentialAsync(
        Guid schoolId,
        BootstrapCredentialUpsert newCredential,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<BootstrapEstablishmentSession> CreateSessionAsync(
        Guid schoolId,
        Guid credentialId,
        string deviceId,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<BootstrapEstablishmentSession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task MarkSessionCompletedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
