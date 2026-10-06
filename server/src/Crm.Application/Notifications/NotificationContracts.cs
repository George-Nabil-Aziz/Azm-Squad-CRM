using Crm.Application.Common.Paging;
using Crm.Domain.Notifications;

namespace Crm.Application.Notifications;

/// <summary>
/// A notification as the API and the SignalR hub send it. <c>Type</c> is "assignment", "slaWarning", "slaEscalation",
/// "taskReminder" or "mention"; the client builds the message from it. <c>TicketId</c> / <c>TicketNumber</c> open the ticket.
/// </summary>
public sealed record NotificationResponse(
    Guid Id,
    string Type,
    Guid? TicketId,
    string? TicketNumber,
    int Level,
    string? Text,
    DateTime CreatedAt,
    DateTime? ReadAt);

/// <summary>API names of notification types: camelCase ("slaWarning").</summary>
public static class NotificationTypes
{
    public static string Name(NotificationType type) => char.ToLowerInvariant(type.ToString()[0]) + type.ToString()[1..];
}

/// <summary>The notification of a new assignment (CRM-28): one event per ticket, assignee and moment.</summary>
public static class AssignmentNotifications
{
    public static NotificationRequest For(Guid ticketId, Guid assigneeId, DateTime utcNow) =>
        new(NotificationType.Assignment, $"assignment:{ticketId}:{assigneeId}:{utcNow.Ticks}", [assigneeId], TicketId: ticketId);
}

/// <summary>GET /api/notifications/unread-count.</summary>
public sealed record UnreadCountResponse(int Count);

/// <summary>GET /api/notifications query: <c>unreadOnly</c> (default false), <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).</summary>
public sealed record ListNotificationsQuery(bool? UnreadOnly, int? Page, int? PageSize);

/// <summary>
/// One event to notify about. <see cref="DedupKey"/> identifies the event: the same key never notifies the same user twice.
/// Recipients are <see cref="UserIds"/> plus every active user holding <see cref="Role"/>; inactive users are skipped.
/// </summary>
public sealed record NotificationRequest(
    NotificationType Type,
    string DedupKey,
    IReadOnlyList<Guid> UserIds,
    string? Role = null,
    Guid? TicketId = null,
    int Level = 0,
    string? Text = null);

/// <summary>An active staff user a notification can go to.</summary>
public sealed record StaffContact(Guid Id, string FullName, string? Email);

/// <summary>An email about a notification (SLA warnings and escalations).</summary>
public sealed record NotificationEmail(string ToAddress, string ToName, string Subject, string Body);

/// <summary>Notification storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface INotificationRepository
{
    void Add(Notification notification);

    /// <summary>True when the user already has a notification with this event key.</summary>
    Task<bool> ExistsAsync(Guid userId, string dedupKey, CancellationToken cancellationToken);

    /// <summary>Saves the added notifications; false (nothing stored) when the unique (user, key) index rejected a duplicate.</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationResponse>> GetResponsesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>One page of a user's notifications, newest first.</summary>
    Task<PagedResult<NotificationResponse>> ListAsync(
        Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The tracked notification when it belongs to the user, otherwise null.</summary>
    Task<Notification?> FindAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task SaveReadAsync(CancellationToken cancellationToken);

    /// <summary>Marks every unread notification of the user read; returns how many.</summary>
    Task<int> MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);
}

/// <summary>Looks up the staff users notifications go to (implemented with ASP.NET Identity).</summary>
public interface IStaffDirectory
{
    Task<IReadOnlyList<Guid>> ListActiveUserIdsInRoleAsync(string role, CancellationToken cancellationToken);

    /// <summary>The active users among <paramref name="ids"/> (inactive or unknown ids are left out).</summary>
    Task<IReadOnlyList<StaffContact>> ListActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

/// <summary>Pushes a new notification to the user's open sessions (SignalR in Crm.Api).</summary>
public interface INotificationPublisher
{
    Task PublishAsync(Guid userId, NotificationResponse notification, int unreadCount, CancellationToken cancellationToken);
}

/// <summary>Sends notification emails (MailKit in Crm.Infrastructure). Does nothing when SMTP is not configured.</summary>
public interface INotificationEmailSender
{
    Task SendAsync(NotificationEmail email, CancellationToken cancellationToken);
}

/// <summary>Default publisher: nobody listens (used where no hub exists).</summary>
public sealed class NoopNotificationPublisher : INotificationPublisher
{
    public Task PublishAsync(Guid userId, NotificationResponse notification, int unreadCount, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

/// <summary>A signed-in user's notifications. <c>NotFoundException</c> 404 for an unknown or foreign notification.</summary>
public interface INotificationService
{
    Task<PagedResult<NotificationResponse>> ListAsync(ListNotificationsQuery query, CancellationToken cancellationToken);

    Task<UnreadCountResponse> UnreadCountAsync(CancellationToken cancellationToken);

    Task<NotificationResponse> MarkReadAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Marks every notification of the user read; returns the new unread count (0).</summary>
    Task<UnreadCountResponse> MarkAllReadAsync(CancellationToken cancellationToken);
}

/// <summary>Creates notifications for an event, pushes them to the open sessions and emails SLA ones.</summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// One notification per active recipient that has none for this event yet. Never throws for a failed push or email
    /// (the rows are stored); returns the number of notifications created.
    /// </summary>
    Task<int> NotifyAsync(NotificationRequest request, CancellationToken cancellationToken);
}
