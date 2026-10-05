using Crm.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Auth;

public static class AuthenticationExtensions
{
    /// <summary>JWT bearer authentication + authorization. Fails at startup when the <c>Jwt</c> section is invalid.</summary>
    public static IServiceCollection AddCrmAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => options.IsValid(),
                "Jwt settings are invalid: Issuer and Audience are required, SigningKey must be at least 32 bytes " +
                "(set it with dotnet user-secrets or the Jwt__SigningKey environment variable).")
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwtOptions, timeProvider) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = AuthClaimTypes.Email,
                    RoleClaimType = AuthClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    // The default lifetime check reads the system clock; use the injected TimeProvider
                    // (CLAUDE.md: all time through TimeProvider) so tests can control token expiry.
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                        IsWithinLifetime(notBefore, expires, parameters.ClockSkew, timeProvider.GetUtcNow().UtcDateTime),
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>A token without <c>exp</c> is rejected; <c>nbf</c> is optional.</summary>
    private static bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, TimeSpan clockSkew, DateTime utcNow) =>
        expires is not null
        && expires.Value.ToUniversalTime() > utcNow - clockSkew
        && (notBefore is null || notBefore.Value.ToUniversalTime() <= utcNow + clockSkew);
}
