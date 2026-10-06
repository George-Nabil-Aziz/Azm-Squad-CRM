using Crm.Application.Common.Security;
using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// Writes ticket history (the audit trail). Everything that changes a ticket (assignment, status, priority, category, SLA
/// escalation) records the change here <b>before</b> its own SaveChangesAsync: the entry joins the same unit of work, so the
/// change and its entry are saved together. The actor is the signed-in user (null for system events).
/// </summary>
public interface ITicketHistoryRecorder
{
    /// <param name="oldValue">Value before, as text (status / priority code, assignee / category name); null = none.</param>
    /// <param name="newValue">Value after; null = none.</param>
    /// <param name="system">An automatic change (e.g. auto-assignment): recorded without the signed-in user as actor.</param>
    void Record(Guid ticketId, TicketHistoryField field, string? oldValue, string? newValue, DateTime utcNow, bool system = false);
}

/// <summary>History storage (implemented in Crm.Infrastructure with EF Core).</summary>
public interface ITicketHistoryRepository
{
    /// <summary>Adds the entry to the current unit of work; it is saved by the caller's next SaveChangesAsync.</summary>
    void Add(TicketHistoryEntry entry);

    /// <summary>A ticket history, oldest first (time, then insertion): its entries plus its SLA escalations (field "escalation").</summary>
    Task<IReadOnlyList<TicketHistoryItemResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken);
}

public sealed class TicketHistoryRecorder(ITicketHistoryRepository history, ICurrentUser currentUser) : ITicketHistoryRecorder
{
    public void Record(Guid ticketId, TicketHistoryField field, string? oldValue, string? newValue, DateTime utcNow, bool system = false) =>
        history.Add(TicketHistoryEntry.Create(ticketId, field, oldValue, newValue, system ? null : currentUser.UserId, utcNow));
}
