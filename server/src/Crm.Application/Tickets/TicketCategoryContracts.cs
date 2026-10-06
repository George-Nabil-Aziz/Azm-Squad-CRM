namespace Crm.Application.Tickets;

/// <summary>GET /api/ticket-categories query string: <c>activeOnly=true</c> returns only the categories new tickets may use.</summary>
public sealed record ListTicketCategoriesQuery(bool? ActiveOnly);

/// <summary>
/// Body of POST /api/ticket-categories and PUT /api/ticket-categories/{id}. <c>IsActive</c>: null = active on create,
/// unchanged on update.
/// </summary>
public sealed record TicketCategoryRequest(string? Name, bool? IsActive);

/// <summary>A category as the API returns it (times UTC).</summary>
public sealed record TicketCategoryResponse(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
