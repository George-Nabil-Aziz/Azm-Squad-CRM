using Crm.Application.Auth;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

public static class TicketCategoriesEndpoints
{
    public static IEndpointRouteBuilder MapTicketCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        // Reading needs tickets.view (the new-ticket form and the ticket filters use the list);
        // creating and editing also need categories.manage (admins). 401 without a valid token.
        var group = app.MapGroup("/api/ticket-categories").RequireAuthorization(Permissions.TicketsView);

        group.MapGet("", async ([AsParameters] ListTicketCategoriesQuery query, ITicketCategoryService categories,
                    CancellationToken cancellationToken) =>
                Results.Ok(await categories.ListAsync(query, cancellationToken)))
            .WithName("ListTicketCategories");

        group.MapPost("", async (TicketCategoryRequest request, ITicketCategoryService categories,
                    CancellationToken cancellationToken) =>
            {
                var category = await categories.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/ticket-categories/{category.Id}", category);
            })
            .RequireAuthorization(Permissions.CategoriesManage)
            .WithName("CreateTicketCategory");

        group.MapPut("/{id:guid}", async (Guid id, TicketCategoryRequest request, ITicketCategoryService categories,
                    CancellationToken cancellationToken) =>
                Results.Ok(await categories.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.CategoriesManage)
            .WithName("UpdateTicketCategory");

        return app;
    }
}
