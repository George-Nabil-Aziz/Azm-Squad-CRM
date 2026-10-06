using Crm.Application.Auth;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

public static class TicketStatusEndpoints
{
    public static IEndpointRouteBuilder MapTicketStatusEndpoints(this IEndpointRouteBuilder app)
    {
        // Changing the status needs tickets.view + tickets.manage; the workflow rules answer 400.
        app.MapPut("/api/tickets/{id:guid}/status", async (Guid id, ChangeTicketStatusRequest request,
                    ITicketStatusService status, CancellationToken cancellationToken) =>
                Results.Ok(await status.ChangeAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.TicketsView, Permissions.TicketsManage)
            .WithName("ChangeTicketStatus");

        return app;
    }
}
