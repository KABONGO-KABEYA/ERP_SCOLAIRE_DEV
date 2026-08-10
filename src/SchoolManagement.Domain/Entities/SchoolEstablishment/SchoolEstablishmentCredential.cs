using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.SchoolEstablishment;

/// <summary>Credential QR établissement (hash only — miroir local du registre Bootstrap).</summary>
public sealed class SchoolEstablishmentCredential : AuditableEntity
{
    public Guid SchoolId { get; set; }

    public int CredentialVersion { get; set; }

    public string TokenType { get; set; } = "school_establishment";

    /// <summary>SHA-256 hex du secret brut (jamais le secret en clair).</summary>
    public string SecretHash { get; set; } = string.Empty;

    public string Status { get; set; } = SchoolEstablishmentCredentialStatuses.Active;

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }

    public Guid? CreatedByUserId { get; set; }

    /// <summary>True tant que le registre Bootstrap n'a pas confirmé l'upsert/rotate.</summary>
    public bool BootstrapSyncPending { get; set; } = true;

    public string BootstrapSyncStatus { get; set; } = SchoolEstablishmentBootstrapSyncStatuses.Pending;

    public string? LastBootstrapSyncError { get; set; }

    public DateTime? LastBootstrapSyncAttemptUtc { get; set; }

    public DateTime? BootstrapSyncedAtUtc { get; set; }
}

public static class SchoolEstablishmentCredentialStatuses
{
    public const string Active = "Active";
    public const string Revoked = "Revoked";
}

public static class SchoolEstablishmentBootstrapSyncStatuses
{
    public const string Pending = "Pending";
    public const string Synced = "Synced";
    public const string Failed = "Failed";
}
