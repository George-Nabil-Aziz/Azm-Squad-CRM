namespace Crm.Application.Customers;

/// <summary>GET /api/customers query string: <c>search</c> (name, phone or email), <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).</summary>
public sealed record ListCustomersQuery(string? Search, int? Page, int? PageSize);

/// <summary>
/// Body of POST /api/customers and PUT /api/customers/{id}. Only the name is required. Email and phone are the
/// customer's primary email / phone contact (phone in any common format; stored as E.164).
/// </summary>
public sealed record CustomerRequest(string? Name, string? Email, string? Phone);

/// <summary>
/// A customer as the API returns it. <c>Email</c> / <c>Phone</c> are the primary email / phone; <c>Contacts</c> lists
/// every contact (phones first, then emails, then WhatsApp numbers; primary first). <c>CreatedAt</c> / <c>UpdatedAt</c> are UTC.
/// </summary>
public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CustomerContactResponse> Contacts);

/// <summary>One contact: <c>Type</c> is "phone", "email" or "whatsapp"; numbers are E.164, emails lower case.</summary>
public sealed record CustomerContactResponse(Guid Id, string Type, string Value, bool IsPrimary);

/// <summary>
/// Body of POST /api/customers/{id}/contacts. <c>Type</c>: "phone", "email" or "whatsapp". <c>IsPrimary</c> true makes
/// it the primary contact of its type (the first contact of a type is always primary).
/// </summary>
public sealed record CustomerContactRequest(string? Type, string? Value, bool? IsPrimary);

/// <summary>
/// GET /api/customers/lookup query string: exactly one of <c>phone</c> (any common format; matches phone and WhatsApp
/// contacts) or <c>email</c> (case-insensitive). Exact match on the stored value, unlike the list search.
/// </summary>
public sealed record CustomerLookupQuery(string? Phone, string? Email);
