using Crm.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Auth;

/// <summary>Issues HS256 JWT access tokens. Times come from the injected <see cref="TimeProvider"/>.</summary>
public sealed class JwtAccessTokenGenerator(IOptions<JwtOptions> options, TimeProvider timeProvider)
    : IAccessTokenGenerator
{
    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken Generate(AccessTokenSubject subject)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.AccessTokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = subject.UserId.ToString(),
                [JwtRegisteredClaimNames.Email] = subject.Email,
                [JwtRegisteredClaimNames.Name] = subject.FullName,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                [AuthClaimTypes.Role] = subject.Roles.ToArray(),
            },
        };

        return new AccessToken(Handler.CreateToken(descriptor), expiresAt);
    }
}

/// <summary>Claim names used in our tokens (inbound claim mapping is off, so these are the names endpoints read).</summary>
public static class AuthClaimTypes
{
    public const string UserId = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string Role = "role";
}
