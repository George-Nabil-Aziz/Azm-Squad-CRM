namespace Crm.Application.Tickets;

/// <summary>
/// One entry of a ticket history. <c>Field</c> is "status", "assignee", "priority", "category" or "escalation" (an SLA
/// escalation: <c>NewValue</c> = the level, no user). Status / priority values are API codes ("open", "high"), assignee /
/// category values names; null = none. <c>ChangedByName</c> is null for system changes. Times are UTC.
/// </summary>
public sealed record TicketHistoryItemResponse(
    string Id,
    string Field,
    string? OldValue,
    string? NewValue,
    Guid? ChangedById,
    string? ChangedByName,
    DateTime ChangedAt);

/// <summary>Body of PUT /api/tickets/{id}/category: the new category (must be active); null = no category.</summary>
public sealed record ChangeTicketCategoryRequest(Guid? CategoryId);
