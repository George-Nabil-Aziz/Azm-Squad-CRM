namespace Crm.Application.Users;

/// <summary>GET /api/users query string: <c>search</c> (name or email), <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).</summary>
public sealed record ListUsersQuery(string? Search, int? Page, int? PageSize);

public sealed record CreateUserRequest(
    string? Email, string? FullName, string? Password, IReadOnlyList<string>? Roles, IReadOnlyList<Guid>? DepartmentIds = null);

/// <summary><c>DepartmentIds</c>: null = unchanged, an empty list removes the user from every department (CRM-61).</summary>
public sealed record UpdateUserRequest(
    string? Email, string? FullName, IReadOnlyList<string>? Roles, IReadOnlyList<Guid>? DepartmentIds = null);

public sealed record UserResponse(
    Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, bool IsActive, IReadOnlyList<Guid>? DepartmentIds = null);
