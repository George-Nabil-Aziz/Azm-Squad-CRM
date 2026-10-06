namespace Crm.Application.Customers.Notes;

/// <summary>Body of POST /api/customers/{id}/notes: plain text, required, at most 4000 characters.</summary>
public sealed record CustomerNoteRequest(string? Text);

/// <summary>GET /api/customers/{id}/notes query string: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).</summary>
public sealed record ListCustomerNotesQuery(int? Page, int? PageSize);

/// <summary>A note with its author (full name; null when the user no longer exists) and UTC time.</summary>
public sealed record CustomerNoteResponse(Guid Id, string Text, Guid? AuthorId, string? AuthorName, DateTime CreatedAt);
