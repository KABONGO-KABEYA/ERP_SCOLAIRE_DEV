using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Notifications.DTOs;
using SchoolManagement.Application.Notifications.Interfaces;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Constants;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/parent/notifications")]
public class ParentNotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly ICurrentUserService _currentUser;

    public ParentNotificationsController(
        INotificationService notifications,
        ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentNotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInbox(
        [FromQuery] string? category,
        [FromQuery] string? q,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        NotificationCategory? cat = null;
        if (!string.IsNullOrWhiteSpace(category) &&
            Enum.TryParse<NotificationCategory>(category, ignoreCase: true, out var parsed))
        {
            cat = parsed;
        }

        var items = await _notifications.GetInboxAsync(userId, cat, q, take, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentNotificationDto>>.Ok(items));
    }

    [HttpGet("changes")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParentNotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChanges(
        [FromQuery] Guid? afterId,
        [FromQuery] DateTime? since,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await _notifications.GetChangesAsync(
            RequireUserId(),
            afterId,
            since,
            take,
            cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentNotificationDto>>.Ok(items));
    }

    [HttpGet("unread-count")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<ParentNotificationUnreadCountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await _notifications.GetUnreadCountAsync(RequireUserId(), cancellationToken);
        return Ok(ApiResponse<ParentNotificationUnreadCountDto>.Ok(new ParentNotificationUnreadCountDto(count)));
    }

    [HttpPost("{notificationId:guid}/delivered")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkDelivered(Guid notificationId, CancellationToken cancellationToken)
    {
        await _notifications.MarkDeliveredAsync(RequireUserId(), notificationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("ack")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Acknowledge(
        [FromBody] AcknowledgeDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        await _notifications.MarkDeliveredAsync(RequireUserId(), request.NotificationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("{notificationId:guid}/read")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await _notifications.MarkReadAsync(RequireUserId(), notificationId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("read-all")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _notifications.MarkAllReadAsync(RequireUserId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("device-token")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegisterDeviceToken(
        [FromBody] RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var schoolId = _currentUser.SchoolId ?? throw new UnauthorizedAccessException();
        await _notifications.RegisterDeviceTokenAsync(
            schoolId,
            userId,
            request.Token,
            request.Platform,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpDelete("device-token")]
    [Authorize(Policy = Permissions.ReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnregisterDeviceToken(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        await _notifications.UnregisterDeviceTokenAsync(RequireUserId(), token, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    private Guid RequireUserId() =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException();
}
