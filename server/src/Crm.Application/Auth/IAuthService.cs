namespace Crm.Application.Auth;

public interface IAuthService
{
    /// <summary>
    /// Checks email + password and returns a signed access token.
    /// Throws <c>ValidationException</c> (400) for an invalid request and
    /// <c>UnauthorizedException</c> (401) for unknown email, wrong password or a locked-out user.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

/// <summary>Creates signed access tokens. Implemented in Crm.Api (JWT); Application does not know the format.</summary>
public interface IAccessTokenGenerator
{
    AccessToken Generate(AccessTokenSubject subject);
}
