namespace Crm.Application.Auth;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

public sealed record CurrentUserResponse(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record AccessTokenSubject(Guid UserId, string Email, string FullName, IReadOnlyList<string> Roles);

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
