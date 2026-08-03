using Microsoft.AspNetCore.SignalR;
using SchoolManagement.Application.Notifications.DTOs;
using SchoolManagement.Application.Notifications.Interfaces;
using SchoolManagement.API.Hubs;

namespace SchoolManagement.API.Notifications;

/// <summary>
/// Diffuse uniquement vers les groupes privés <c>parent-{userId}</c>.
/// Aucune diffusion globale (Clients.All interdit).
/// </summary>
public sealed class SignalRNotificationRealtimePublisher : INotificationRealtimePublisher
{
    private readonly IHubContext<ParentNotificationsHub> _hub;

    public SignalRNotificationRealtimePublisher(IHubContext<ParentNotificationsHub> hub)
    {
        _hub = hub;
    }

    public async Task PublishToUsersAsync(
        IReadOnlyList<Guid> userAccountIds,
        ParentNotificationDto notification,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in userAccountIds.Distinct())
        {
            await _hub.Clients
                .Group(ParentNotificationsHub.ParentGroup(userId))
                .SendAsync("notification", notification, cancellationToken);
        }
    }
}
