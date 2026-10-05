namespace Crm.Application.Auth;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

/// <summary>GET /api/auth/me. <c>Permissions</c> = the union of the roles' permissions (<see cref="RolePermissions"/>), in catalogue order.</summary>
public sealed record CurrentUserResponse(
    Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public sealed record AccessTokenSubject(Guid UserId, string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
