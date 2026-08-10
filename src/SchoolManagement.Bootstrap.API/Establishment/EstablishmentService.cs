using Microsoft.Extensions.Options;
using SchoolManagement.Application.ParentActivation;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;

namespace SchoolManagement.Bootstrap.API.Establishment;

public sealed class EstablishmentStartRequest
{
    public string Token { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public Dictionary<string, object?>? ClientHints { get; set; }
}

public sealed class EstablishmentCompleteRequest
{
    public Guid EstablishmentSessionId { get; set; }

    public string DeviceId { get; set; } = string.Empty;
}

public sealed class EstablishmentSessionResponse
{
    public required Guid EstablishmentSessionId { get; init; }

    public required Guid SchoolId { get; init; }

    public required string DeviceId { get; init; }

    public required string Status { get; init; }

    public required DateTime ExpiresAt { get; init; }
}

public sealed class EstablishmentService
{
    private readonly IBootstrapSchoolRegistryRepository _registry;
    private readonly BootstrapOptions _options;

    public EstablishmentService(
        IBootstrapSchoolRegistryRepository registry,
        IOptions<BootstrapOptions> options)
    {
        _registry = registry;
        _options = options.Value;
    }

    public async Task<EstablishmentSessionResponse> StartAsync(
        EstablishmentStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new EstablishmentException(
                StatusCodes.Status400BadRequest,
                "DeviceId requis.");
        }

        var parsed = EstablishmentJwtValidator.ReadClaims(request.Token);

        var school = await _registry.GetBySchoolIdAsync(parsed.SchoolId, cancellationToken);
        if (school is null || !school.IsActive)
        {
            throw new EstablishmentException(
                StatusCodes.Status404NotFound,
                "École introuvable dans le registre Bootstrap.");
        }

        var credential = await _registry.GetCredentialByIdAsync(parsed.CredentialId, cancellationToken);
        if (credential is null || credential.SchoolId != parsed.SchoolId)
        {
            throw new EstablishmentException(
                StatusCodes.Status401Unauthorized,
                "Token établissement invalide.");
        }

        if (string.Equals(credential.Status, EstablishmentCredentialStatuses.Revoked, StringComparison.OrdinalIgnoreCase))
        {
            throw new EstablishmentException(
                StatusCodes.Status403Forbidden,
                "QR établissement révoqué. Demandez un nouveau QR à l'école.");
        }

        if (!string.Equals(credential.Status, EstablishmentCredentialStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new EstablishmentException(
                StatusCodes.Status403Forbidden,
                "QR établissement révoqué. Demandez un nouveau QR à l'école.");
        }

        if (credential.CredentialVersion != parsed.CredentialVersion)
        {
            throw new EstablishmentException(
                StatusCodes.Status400BadRequest,
                "Version de credential invalide.");
        }

        EstablishmentJwtValidator.ValidateSignature(request.Token, credential.SecretHash, parsed.SchoolId);

        var ttlMinutes = _options.EstablishmentSessionMinutes <= 0 ? 15 : _options.EstablishmentSessionMinutes;
        var expiresAt = DateTime.UtcNow.AddMinutes(ttlMinutes);
        var session = await _registry.CreateSessionAsync(
            parsed.SchoolId,
            parsed.CredentialId,
            request.DeviceId.Trim(),
            expiresAt,
            cancellationToken);

        return new EstablishmentSessionResponse
        {
            EstablishmentSessionId = session.Id,
            SchoolId = session.SchoolId,
            DeviceId = session.DeviceId,
            Status = "pending",
            ExpiresAt = session.ExpiresAtUtc,
        };
    }

    public async Task<SchoolBindingDto> CompleteAsync(
        EstablishmentCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new EstablishmentException(
                StatusCodes.Status400BadRequest,
                "DeviceId requis.");
        }

        var session = await _registry.GetSessionAsync(request.EstablishmentSessionId, cancellationToken);
        if (session is null)
        {
            throw new EstablishmentException(
                StatusCodes.Status404NotFound,
                "Session d'établissement introuvable.");
        }

        if (!string.Equals(session.DeviceId, request.DeviceId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new EstablishmentException(
                StatusCodes.Status400BadRequest,
                "DeviceId incompatible.");
        }

        if (string.Equals(session.Status, EstablishmentSessionStatuses.Expired, StringComparison.OrdinalIgnoreCase)
            || session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new EstablishmentException(
                StatusCodes.Status400BadRequest,
                "Session d'établissement expirée.");
        }

        if (!string.Equals(session.Status, EstablishmentSessionStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new EstablishmentException(
                StatusCodes.Status400BadRequest,
                "Session d'établissement non valide.");
        }

        var school = await _registry.GetBySchoolIdAsync(session.SchoolId, cancellationToken);
        if (school is null || !school.IsActive)
        {
            throw new EstablishmentException(
                StatusCodes.Status404NotFound,
                "École introuvable dans le registre Bootstrap.");
        }

        var credential = await _registry.GetCredentialByIdAsync(session.CredentialId, cancellationToken);
        if (credential is null
            || !string.Equals(credential.Status, EstablishmentCredentialStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new EstablishmentException(
                StatusCodes.Status403Forbidden,
                "QR établissement révoqué. Demandez un nouveau QR à l'école.");
        }

        await _registry.MarkSessionCompletedAsync(session.Id, cancellationToken);

        return new SchoolBindingDto(
            SchoolId: school.SchoolId,
            SchoolName: school.SchoolName,
            CloudBaseUrl: school.CloudBaseUrl.TrimEnd('/'),
            ServerInstanceId: school.ServerInstanceId ?? Guid.Empty,
            LicenseId: school.LicenseId,
            ActivationDate: DateTime.UtcNow,
            ActivationTokenId: session.CredentialId,
            ActivationSessionId: session.Id,
            DeviceId: session.DeviceId,
            ProtocolVersion: 2,
            SuggestedUserName: null,
            ExpiresAt: null,
            Extensions: new Dictionary<string, object?>
            {
                [EstablishmentTokenConstants.BindingKindExtensionKey] =
                    EstablishmentTokenConstants.BindingKindExtensionValue,
                [EstablishmentTokenConstants.CredentialVersionExtensionKey] =
                    credential.CredentialVersion,
            });
    }
}
