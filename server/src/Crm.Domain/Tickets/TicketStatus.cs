namespace Crm.Domain.Tickets;

/// <summary>Where a ticket is in its workflow (CRM-17 adds the transitions and reopening). Stored by name.</summary>
public enum TicketStatus
{
    New = 1,
    Open = 2,
    Pending = 3,
    Resolved = 4,
    Closed = 5,
}
