using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Notifications;

/// <summary>Notification métier créée par un module de l'ERP.</summary>
public class SchoolNotification : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid? StudentId { get; set; }

    public NotificationCategory Category { get; set; }

    public NotificationEventType EventType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Payload JSON optionnel (ids, deep-link data).</summary>
    public string? DataJson { get; set; }

    public string? DeepLink { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public ICollection<NotificationRecipient> Recipients { get; set; } = [];
}

/// <summary>Destinataire (compte parent) d'une notification.</summary>
public class NotificationRecipient : AuditableEntity
{
    public Guid NotificationId { get; set; }

    public Guid UserAccountId { get; set; }

    public Guid? GuardianId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? PushSentAt { get; set; }

    public SchoolNotification Notification { get; set; } = null!;
}

/// <summary>Token FCM / appareil mobile d'un parent.</summary>
public class ParentDeviceToken : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public Guid UserAccountId { get; set; }

    public string Token { get; set; } = string.Empty;

    /// <summary>android | ios | web</summary>
    public string Platform { get; set; } = "android";

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
