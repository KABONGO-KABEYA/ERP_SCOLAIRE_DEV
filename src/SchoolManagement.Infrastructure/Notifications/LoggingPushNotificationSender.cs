using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Notifications.Interfaces;

namespace SchoolManagement.Infrastructure.Notifications;

/// <summary>
/// Stub FCM — journalise les envois. Brancher Firebase Admin SDK ici
/// (fichier service-account + package FirebaseAdmin) sans changer les modules métier.
/// </summary>
public sealed class LoggingPushNotificationSender : IPushNotificationSender
{
    private readonly ILogger<LoggingPushNotificationSender> _logger;

    public LoggingPushNotificationSender(ILogger<LoggingPushNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
        IReadOnlyList<string> deviceTokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (deviceTokens.Count == 0)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Push FCM (stub) → {Count} appareil(s) | {Title} | {Body}",
            deviceTokens.Count,
            title,
            body);
        return Task.CompletedTask;
    }
}
