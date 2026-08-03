using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Notifications.Interfaces;

namespace SchoolManagement.API.Hubs;

/// <summary>
/// Hub temps réel des notifications parent.
/// Isolation stricte : chaque connexion rejoint uniquement <c>parent-{UserAccountId}</c>.
/// Jamais Clients.All / groupes partagés.
/// </summary>
[Authorize]
public sealed class ParentNotificationsHub : Hub
{
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public ParentNotificationsHub(
        ICurrentUserService currentUser,
        INotificationService notifications)
    {
        _currentUser = currentUser;
        _notifications = notifications;
    }

    /// <summary>Canal privé d'un compte parent (UserAccountId du JWT).</summary>
    public static string ParentGroup(Guid parentUserAccountId) =>
        $"parent-{parentUserAccountId:D}";

    /// <summary>Alias historique — préférer <see cref="ParentGroup"/>.</summary>
    public static string UserGroup(Guid userId) => ParentGroup(userId);

    public override async Task OnConnectedAsync()
    {
        if (_currentUser.UserId is Guid userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ParentGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_currentUser.UserId is Guid userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ParentGroup(userId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Accusé de réception client (livrée). Prépare le pipeline Sent → Delivered → Read.
    /// </summary>
    public async Task AcknowledgeDelivery(Guid notificationId)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return;
        }

        await _notifications.MarkDeliveredAsync(userId, notificationId);
    }
}
