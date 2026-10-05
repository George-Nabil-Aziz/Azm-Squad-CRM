namespace Crm.Api.IntegrationTests.Users;

/// <summary>JSON shapes of /api/users responses, as the client sees them.</summary>
public sealed record UserBody(Guid Id, string Email, string FullName, string[] Roles, bool IsActive);

public sealed record UserPageBody(UserBody[] Items, int Page, int PageSize, int TotalCount);
