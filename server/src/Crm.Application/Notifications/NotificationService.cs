using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Localization;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Sla;
using Crm.Domain.Notifications;
using FluentValidation;

namespace Crm.Application.Notifications;

public sealed class ListNotificationsQueryValidator : AbstractValidator<ListNotificationsQuery>
{
    public ListNotificationsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(PagingDefaults.DefaultPage).WithName(_ => PagingText.PageField);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagingDefaults.MaxPageSize).WithName(_ => PagingText.PageSizeField);
    }
}

public sealed class NotificationService(
    INotificationRepository notifications,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<ListNotificationsQuery> listValidator) : INotificationService
{
    public async Task<PagedResult<NotificationResponse>> ListAsync(ListNotificationsQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        return await notifications.ListAsync(
            UserId(),
            query.UnreadOnly == true,
            query.Page ?? PagingDefaults.DefaultPage,
            query.PageSize ?? PagingDefaults.DefaultPageSize,
            cancellationToken);
    }

    public async Task<UnreadCountResponse> UnreadCountAsync(CancellationToken cancellationToken) =>
        new(await notifications.CountUnreadAsync(UserId(), cancellationToken));

    public async Task<NotificationResponse> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await notifications.FindAsync(id, UserId(), cancellationToken)
                           ?? throw new NotFoundException(NotificationText.NotFound);
        if (notification.MarkRead(timeProvider.GetUtcNow().UtcDateTime))
        {
            await notifications.SaveReadAsync(cancellationToken);
        }

        return (await notifications.GetResponsesAsync([id], cancellationToken)).Single();
    }

    public async Task<UnreadCountResponse> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(UserId(), timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return new UnreadCountResponse(0);
    }

    private Guid UserId() => currentUser.UserId ?? throw new UnauthorizedException(NotificationText.NotFound);
}

/// <summary>User-facing text of the notifications feature, in the request language.</summary>
public static class NotificationText
{
    public static string NotFound => LocalizedText.Get(
        "The notification was not found.",
        "الإشعار غير موجود.");
}

/// <summary>Turns SLA job notices into notifications (replaces the logging notifier of CRM-22).</summary>
public sealed class SlaNotifier(INotificationDispatcher dispatcher) : ISlaNotifier
{
    public Task NotifyAsync(SlaNotice notice, CancellationToken cancellationToken) =>
        dispatcher.NotifyAsync(
            new NotificationRequest(
                notice.Type,
                notice.Type == NotificationType.SlaEscalation
                    ? $"sla-escalation:{notice.TicketId}:{notice.Level}"
                    : $"sla-warning:{notice.TicketId}",
                notice.RecipientUserId is { } user ? [user] : [],
                notice.RecipientRole,
                notice.TicketId,
                notice.Level),
            cancellationToken);
}
