namespace SchoolManagement.Application.ParentActivation;

public sealed record IssueParentActivationTokenRequest(
    string? SuggestedUserName,
    int? ValidityMinutes);

public sealed record IssueParentActivationTokenResponse(
    string Token,
    Guid ActivationTokenId,
    DateTime ExpiresAtUtc,
    string DeepLinkUri,
    string QrPayload);

public sealed record ActivationStartRequest(
    string Token,
    string DeviceId,
    Guid? BootstrapSessionId,
    Dictionary<string, object?>? ClientHints);

public sealed record ActivationSessionDto(
    Guid ActivationSessionId,
    Guid ActivationTokenId,
    string DeviceId,
    Guid SchoolId,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    Dictionary<string, object?>? ClientHints);

public sealed record ActivationCompleteRequest(
    Guid ActivationSessionId,
    string DeviceId);

public sealed record SchoolBindingDto(
    Guid SchoolId,
    string SchoolName,
    string CloudBaseUrl,
    Guid ServerInstanceId,
    Guid? LicenseId,
    DateTime ActivationDate,
    Guid ActivationTokenId,
    Guid ActivationSessionId,
    string DeviceId,
    int ProtocolVersion,
    string? SuggestedUserName,
    DateTime? ExpiresAt,
    Dictionary<string, object?>? Extensions);
