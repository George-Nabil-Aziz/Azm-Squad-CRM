using System.Security.Claims;
using Crm.Application.Auth;
using Crm.Application.Common.Security;
using Crm.Application.Portal;
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
                bearer.Events = new JwtBearerEvents { OnTokenValidated = RejectInactiveUserAsync };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // One policy per permission, named like the permission (Permissions.All). Endpoints name the permission they
        // need; PermissionAuthorizationHandler checks it against the user's role claims (RolePermissions).
        var authorization = services.AddAuthorizationBuilder();
        foreach (var permission in Permissions.All)
        {
            authorization.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
        }

        // Portal customers (CRM-40): role Customer, no staff permission.
        authorization.AddPolicy(PortalPolicies.Customer, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(PortalRoles.Customer));

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }

    /// <summary>
    /// A valid signature is not enough: the user must still exist and be active. A deactivated user's token
    /// stops working on the next request (401), not only when it expires.
    /// </summary>
    private static async Task RejectInactiveUserAsync(TokenValidatedContext context)
    {
        var services = context.HttpContext.RequestServices;
        var cancellationToken = context.HttpContext.RequestAborted;
        var isActive = Guid.TryParse(context.Principal?.FindFirstValue(AuthClaimTypes.UserId), out var userId)
                       && (context.Principal!.IsInRole(PortalRoles.Customer)
                           // A portal customer must still exist (not deleted); a staff user must be active.
                           ? await services.GetRequiredService<IPortalAccountRepository>().CustomerExistsAsync(userId, cancellationToken)
                           : await services.GetRequiredService<IActiveUserChecker>().IsActiveAsync(userId, cancellationToken));
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
