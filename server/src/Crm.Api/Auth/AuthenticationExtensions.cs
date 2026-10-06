using System.Security.Claims;
using Crm.Application.Audit;
using Crm.Application.Auth;
using Crm.Application.Common.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
                bearer.Events = new JwtBearerEvents { OnMessageReceived = ReadHubTokenFromQuery, OnTokenValidated = RejectInactiveUserAsync };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IClientInfo, HttpClientInfo>();

        // One policy per permission, named like the permission (Permissions.All). Endpoints name the permission they
        // need; PermissionAuthorizationHandler checks it against the user's role claims (RolePermissions).
        var authorization = services.AddAuthorizationBuilder();
        foreach (var permission in Permissions.All)
        {
            authorization.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
        }

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }

    /// <summary>
    /// Browsers cannot set headers on a WebSocket: SignalR sends the token as <c>access_token</c> in the query string,
    /// which is accepted for hub requests only (CRM-28).
    /// </summary>
    private static Task ReadHubTokenFromQuery(MessageReceivedContext context)
    {
        var token = context.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Token = token;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// A valid signature is not enough: the user must still exist and be active. A deactivated user's token
    /// stops working on the next request (401), not only when it expires.
    /// </summary>
    private static async Task RejectInactiveUserAsync(TokenValidatedContext context)
    {
        var checker = context.HttpContext.RequestServices.GetRequiredService<IActiveUserChecker>();
        var isActive = Guid.TryParse(context.Principal?.FindFirstValue(AuthClaimTypes.UserId), out var userId)
                       && await checker.IsActiveAsync(userId, context.HttpContext.RequestAborted);
        if (!isActive)
        {
            context.Fail("The user is deactivated or no longer exists.");
        }
    }

    /// <summary>A token without <c>exp</c> is rejected; <c>nbf</c> is optional.</summary>
    private static bool IsWithinLifetime(DateTime? notBefore, DateTime? expires, TimeSpan clockSkew, DateTime utcNow) =>
        expires is not null
        && expires.Value.ToUniversalTime() > utcNow - clockSkew
        && (notBefore is null || notBefore.Value.ToUniversalTime() <= utcNow + clockSkew);
}
