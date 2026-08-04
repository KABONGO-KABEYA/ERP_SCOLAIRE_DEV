using SchoolManagement.Application.Notifications.DTOs;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Notifications.Interfaces;

/// <summary>
/// Point d'entrée unique pour tous les modules ERP.
/// Pipeline de livraison (évolutif) :
/// NotificationService → SignalR (app ouverte)
///                    → Foreground Service / changes API (app minimisée)
///                    → FCM via IPushNotificationSender (app tuée, à brancher plus tard)
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Crée la notification, résout les parents liés à l'élève, persiste les destinataires,
    /// pousse en temps réel (SignalR groupe privé) et tente l'envoi Push (FCM stub/réel).
    /// </summary>
    Task<Guid> NotifyStudentParentsAsync(
        Guid schoolId,
        Guid studentId,
        NotificationCategory category,
        NotificationEventType eventType,
        string title,
        string body,
        string? dataJson = null,
        string? deepLink = null,
        CancellationToken cancellationToken = default);

    /// <summary>Envoi générique (déjà résolu ou sans élève).</summary>
    Task<Guid> SendAsync(
        SendNotificationRequest request,
        IReadOnlyList<Guid>? explicitUserAccountIds = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParentNotificationDto>> GetInboxAsync(
        Guid schoolId,
        Guid userAccountId,
        NotificationCategory? category = null,
        string? search = null,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delta inbox : uniquement les nouvelles notifs après <paramref name="afterId"/>
    /// et/ou <paramref name="since"/> (UTC). Trafic réduit pour le Foreground Service.
    /// </summary>
    Task<IReadOnlyList<ParentNotificationDto>> GetChangesAsync(
        Guid schoolId,
        Guid userAccountId,
        Guid? afterId = null,
        DateTime? since = null,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid schoolId,
        Guid userAccountId,
        CancellationToken cancellationToken = default);

    Task MarkReadAsync(
        Guid schoolId,
        Guid userAccountId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(
        Guid schoolId,
        Guid userAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>ACK livrée (SignalR / FG / futur FCM).</summary>
    Task MarkDeliveredAsync(
        Guid schoolId,
        Guid userAccountId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    Task RegisterDeviceTokenAsync(
        Guid schoolId,
        Guid userAccountId,
        string token,
        string platform,
        CancellationToken cancellationToken = default);

    Task UnregisterDeviceTokenAsync(
        Guid userAccountId,
        string token,
        CancellationToken cancellationToken = default);
}

/// <summary>Diffusion temps réel vers les apps ouvertes (SignalR groupes privés).</summary>
public interface INotificationRealtimePublisher
{
    Task PublishToUsersAsync(
        IReadOnlyList<Guid> userAccountIds,
        ParentNotificationDto notification,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Envoi Push FCM (stub aujourd'hui, implémentation Firebase demain).
/// Ne pas appeler depuis le mobile — uniquement depuis NotificationService.
/// </summary>
public interface IPushNotificationSender
{
    Task SendAsync(
        IReadOnlyList<string> deviceTokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
