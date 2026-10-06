using Crm.Application.Auth;
using Crm.Application.Notifications;

namespace Crm.Api.Endpoints;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        // Every signed-in staff user reads only their own notifications (the service filters by the current user).
        var group = app.MapGroup("/api/notifications").RequireAuthorization(Permissions.NotificationsView);

        group.MapGet("", async ([AsParameters] ListNotificationsQuery query, INotificationService notifications,
                    CancellationToken cancellationToken) =>
                Results.Ok(await notifications.ListAsync(query, cancellationToken)))
            .WithName("ListNotifications");

        group.MapGet("/unread-count", async (INotificationService notifications, CancellationToken cancellationToken) =>
                Results.Ok(await notifications.UnreadCountAsync(cancellationToken)))
            .WithName("CountUnreadNotifications");

        group.MapPost("/{id:guid}/read", async (Guid id, INotificationService notifications, CancellationToken cancellationToken) =>
                Results.Ok(await notifications.MarkReadAsync(id, cancellationToken)))
            .WithName("MarkNotificationRead");

        group.MapPost("/read-all", async (INotificationService notifications, CancellationToken cancellationToken) =>
                Results.Ok(await notifications.MarkAllReadAsync(cancellationToken)))
            .WithName("MarkAllNotificationsRead");

        return app;
    }
}
