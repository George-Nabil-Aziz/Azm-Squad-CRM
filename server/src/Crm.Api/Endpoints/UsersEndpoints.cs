using Crm.Api.Auth;
using Crm.Application.Users;

namespace Crm.Api.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        // Every endpoint: 401 without a valid token, 403 for users outside the ManageUsers policy.
        var group = app.MapGroup("/api/users").RequireAuthorization(CrmPolicies.ManageUsers);

        group.MapGet("", async ([AsParameters] ListUsersQuery query, IUserService users, CancellationToken cancellationToken) =>
                Results.Ok(await users.ListAsync(query, cancellationToken)))
            .WithName("ListUsers");

        group.MapGet("/{id:guid}", async (Guid id, IUserService users, CancellationToken cancellationToken) =>
                Results.Ok(await users.GetAsync(id, cancellationToken)))
            .WithName("GetUser");

        group.MapPost("", async (CreateUserRequest request, IUserService users, CancellationToken cancellationToken) =>
            {
                var user = await users.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/users/{user.Id}", user);
            })
            .WithName("CreateUser");

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, IUserService users, CancellationToken cancellationToken) =>
                Results.Ok(await users.UpdateAsync(id, request, cancellationToken)))
            .WithName("UpdateUser");

        group.MapPost("/{id:guid}/deactivate", async (Guid id, IUserService users, CancellationToken cancellationToken) =>
            {
                await users.DeactivateAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeactivateUser");

        group.MapPost("/{id:guid}/reactivate", async (Guid id, IUserService users, CancellationToken cancellationToken) =>
            {
                await users.ReactivateAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("ReactivateUser");

        return app;
    }
}
