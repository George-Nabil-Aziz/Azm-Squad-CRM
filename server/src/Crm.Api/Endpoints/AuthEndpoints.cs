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

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new CurrentUserResponse(
                Guid.Parse(user.FindFirstValue(AuthClaimTypes.UserId)!),
                user.FindFirstValue(AuthClaimTypes.Email) ?? string.Empty,
                user.FindFirstValue(AuthClaimTypes.Name) ?? string.Empty,
                [.. user.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value)])))
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        return app;
    }
}
