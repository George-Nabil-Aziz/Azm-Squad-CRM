using Crm.Domain.Notifications;

namespace Crm.Application.Sla;

/// <summary>What the SLA job tells somebody: exactly one of the recipients is set.</summary>
public sealed record SlaNotice(Guid TicketId, NotificationType Type, int Level, Guid? RecipientUserId, string? RecipientRole);

/// <summary>
/// Delivers SLA warnings and escalations (CRM-22). The default implementation only logs; a later story sends e-mail or
/// SignalR messages. The job stores the <see cref="Notification"/> rows itself and never fails when a notifier does.
/// </summary>
public interface ISlaNotifier
{
    Task NotifyAsync(SlaNotice notice, CancellationToken cancellationToken);
}
