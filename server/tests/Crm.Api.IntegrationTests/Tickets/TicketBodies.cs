namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>JSON shape of /api/ticket-categories responses, as the client sees them.</summary>
public sealed record TicketCategoryBody(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
