using SchoolManagement.Application.Notifications.DTOs;
using SchoolManagement.Application.Notifications.Interfaces;

namespace SchoolManagement.Application.Notifications.Services;

/// <summary>No-op temps réel (remplacé par SignalR dans l'API).</summary>
public sealed class NullNotificationRealtimePublisher : INotificationRealtimePublisher
{
    public Task PublishToUsersAsync(
        IReadOnlyList<Guid> userAccountIds,
        ParentNotificationDto notification,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
