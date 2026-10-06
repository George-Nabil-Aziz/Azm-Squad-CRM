using Crm.Application.Auth;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

public static class TicketHistoryEndpoints
{
    public static IEndpointRouteBuilder MapTicketHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        // The history is read-only: there is deliberately no POST / PUT / PATCH / DELETE for it (CRM-18 AC 3).
        app.MapGet("/api/tickets/{id:guid}/history", async (Guid id, ITicketHistoryService history,
                    CancellationToken cancellationToken) =>
                Results.Ok(await history.ListAsync(id, cancellationToken)))
            .RequireAuthorization(Permissions.TicketsView)
            .WithName("ListTicketHistory");

        app.MapPut("/api/tickets/{id:guid}/category", async (Guid id, ChangeTicketCategoryRequest request,
                    ITicketCategoryChangeService category, CancellationToken cancellationToken) =>
                Results.Ok(await category.ChangeAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.TicketsView, Permissions.TicketsManage)
            .WithName("ChangeTicketCategory");

        return app;
    }
}
