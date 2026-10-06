using Crm.Domain.Notifications;

namespace Crm.Application.Notifications;

/// <summary>
/// The one place that creates notifications (CRM-28): resolves the recipients, stores one row per user (never twice for
/// the same <see cref="NotificationRequest.DedupKey"/>), then pushes it in real time and emails SLA notifications.
/// </summary>
public sealed class NotificationDispatcher(
    INotificationRepository notifications,
    IStaffDirectory staff,
    INotificationPublisher publisher,
    INotificationEmailSender email,
    TimeProvider timeProvider) : INotificationDispatcher
{
    public async Task<int> NotifyAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>(request.UserIds);
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            ids.UnionWith(await staff.ListActiveUserIdsInRoleAsync(request.Role, cancellationToken));
        }

        if (ids.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var created = new List<(Notification Notification, StaffContact Contact)>();
        foreach (var contact in await staff.ListActiveAsync(ids, cancellationToken))
        {
            if (await notifications.ExistsAsync(contact.Id, request.DedupKey, cancellationToken))
            {
                continue; // AC 4: this event already notified this user
            }

            var notification = Notification.ForUser(
                contact.Id, request.TicketId, request.Type, request.Level, request.DedupKey, request.Text, now);
            notifications.Add(notification);
            created.Add((notification, contact));
        }

        if (created.Count == 0 || !await notifications.SaveChangesAsync(cancellationToken))
        {
            return 0; // nothing new, or another process stored the same event first
        }

        var responses = (await notifications.GetResponsesAsync([.. created.Select(c => c.Notification.Id)], cancellationToken))
            .ToDictionary(r => r.Id);
        foreach (var (notification, contact) in created)
        {
            if (!responses.TryGetValue(notification.Id, out var response))
            {
                continue;
            }

            await PushAsync(contact.Id, response, cancellationToken);
            await EmailAsync(contact, response, cancellationToken);
        }

        return created.Count;
    }

    private async Task PushAsync(Guid userId, NotificationResponse response, CancellationToken cancellationToken)
    {
        try
        {
            var unread = await notifications.CountUnreadAsync(userId, cancellationToken);
            await publisher.PublishAsync(userId, response, unread, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A broken hub must not undo the stored notification; the user sees it on the next load.
        }
    }

    private async Task EmailAsync(StaffContact contact, NotificationResponse response, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contact.Email) || NotificationEmailContent.For(response, contact) is not { } message)
        {
            return;
        }

        try
        {
            await email.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // SMTP down or refused: the in-app notification stays.
        }
    }
}

/// <summary>Builds the (bilingual) email of SLA notifications; other types are in-app only.</summary>
public static class NotificationEmailContent
{
    public static NotificationEmail? For(NotificationResponse notification, StaffContact to)
    {
        if (to.Email is not { Length: > 0 } address)
        {
            return null;
        }

        var number = notification.TicketNumber ?? string.Empty;
        return notification.Type switch
        {
            "slaWarning" => new NotificationEmail(address, to.FullName,
                $"SLA warning: {number} / تنبيه اتفاقية مستوى الخدمة: {number}",
                $"The response time of ticket {number} is almost over (80 % has passed).\n" +
                $"اقتربت مهلة الرد على التذكرة {number} من الانتهاء (انقضى 80٪ منها)."),
            "slaEscalation" => new NotificationEmail(address, to.FullName,
                $"SLA breach: {number} (level {notification.Level}) / تجاوز اتفاقية مستوى الخدمة: {number} (المستوى {notification.Level})",
                $"Ticket {number} breached its SLA and was escalated to level {notification.Level}.\n" +
                $"تجاوزت التذكرة {number} اتفاقية مستوى الخدمة وتم تصعيدها إلى المستوى {notification.Level}."),
            _ => null,
        };
    }
}
