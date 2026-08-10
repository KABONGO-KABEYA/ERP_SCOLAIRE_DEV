namespace SchoolManagement.Application.SchoolEstablishment;

public static class SchoolEstablishmentTokenConstants
{
    public const string TokenTypeClaim = "token_type";
    public const string TokenTypeValue = "school_establishment";
    public const string SchoolIdClaim = "school_id";
    public const string VersionClaim = "ver";
    public const string Audience = "erp-scolaire-mobile-establish";
    public const string BootstrapIssuer = "https://bootstrap.erp-scolaire.com";
    public const string DeepLinkScheme = "erp-scolaire";
    public const string DeepLinkPath = "establish";

    public static string SchoolIssuer(Guid schoolId) => $"school:{schoolId:D}";
}

/// <summary>Options école → publication registre Bootstrap (section <c>Bootstrap</c>).</summary>
public sealed class SchoolBootstrapRegistryOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>URL de base Bootstrap (ex. https://gopvetrs….sslip.io). Sans slash final.</summary>
    public string? RegistryBaseUrl { get; set; }

    /// <summary>Clé <c>X-Bootstrap-Relay-Key</c>. Si vide, repli sur <c>Activation:BootstrapRelayKey</c>.</summary>
    public string? RelayApiKey { get; set; }

    /// <summary>URL école joignable depuis Bootstrap (health / ops). Obligatoire pour upsert.</summary>
    public string? ActivationBaseUrl { get; set; }

    /// <summary>URL cloud parent. Si vide, repli sur <c>Activation:CloudBaseUrl</c>.</summary>
    public string? CloudBaseUrl { get; set; }
}

public sealed record SchoolEstablishmentQrDto(
    Guid SchoolId,
    Guid CredentialId,
    int CredentialVersion,
    string Token,
    string DeepLinkUri,
    string QrPayload,
    bool BootstrapSyncPending,
    string BootstrapSyncStatus,
    string? BootstrapSyncMessage);

public static class SchoolEstablishmentBootstrapSyncUi
{
    public const string Pending = "Pending";
    public const string Synced = "Synced";
    public const string Failed = "Failed";
}

public sealed record BootstrapSyncRetryResult(
    bool Success,
    bool BootstrapSyncPending,
    string BootstrapSyncStatus,
    string? Message,
    SchoolEstablishmentQrDto? Qr);

public interface IBootstrapSchoolRegistryClient
{
    Task UpsertSchoolAsync(
        BootstrapRegistryUpsertPayload payload,
        CancellationToken cancellationToken = default);

    Task RotateCredentialAsync(
        Guid schoolId,
        BootstrapRegistryCredentialPayload credential,
        string? reason,
        CancellationToken cancellationToken = default);
}

public sealed record BootstrapRegistryUpsertPayload(
    Guid SchoolId,
    string SchoolName,
    string ActivationBaseUrl,
    string CloudBaseUrl,
    string? PublicKeyFingerprint,
    int? KeyVersion,
    Guid? ServerInstanceId,
    Guid? LicenseId,
    BootstrapRegistryCredentialPayload Credential);

public sealed record BootstrapRegistryCredentialPayload(
    Guid CredentialId,
    int CredentialVersion,
    string SecretHash,
    string TokenType);

public interface ISchoolEstablishmentService
{
    /// <summary>
    /// Après création école : génère credential local, tente upsert Bootstrap.
    /// Échec Bootstrap → pas de rollback école ; <see cref="SchoolEstablishmentQrDto.BootstrapSyncPending"/> = true.
    /// </summary>
    Task<SchoolEstablishmentQrDto> ProvisionForNewSchoolAsync(
        Guid schoolId,
        string schoolName,
        CancellationToken cancellationToken = default);

    Task<SchoolEstablishmentQrDto> GetCurrentQrAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<SchoolEstablishmentQrDto> RotateAsync(
        Guid schoolId,
        Guid? rotatedByUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<BootstrapSyncRetryResult> RetryBootstrapSyncAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);
}
