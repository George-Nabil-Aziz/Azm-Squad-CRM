namespace Crm.Application.Customers;

/// <summary>GET /api/customers query string: <c>search</c> (name, phone or email), <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).</summary>
public sealed record ListCustomersQuery(string? Search, int? Page, int? PageSize);

/// <summary>Body of POST /api/customers and PUT /api/customers/{id}. Only the name is required.</summary>
public sealed record CustomerRequest(string? Name, string? Email, string? Phone);

/// <summary>A customer as the API returns it. <c>CreatedAt</c> / <c>UpdatedAt</c> are UTC.</summary>
public sealed record CustomerResponse(
    Guid Id, string Name, string? Email, string? Phone, DateTime CreatedAt, DateTime UpdatedAt);
