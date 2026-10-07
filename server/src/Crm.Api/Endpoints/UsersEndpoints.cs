using Crm.Api.Auth;
using Crm.Application.Auth;
using Crm.Application.Branches;
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

        // CRM-62: the branch of a user (branches.manage, SuperAdmin): a branch user must not be able to lift their own restriction.
        group.MapPut("/{id:guid}/branch", async (Guid id, SetUserBranchRequest request, IUserService users, CancellationToken cancellationToken) =>
                Results.Ok(await users.SetBranchAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.BranchesManage)
            .WithName("SetUserBranch");

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
