namespace SchoolManagement.Bootstrap.API.Contracts;

public sealed class CreateUpdateAgentCredentialRequest
{
    public Guid SchoolId { get; set; }

    public string? CreatedBy { get; set; }
}

public sealed class UpdateAgentCredentialSecretResponse
{
    public required Guid ClientId { get; init; }

    public required Guid SchoolId { get; init; }

    public required int CredentialVersion { get; init; }

    public required string Status { get; init; }

    /// <summary>Secret en clair — uniquement à la création / rotation. Jamais relire ensuite.</summary>
    public required string ClientSecret { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public sealed class UpdateAgentCredentialListItem
{
    public required Guid ClientId { get; init; }

    public required Guid SchoolId { get; init; }

    public required int CredentialVersion { get; init; }

    public required string Status { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; init; }

    public string? RevokedReason { get; init; }
}

public sealed class UpdateAgentRevokeRequest
{
    public string? Reason { get; set; }
}

public sealed class UpdateAgentTokenRequest
{
    public Guid ClientId { get; set; }

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optionnel. S'il est fourni, doit correspondre au SchoolId du credential.
    /// Jamais utilisé pour déterminer l'école.
    /// </summary>
    public Guid? SchoolId { get; set; }
}

public sealed class UpdateAgentTokenResponse
{
    public required string AccessToken { get; init; }

    public required string TokenType { get; init; }

    public required int ExpiresIn { get; init; }

    public required Guid SchoolId { get; init; }

    public required Guid ClientId { get; init; }
}

public sealed class UpdateAgentAuthContext
{
    public const string HttpContextItemKey = "UpdateAgentAuth";

    public required Guid ClientId { get; init; }

    public required Guid SchoolId { get; init; }

    public required Guid JwtId { get; init; }

    public Guid? ServerInstanceId { get; init; }
}
