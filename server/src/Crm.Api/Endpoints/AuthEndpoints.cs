using System.Security.Claims;
using Crm.Api.Auth;
using Crm.Application.Auth;

namespace Crm.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, IAuthService authService, CancellationToken cancellationToken) =>
                Results.Ok(await authService.LoginAsync(request, cancellationToken)))
            .AllowAnonymous()
            .WithName("Login");

        // Development only (404 elsewhere): the demo sign-ins the login pages offer. Never returns passwords.
        group.MapGet("/demo-accounts", (IHostEnvironment environment, IConfiguration configuration) =>
                DemoAccounts.IsEnabled(environment.EnvironmentName, configuration["Seed:DemoAccounts"])
                    ? Results.Ok(DemoAccounts.All)
                    : Results.NotFound())
            .AllowAnonymous()
            .WithName("GetDemoAccounts");

        // Any signed-in user (no permission): the client reads its permissions here to hide menu items and actions.
        group.MapGet("/me", (ClaimsPrincipal user) =>
            {
                string[] roles = [.. user.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value)];
                return Results.Ok(new CurrentUserResponse(
                    Guid.Parse(user.FindFirstValue(AuthClaimTypes.UserId)!),
                    user.FindFirstValue(AuthClaimTypes.Email) ?? string.Empty,
                    user.FindFirstValue(AuthClaimTypes.Name) ?? string.Empty,
                    roles,
                    RolePermissions.ForRoles(roles)));
            })
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        return app;
    }
}
