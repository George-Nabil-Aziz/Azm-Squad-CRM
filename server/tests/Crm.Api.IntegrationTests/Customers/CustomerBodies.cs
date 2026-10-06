namespace Crm.Api.IntegrationTests.Customers;

/// <summary>JSON shapes of /api/customers responses, as the client sees them.</summary>
public sealed record CustomerBody(
    Guid Id, string Name, string? Email, string? Phone, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CustomerPageBody(CustomerBody[] Items, int Page, int PageSize, int TotalCount);
