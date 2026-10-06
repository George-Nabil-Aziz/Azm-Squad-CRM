using Crm.Domain.Customers;

namespace Crm.Application.Customers.Timeline;

/// <summary>
/// Adds an entry to a customer's timeline. Every feature that does something with a customer calls it (customers,
/// notes, attachments, tickets, messages) <b>before</b> its own SaveChangesAsync: the entry joins the same unit of
/// work, so the change and its entry are saved together. The acting user is the signed-in user (null for system /
/// channel events).
/// </summary>
public interface IInteractionRecorder
{
    void Record(Guid customerId, InteractionType type, string @event, string? details, Guid? sourceId, DateTime utcNow);
}
