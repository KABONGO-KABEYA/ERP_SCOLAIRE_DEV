using System.ComponentModel.DataAnnotations;
using SchoolManagement.Bootstrap.API.Persistence.Entities;

namespace SchoolManagement.Bootstrap.API.Contracts;

public sealed class RegistrySchoolUpsertHttpRequest
{
    [Required]
    public Guid SchoolId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SchoolName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string ActivationBaseUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string CloudBaseUrl { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? PublicKeyFingerprint { get; set; }

    public int? KeyVersion { get; set; }

    public Guid? ServerInstanceId { get; set; }

    public Guid? LicenseId { get; set; }

    public RegistryCredentialHttpBody? Credential { get; set; }
}

public sealed class RegistryCredentialHttpBody
{
    [Required]
    public Guid CredentialId { get; set; }

    [Range(1, int.MaxValue)]
    public int CredentialVersion { get; set; }

    [Required]
    [MaxLength(128)]
    public string SecretHash { get; set; } = string.Empty;

    [MaxLength(64)]
    public string TokenType { get; set; } = EstablishmentTokenTypes.SchoolEstablishment;

    [MaxLength(128)]
    public string? CreatedBy { get; set; }
}

public sealed class RegistryCredentialRotateHttpRequest
{
    [Required]
    public RegistryCredentialHttpBody Credential { get; set; } = null!;

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public sealed class RegistrySchoolUpsertHttpResponse
{
    public required Guid SchoolId { get; init; }

    public required string SchoolName { get; init; }

    public required bool IsActive { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }

    public Guid? ActiveCredentialId { get; init; }

    public int? ActiveCredentialVersion { get; init; }
}

public sealed class RegistryCredentialRotateHttpResponse
{
    public required Guid SchoolId { get; init; }

    public required Guid RevokedCredentialId { get; init; }

    public required string RevokedReason { get; init; }

    public required Guid ActiveCredentialId { get; init; }

    public required int ActiveCredentialVersion { get; init; }
}
