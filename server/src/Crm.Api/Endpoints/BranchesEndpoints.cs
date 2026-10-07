using Crm.Application.Auth;
using Crm.Application.Branches;

namespace Crm.Api.Endpoints;

public static class BranchesEndpoints
{
    public static IEndpointRouteBuilder MapBranchesEndpoints(this IEndpointRouteBuilder app)
    {
        // Every signed-in staff user may read the list (a branch user only gets their own branch); writing needs
        // branches.manage (SuperAdmin). 401 without a valid token.
        var group = app.MapGroup("/api/branches").RequireAuthorization();

        group.MapGet("", async ([AsParameters] ListBranchesQuery query, IBranchService branches, CancellationToken cancellationToken) =>
                Results.Ok(await branches.ListAsync(query, cancellationToken)))
            .WithName("ListBranches");

        group.MapPost("", async (BranchRequest request, IBranchService branches, CancellationToken cancellationToken) =>
            {
                var branch = await branches.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/branches/{branch.Id}", branch);
            })
            .RequireAuthorization(Permissions.BranchesManage)
            .WithName("CreateBranch");

        group.MapPut("/{id:guid}", async (Guid id, BranchRequest request, IBranchService branches, CancellationToken cancellationToken) =>
                Results.Ok(await branches.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.BranchesManage)
            .WithName("UpdateBranch");

        return app;
    }
}
