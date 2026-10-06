using Crm.Api.Auth;
using Crm.Application.Auth;
using Crm.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Crm.Api.Notifications;

/// <summary>
/// Real-time notifications (CRM-28), mapped at <see cref="Path"/>. The server only pushes: the "notification" message
/// carries the new <see cref="NotificationResponse"/> and the user's unread count. Connections are per user (the JWT
/// <c>sub</c> claim); browsers send the token as the <c>access_token</c> query string (WebSockets cannot set headers).
/// </summary>
[Authorize(Policy = Permissions.NotificationsView)]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";

    /// <summary>Name of the client method that receives a new notification.</summary>
    public const string NotificationMessage = "notification";
}

/// <summary>SignalR user id = the user's id (the <c>sub</c> claim; inbound claim mapping is off).</summary>
public sealed class NotificationUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirst(AuthClaimTypes.UserId)?.Value;
}

/// <summary>Pushes notifications to the open sessions of the user through <see cref="NotificationsHub"/>.</summary>
public sealed class SignalRNotificationPublisher(IHubContext<NotificationsHub> hub) : INotificationPublisher
{
    public Task PublishAsync(Guid userId, NotificationResponse notification, int unreadCount, CancellationToken cancellationToken) =>
        hub.Clients.User(userId.ToString()).SendAsync(
            NotificationsHub.NotificationMessage, new { notification, unreadCount }, cancellationToken);
}
