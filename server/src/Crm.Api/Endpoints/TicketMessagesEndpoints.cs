using Crm.Application.Auth;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

public static class TicketMessagesEndpoints
{
    public static IEndpointRouteBuilder MapTicketMessagesEndpoints(this IEndpointRouteBuilder app)
    {
        // The thread of one ticket: reading needs tickets.view, replying / adding notes tickets.manage.
        var group = app.MapGroup("/api/tickets/{id:guid}/messages").RequireAuthorization(Permissions.TicketsView);

        group.MapGet("", async (Guid id, ITicketMessageService messages, CancellationToken cancellationToken) =>
                Results.Ok(await messages.ListAsync(id, cancellationToken)))
            .WithName("ListTicketMessages");

        group.MapPost("", async (Guid id, AddTicketMessageRequest request, ITicketMessageService messages,
                    CancellationToken cancellationToken) =>
            {
                var message = await messages.AddAsync(id, request, cancellationToken);
                return Results.Created($"/api/tickets/{id}/messages", message);
            })
            .RequireAuthorization(Permissions.TicketsManage)
            .WithName("AddTicketMessage");

        return app;
    }
}
