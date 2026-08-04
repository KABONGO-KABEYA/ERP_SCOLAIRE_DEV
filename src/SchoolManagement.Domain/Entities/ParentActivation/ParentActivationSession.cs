using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.ParentActivation;

public enum ParentActivationSessionStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Revoked = 3
}

/// <summary>Session côté école pendant start/complete (corrélation bootstrap).</summary>
public sealed class ParentActivationSession : AuditableEntity
{
    public Guid SchoolId { get; set; }

    public Guid ActivationTokenId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public Guid? BootstrapSessionId { get; set; }

    public ParentActivationSessionStatus Status { get; set; } = ParentActivationSessionStatus.Pending;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}
