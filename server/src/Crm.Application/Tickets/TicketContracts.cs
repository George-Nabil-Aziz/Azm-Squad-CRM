using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// Body of POST /api/tickets. Customer and subject are required; description and category are optional;
/// priority is "high", "mid" or "low" (default "mid"). The category must be active.
/// </summary>
public sealed record CreateTicketRequest(Guid? CustomerId, string? Subject, string? Description, Guid? CategoryId, string? Priority, Guid? DepartmentId = null);

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
    DateTime UpdatedAt,
    DateTime? ResponseDueAt = null,
    DateTime? ResolutionDueAt = null,
    DateTime? FirstResponseAt = null,
    DateTime? ResolvedAt = null,
    bool ResponseBreached = false,
    bool ResolutionBreached = false,
    int EscalationLevel = 0,
    DateTime? ResponseWarnedAt = null,
    IReadOnlyList<string>? AllowedStatuses = null,
    Guid? DepartmentId = null,
    string? DepartmentName = null,
    Guid? BranchId = null);

/// <summary>Body of PUT /api/tickets/{id}/priority: "high", "mid" or "low" (CRM-20: the SLA due times are recalculated).</summary>
public sealed record ChangeTicketPriorityRequest(string? Priority);

/// <summary>A ticket with the names it shows (read model filled by the repository).</summary>
public sealed record TicketView(Ticket Ticket, string CustomerName, string? CategoryName, string? AssigneeName, string? DepartmentName = null);

/// <summary>
/// GET /api/tickets query string. Every given filter must match: <c>status</c>, <c>priority</c> (API names),
/// <c>categoryId</c>, <c>assigneeId</c> or <c>unassigned=true</c>, <c>createdFrom</c> / <c>createdTo</c> (inclusive UTC days,
/// yyyy-MM-dd), <c>search</c> (ticket number or subject); <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).
/// </summary>
public sealed record ListTicketsQuery(
    string? Status,
    string? Priority,
    Guid? CategoryId,
    Guid? AssigneeId,
    bool? Unassigned,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    string? Search,
    int? Page,
    int? PageSize,
    Guid? DepartmentId = null);

/// <summary>
/// The validated, typed ticket filters the repository applies (null = no filter). <c>CreatedBeforeUtc</c> is exclusive.
/// <c>SearchNumber</c> is the ticket number the search text stands for ("TKT-000012" → 12), if any.
/// </summary>
public sealed record TicketListFilter(
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? CategoryId,
    Guid? AssigneeId,
    bool Unassigned,
    DateTime? CreatedFromUtc,
    DateTime? CreatedBeforeUtc,
    string? Search,
    int? SearchNumber,
    Guid? CustomerId = null,
    Guid? DepartmentId = null);

/// <summary>A staff user tickets can be assigned to (assignee filter, assign picker).</summary>
public sealed record TicketAssigneeResponse(Guid Id, string FullName);
