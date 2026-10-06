using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// Body of POST /api/tickets. Customer and subject are required; description and category are optional;
/// priority is "high", "mid" or "low" (default "mid"). The category must be active.
/// </summary>
public sealed record CreateTicketRequest(Guid? CustomerId, string? Subject, string? Description, Guid? CategoryId, string? Priority);

/// <summary>
/// A ticket as the API returns it. <c>Number</c> is "TKT-000001"; <c>Status</c> / <c>Priority</c> / <c>Channel</c> are the
/// lower-case names of <see cref="TicketValues"/>. Names of a deleted customer or a deactivated category are still shown.
/// </summary>
public sealed record TicketResponse(
    Guid Id,
    string Number,
    string Subject,
    string? Description,
    string Status,
    string Priority,
    string Channel,
    Guid CustomerId,
    string CustomerName,
    Guid? CategoryId,
    string? CategoryName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>A ticket with the names it shows (read model filled by the repository).</summary>
public sealed record TicketView(Ticket Ticket, string CustomerName, string? CategoryName, string? AssigneeName);
