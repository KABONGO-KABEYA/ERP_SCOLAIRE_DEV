using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Notifications.DTOs;

public sealed record SendNotificationRequest(
    Guid SchoolId,
    Guid? StudentId,
    NotificationCategory Category,
    NotificationEventType EventType,
    string Title,
    string Body,
    string? DataJson = null,
    string? DeepLink = null,
    DateTime? OccurredAt = null);

public sealed record ParentNotificationDto(
    Guid Id,
    Guid RecipientId,
    string Title,
    string Message,
    DateTime Date,
    bool IsRead,
    string Category,
    string EventType,
    Guid? StudentId = null,
    string? DeepLink = null,
    /// <summary>Sent | Delivered | Read — pipeline ACK.</summary>
    string DeliveryStatus = "Sent");

public sealed record ParentNotificationUnreadCountDto(int Count);

public sealed record RegisterDeviceTokenRequest(
    string Token,
    string Platform);

public sealed record AcknowledgeDeliveryRequest(Guid NotificationId);
