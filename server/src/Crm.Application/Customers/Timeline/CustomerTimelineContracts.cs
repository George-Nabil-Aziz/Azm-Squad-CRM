namespace Crm.Application.Customers.Timeline;

/// <summary>
/// GET /api/customers/{id}/timeline query string: <c>type</c> (optional: customer, note, attachment, ticket, message),
/// <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).
/// </summary>
public sealed record CustomerTimelineQuery(string? Type, int? Page, int? PageSize);

/// <summary>
/// One timeline entry. <c>Type</c> is the API name of the category, <c>Event</c> a code the client translates
/// (e.g. "customerCreated"), <c>ActorName</c> the staff user's full name (null for system / channel events),
/// <c>OccurredAt</c> UTC.
/// </summary>
public sealed record CustomerInteractionResponse(
    long Id,
    string Type,
    string Event,
    string? Details,
    Guid? SourceId,
    Guid? ActorId,
    string? ActorName,
    DateTime OccurredAt);
