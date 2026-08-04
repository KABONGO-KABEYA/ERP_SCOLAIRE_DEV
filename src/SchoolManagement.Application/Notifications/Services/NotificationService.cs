using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Notifications.DTOs;
using SchoolManagement.Application.Notifications.Interfaces;
using SchoolManagement.Domain.Entities.Notifications;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Notifications.Services;

public sealed class NotificationService : INotificationService
{
    private readonly IRepository<SchoolNotification> _notificationRepository;
    private readonly IRepository<NotificationRecipient> _recipientRepository;
    private readonly IRepository<ParentDeviceToken> _deviceTokenRepository;
    private readonly IRepository<StudentGuardian> _studentGuardianRepository;
    private readonly IRepository<UserAccount> _userAccountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationRealtimePublisher _realtime;
    private readonly IPushNotificationSender _push;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IRepository<SchoolNotification> notificationRepository,
        IRepository<NotificationRecipient> recipientRepository,
        IRepository<ParentDeviceToken> deviceTokenRepository,
        IRepository<StudentGuardian> studentGuardianRepository,
        IRepository<UserAccount> userAccountRepository,
        IUnitOfWork unitOfWork,
        INotificationRealtimePublisher realtime,
        IPushNotificationSender push,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _recipientRepository = recipientRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _studentGuardianRepository = studentGuardianRepository;
        _userAccountRepository = userAccountRepository;
        _unitOfWork = unitOfWork;
        _realtime = realtime;
        _push = push;
        _logger = logger;
    }

    public Task<Guid> NotifyStudentParentsAsync(
        Guid schoolId,
        Guid studentId,
        NotificationCategory category,
        NotificationEventType eventType,
        string title,
        string body,
        string? dataJson = null,
        string? deepLink = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new SendNotificationRequest(
                schoolId,
                studentId,
                category,
                eventType,
                title,
                body,
                dataJson,
                deepLink),
            explicitUserAccountIds: null,
            cancellationToken);

    public async Task<Guid> SendAsync(
        SendNotificationRequest request,
        IReadOnlyList<Guid>? explicitUserAccountIds = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Titre et message de notification obligatoires.");
        }

        var recipients = await ResolveRecipientsAsync(
            request.SchoolId,
            request.StudentId,
            explicitUserAccountIds,
            cancellationToken);

        if (recipients.Count == 0)
        {
            _logger.LogInformation(
                "Notification ignorée (aucun parent) school={SchoolId} student={StudentId} type={EventType}",
                request.SchoolId,
                request.StudentId,
                request.EventType);
            return Guid.Empty;
        }

        var notification = new SchoolNotification
        {
            SchoolId = request.SchoolId,
            StudentId = request.StudentId,
            Category = request.Category,
            EventType = request.EventType,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            DataJson = request.DataJson,
            DeepLink = request.DeepLink,
            OccurredAt = request.OccurredAt ?? DateTime.UtcNow,
        };

        await _notificationRepository.AddAsync(notification, cancellationToken);

        var recipientEntities = new List<NotificationRecipient>(recipients.Count);
        foreach (var (userId, guardianId) in recipients)
        {
            var row = new NotificationRecipient
            {
                NotificationId = notification.Id,
                UserAccountId = userId,
                GuardianId = guardianId,
                IsRead = false,
            };
            recipientEntities.Add(row);
            await _recipientRepository.AddAsync(row, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[PushAudit] Notification créée Id={NotificationId} OccurredAt={OccurredAt} recipients={RecipientCount} userIds={UserIds}",
            notification.Id,
            notification.OccurredAt,
            recipientEntities.Count,
            string.Join(',', recipientEntities.Select(r => r.UserAccountId).Distinct()));

        // Une publication SignalR par parent distinct (groupe privé parent-{userId}).
        foreach (var recipient in recipientEntities
                     .GroupBy(r => r.UserAccountId)
                     .Select(g => g.First()))
        {
            var dto = MapDto(notification, recipient);
            try
            {
                await _realtime.PublishToUsersAsync(
                    [recipient.UserAccountId],
                    dto,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec diffusion SignalR notification {Id}", notification.Id);
            }
        }

        var userIds = recipientEntities.Select(r => r.UserAccountId).Distinct().ToList();
        // FCM (stub aujourd'hui) — prêt pour branchement Firebase sans refonte.
        await TrySendPushAsync(userIds, notification, cancellationToken);

        return notification.Id;
    }

    public async Task<IReadOnlyList<ParentNotificationDto>> GetInboxAsync(
        Guid schoolId,
        Guid userAccountId,
        NotificationCategory? category = null,
        string? search = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var recipients = await _recipientRepository.FindAsync(
            r => r.UserAccountId == userAccountId,
            cancellationToken);

        if (recipients.Count == 0)
        {
            return [];
        }

        var notificationIds = recipients.Select(r => r.NotificationId).ToHashSet();
        var notifications = await _notificationRepository.FindAsync(
            n => notificationIds.Contains(n.Id) && n.SchoolId == schoolId,
            cancellationToken);

        var byId = notifications.ToDictionary(n => n.Id);
        var query = recipients
            .Where(r => byId.ContainsKey(r.NotificationId))
            .Select(r => (Recipient: r, Notification: byId[r.NotificationId]));

        if (category.HasValue)
        {
            query = query.Where(x => x.Notification.Category == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(x =>
                x.Notification.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Notification.Body.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        // Un parent peut avoir plusieurs lignes destinataire pour la même notif
        // (liens tuteur dupliqués) — une seule entrée par notification.
        return query
            .GroupBy(x => x.Notification.Id)
            .Select(g => g.OrderByDescending(x => x.Recipient.ReadAt ?? DateTime.MinValue).First())
            .OrderByDescending(x => x.Notification.OccurredAt)
            .Take(Math.Clamp(take, 1, 500))
            .Select(x => MapDto(x.Notification, x.Recipient))
            .ToList();
    }

    public async Task<IReadOnlyList<ParentNotificationDto>> GetChangesAsync(
        Guid schoolId,
        Guid userAccountId,
        Guid? afterId = null,
        DateTime? since = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var recipients = await _recipientRepository.FindAsync(
            r => r.UserAccountId == userAccountId,
            cancellationToken);

        if (recipients.Count == 0)
        {
            return [];
        }

        var notificationIds = recipients.Select(r => r.NotificationId).ToHashSet();
        var notifications = await _notificationRepository.FindAsync(
            n => notificationIds.Contains(n.Id) && n.SchoolId == schoolId,
            cancellationToken);
        var byId = notifications.ToDictionary(n => n.Id);

        DateTime? afterOccurredAt = null;
        if (afterId.HasValue && byId.TryGetValue(afterId.Value, out var afterNotif))
        {
            afterOccurredAt = afterNotif.OccurredAt;
        }
        else if (afterId.HasValue)
        {
            _logger.LogWarning(
                "[PushAudit] GET /changes afterId introuvable dans la boîte parent — filtre cursor ignoré, risque liste vide. UserAccountId={UserAccountId} afterId={AfterId}",
                userAccountId,
                afterId.Value);
        }

        var query = recipients
            .Where(r => byId.ContainsKey(r.NotificationId))
            .Select(r => (Recipient: r, Notification: byId[r.NotificationId]))
            .AsEnumerable();

        if (afterOccurredAt.HasValue)
        {
            var cursorId = afterId!.Value;
            var cursorAt = afterOccurredAt.Value;
            query = query.Where(x =>
                x.Notification.OccurredAt > cursorAt
                || (x.Notification.OccurredAt == cursorAt && x.Notification.Id.CompareTo(cursorId) > 0));
        }
        else if (since.HasValue)
        {
            var sinceUtc = since.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(since.Value, DateTimeKind.Utc)
                : since.Value.ToUniversalTime();
            query = query.Where(x => x.Notification.OccurredAt > sinceUtc);
        }
        else
        {
            // Sans curseur : ne rien renvoyer (évite de re-télécharger toute la boîte).
            _logger.LogInformation(
                "[PushAudit] GET /changes sans curseur valide — []. UserAccountId={UserAccountId} afterId={AfterId} since={Since}",
                userAccountId,
                afterId,
                since);
            return [];
        }

        var result = query
            .GroupBy(x => x.Notification.Id)
            .Select(g => g.OrderByDescending(x => x.Recipient.ReadAt ?? DateTime.MinValue).First())
            .OrderBy(x => x.Notification.OccurredAt)
            .ThenBy(x => x.Notification.Id)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => MapDto(x.Notification, x.Recipient))
            .ToList();

        _logger.LogInformation(
            "[PushAudit] GET /changes UserAccountId={UserAccountId} afterId={AfterId} afterResolved={AfterResolved} since={Since} count={Count} ids={Ids}",
            userAccountId,
            afterId,
            afterOccurredAt.HasValue,
            since,
            result.Count,
            result.Count == 0 ? "(empty)" : string.Join(',', result.Select(x => x.Id)));

        return result;
    }

    public async Task<int> GetUnreadCountAsync(
        Guid schoolId,
        Guid userAccountId,
        CancellationToken cancellationToken = default)
    {
        var recipients = await _recipientRepository.FindAsync(
            r => r.UserAccountId == userAccountId && !r.IsRead,
            cancellationToken);
        if (recipients.Count == 0)
        {
            return 0;
        }

        var notificationIds = recipients.Select(r => r.NotificationId).ToHashSet();
        var notifications = await _notificationRepository.FindAsync(
            n => notificationIds.Contains(n.Id) && n.SchoolId == schoolId,
            cancellationToken);
        var allowed = notifications.Select(n => n.Id).ToHashSet();
        return recipients.Count(r => allowed.Contains(r.NotificationId));
    }

    public async Task MarkReadAsync(
        Guid schoolId,
        Guid userAccountId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureParentNotificationSchoolAsync(
            schoolId,
            userAccountId,
            notificationId,
            cancellationToken);

        var row = (await _recipientRepository.FindAsync(
            r => r.UserAccountId == userAccountId && r.NotificationId == notificationId,
            cancellationToken)).FirstOrDefault();
        if (row is null || row.IsRead)
        {
            return;
        }

        row.IsRead = true;
        row.ReadAt = DateTime.UtcNow;
        await _recipientRepository.UpdateAsync(row, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(
        Guid schoolId,
        Guid userAccountId,
        CancellationToken cancellationToken = default)
    {
        var unread = await _recipientRepository.FindAsync(
            r => r.UserAccountId == userAccountId && !r.IsRead,
            cancellationToken);
        if (unread.Count == 0)
        {
            return;
        }

        var notificationIds = unread.Select(r => r.NotificationId).ToHashSet();
        var notifications = await _notificationRepository.FindAsync(
            n => notificationIds.Contains(n.Id) && n.SchoolId == schoolId,
            cancellationToken);
        var allowed = notifications.Select(n => n.Id).ToHashSet();

        var now = DateTime.UtcNow;
        foreach (var row in unread.Where(r => allowed.Contains(r.NotificationId)))
        {
            row.IsRead = true;
            row.ReadAt = now;
            await _recipientRepository.UpdateAsync(row, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkDeliveredAsync(
        Guid schoolId,
        Guid userAccountId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureParentNotificationSchoolAsync(
            schoolId,
            userAccountId,
            notificationId,
            cancellationToken);

        var row = (await _recipientRepository.FindAsync(
            r => r.UserAccountId == userAccountId && r.NotificationId == notificationId,
            cancellationToken)).FirstOrDefault();
        if (row is null || row.DeliveredAt.HasValue)
        {
            return;
        }

        row.DeliveredAt = DateTime.UtcNow;
        await _recipientRepository.UpdateAsync(row, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureParentNotificationSchoolAsync(
        Guid schoolId,
        Guid userAccountId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var notification = (await _notificationRepository.FindAsync(
            n => n.Id == notificationId,
            cancellationToken)).FirstOrDefault()
            ?? throw new UnauthorizedAccessException("Notification introuvable.");

        if (notification.SchoolId != schoolId)
        {
            throw new UnauthorizedAccessException("Notification hors contexte école.");
        }

        var recipient = (await _recipientRepository.FindAsync(
            r => r.UserAccountId == userAccountId && r.NotificationId == notificationId,
            cancellationToken)).FirstOrDefault();
        if (recipient is null)
        {
            throw new UnauthorizedAccessException("Notification non destinée à ce compte.");
        }
    }

    public async Task RegisterDeviceTokenAsync(
        Guid schoolId,
        Guid userAccountId,
        string token,
        string platform,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token appareil requis.");
        }

        var normalized = token.Trim();
        var plat = string.IsNullOrWhiteSpace(platform) ? "android" : platform.Trim().ToLowerInvariant();

        var existing = (await _deviceTokenRepository.FindAsync(
            t => t.UserAccountId == userAccountId && t.Token == normalized,
            cancellationToken)).FirstOrDefault();

        if (existing is not null)
        {
            existing.Platform = plat;
            existing.SchoolId = schoolId;
            existing.LastSeenAt = DateTime.UtcNow;
            await _deviceTokenRepository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            // Un même token ne doit appartenir qu'à un compte.
            var others = await _deviceTokenRepository.FindAsync(
                t => t.Token == normalized,
                cancellationToken);
            foreach (var other in others)
            {
                await _deviceTokenRepository.DeleteAsync(other, cancellationToken);
            }

            await _deviceTokenRepository.AddAsync(new ParentDeviceToken
            {
                SchoolId = schoolId,
                UserAccountId = userAccountId,
                Token = normalized,
                Platform = plat,
                LastSeenAt = DateTime.UtcNow,
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterDeviceTokenAsync(
        Guid userAccountId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var rows = await _deviceTokenRepository.FindAsync(
            t => t.UserAccountId == userAccountId && t.Token == token.Trim(),
            cancellationToken);
        foreach (var row in rows)
        {
            await _deviceTokenRepository.DeleteAsync(row, cancellationToken);
        }

        if (rows.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<List<(Guid UserId, Guid? GuardianId)>> ResolveRecipientsAsync(
        Guid schoolId,
        Guid? studentId,
        IReadOnlyList<Guid>? explicitUserAccountIds,
        CancellationToken cancellationToken)
    {
        if (explicitUserAccountIds is { Count: > 0 })
        {
            return explicitUserAccountIds
                .Distinct()
                .Select(id => (id, (Guid?)null))
                .ToList();
        }

        if (studentId is null)
        {
            return [];
        }

        var links = await _studentGuardianRepository.FindAsync(
            sg => sg.StudentId == studentId.Value,
            cancellationToken);
        if (links.Count == 0)
        {
            return [];
        }

        var guardianIds = links.Select(l => l.GuardianId).Distinct().ToHashSet();
        var accounts = await _userAccountRepository.FindAsync(
            u => u.SchoolId == schoolId
                 && u.GuardianId != null
                 && guardianIds.Contains(u.GuardianId.Value)
                 && u.IsActive,
            cancellationToken);

        return accounts
            .Where(a => a.GuardianId.HasValue)
            .Select(a => (a.Id, a.GuardianId))
            .Distinct()
            .ToList();
    }

    private async Task TrySendPushAsync(
        IReadOnlyList<Guid> userIds,
        SchoolNotification notification,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var tokens = await _deviceTokenRepository.FindAsync(
            t => userIds.Contains(t.UserAccountId),
            cancellationToken);
        if (tokens.Count == 0)
        {
            return;
        }

        var data = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString(),
            ["category"] = notification.Category.ToString(),
            ["eventType"] = notification.EventType.ToString(),
        };
        if (notification.StudentId.HasValue)
        {
            data["studentId"] = notification.StudentId.Value.ToString();
        }
        if (!string.IsNullOrWhiteSpace(notification.DeepLink))
        {
            data["deepLink"] = notification.DeepLink!;
        }

        try
        {
            await _push.SendAsync(
                tokens.Select(t => t.Token).Distinct().ToList(),
                notification.Title,
                notification.Body,
                data,
                cancellationToken);

            var recipients = await _recipientRepository.FindAsync(
                r => r.NotificationId == notification.Id,
                cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var r in recipients)
            {
                r.PushSentAt = now;
                await _recipientRepository.UpdateAsync(r, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Échec envoi Push notification {Id}", notification.Id);
        }
    }

    private static ParentNotificationDto MapDto(
        SchoolNotification notification,
        NotificationRecipient recipient)
    {
        var status = recipient.IsRead
            ? "Read"
            : recipient.DeliveredAt.HasValue
                ? "Delivered"
                : "Sent";

        return new ParentNotificationDto(
            notification.Id,
            recipient.Id,
            notification.SchoolId,
            notification.Title,
            notification.Body,
            notification.OccurredAt,
            recipient.IsRead,
            notification.Category.ToString(),
            notification.EventType.ToString(),
            notification.StudentId,
            notification.DeepLink,
            status);
    }
}
