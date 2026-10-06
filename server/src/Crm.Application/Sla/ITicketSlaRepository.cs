using Crm.Domain.Notifications;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Sla;

/// <summary>Storage used by the SLA monitor job (CRM-21, CRM-22).</summary>
public interface ITicketSlaRepository
{
    /// <summary>Tracked tickets that are late at <paramref name="utcNow"/> and not flagged yet (response and/or resolution).</summary>
    Task<IReadOnlyList<Ticket>> ListBreachCandidatesAsync(DateTime utcNow, int take, CancellationToken cancellationToken);

    /// <summary>Tracked unanswered, unresolved tickets whose warning time has come and that were not warned yet.</summary>
    Task<IReadOnlyList<Ticket>> ListWarningCandidatesAsync(DateTime utcNow, int take, CancellationToken cancellationToken);

    void AddEvent(TicketSlaEvent slaEvent);

    void AddNotification(Notification notification);

    /// <summary>Saves; false when another job run saved the same events first (unique index), nothing was stored.</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}
