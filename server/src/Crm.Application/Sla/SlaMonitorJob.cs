using Crm.Domain.Notifications;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Sla;

/// <summary>Counts of what one run of <see cref="SlaMonitorJob"/> did.</summary>
public sealed record SlaMonitorResult(int ResponseBreaches, int ResolutionBreaches, int Warnings = 0, int Escalations = 0);

/// <summary>
/// The recurring SLA job (every minute): flags late tickets (CRM-21), escalates newly breached unresolved tickets to
/// the supervisors and warns assignees at 80 % of the response time (CRM-22). A plain class (no Hangfire reference);
/// Hangfire only calls <see cref="RunAsync"/>, tests call it directly. Everything is stored in one save, so a failed
/// (concurrent) run stores nothing and a level or warning can never be recorded twice.
/// </summary>
public sealed class SlaMonitorJob(ITicketSlaRepository repository, ISlaNotifier notifier, TimeProvider clock)
{
    /// <summary>Tickets handled per run and kind; the rest follow in the next minute.</summary>
    public const int BatchSize = 500;

    public async Task<SlaMonitorResult> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var notices = new List<SlaNotice>();
        int response = 0, resolution = 0, warnings = 0, escalations = 0;

        foreach (var ticket in await repository.ListBreachCandidatesAsync(now, BatchSize, cancellationToken))
        {
            if (ticket.IsResponseBreachedAt(now) && ticket.MarkResponseBreached())
            {
                repository.AddEvent(TicketSlaEvent.Create(ticket.Id, SlaEventType.ResponseBreached, 0, ticket.ResponseDueAt, now));
                response++;
                escalations += Escalate(ticket, now, notices);
            }

            if (ticket.IsResolutionBreachedAt(now) && ticket.MarkResolutionBreached())
            {
                repository.AddEvent(TicketSlaEvent.Create(ticket.Id, SlaEventType.ResolutionBreached, 0, ticket.ResolutionDueAt, now));
                resolution++;
                escalations += Escalate(ticket, now, notices);
            }
        }

        foreach (var ticket in await repository.ListWarningCandidatesAsync(now, BatchSize, cancellationToken))
        {
            if (ticket.TryWarnResponse(now))
            {
                repository.AddEvent(TicketSlaEvent.Create(ticket.Id, SlaEventType.ResponseWarning, 0, ticket.ResponseDueAt, now));
                var notification = ticket.AssigneeId is { } assignee
                    ? Notification.ForUser(assignee, ticket.Id, NotificationType.SlaWarning, 0, now)
                    : Notification.ForRole(Notification.SupervisorRole, ticket.Id, NotificationType.SlaWarning, 0, now);
                Store(notification, notices);
                warnings++;
            }
        }

        if (response + resolution + warnings == 0)
        {
            return new SlaMonitorResult(0, 0);
        }

        if (!await repository.SaveChangesAsync(cancellationToken))
        {
            return new SlaMonitorResult(0, 0);
        }

        await NotifyAsync(notices, cancellationToken);
        return new SlaMonitorResult(response, resolution, warnings, escalations);
    }

    /// <summary>A breach of an unresolved ticket raises its level, adds the history event and notifies the supervisors.</summary>
    private int Escalate(Ticket ticket, DateTime now, List<SlaNotice> notices)
    {
        if (ticket.ResolvedAt is not null)
        {
            return 0; // a resolved ticket is not escalated, even when it was resolved late
        }

        var level = ticket.Escalate(now);
        repository.AddEvent(TicketSlaEvent.Create(ticket.Id, SlaEventType.Escalated, level, null, now));
        Store(Notification.ForRole(Notification.SupervisorRole, ticket.Id, NotificationType.SlaEscalation, level, now), notices);
        return 1;
    }

    private void Store(Notification notification, List<SlaNotice> notices)
    {
        repository.AddNotification(notification);
        notices.Add(new SlaNotice(notification.TicketId, notification.Type, notification.Level, notification.RecipientUserId, notification.RecipientRole));
    }

    private async Task NotifyAsync(List<SlaNotice> notices, CancellationToken cancellationToken)
    {
        foreach (var notice in notices)
        {
            try
            {
                await notifier.NotifyAsync(notice, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A broken notifier must not stop the job; the rows are stored and the next notices still go out.
            }
        }
    }
}
