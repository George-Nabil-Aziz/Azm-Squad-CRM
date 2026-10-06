namespace Crm.Domain.Tickets;

/// <summary>The ticket attribute a history entry is about. Stored by name.</summary>
public enum TicketHistoryField
{
    Status = 1,
    Assignee = 2,
    Priority = 3,
    Category = 4,
}
