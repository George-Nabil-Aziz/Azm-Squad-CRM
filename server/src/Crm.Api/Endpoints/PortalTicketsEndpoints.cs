using System.Security.Claims;
using Crm.Api.Auth;
using Crm.Application.Common.Paging;
using Crm.Application.Customers.Attachments;
using Crm.Application.Portal;

namespace Crm.Api.Endpoints;

/// <summary>Tickets of the signed-in portal customer (CRM-41+). Every endpoint needs the Portal policy (a customer token).</summary>
public static class PortalTicketsEndpoints
{
    public static IEndpointRouteBuilder MapPortalTicketsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal").RequireAuthorization(PortalPolicies.Customer);

        group.MapGet("/ticket-categories", async (IPortalTicketService tickets, CancellationToken cancellationToken) =>
                Results.Ok(await tickets.ListCategoriesAsync(cancellationToken)))
            .WithName("ListPortalTicketCategories");

        // Multipart: subject, description, categoryId and any number of "files" parts (the Application layer limits them).
        // No antiforgery: bearer header authentication, not cookies.
        group.MapPost("/tickets", async (HttpRequest http, ClaimsPrincipal user, IPortalTicketService tickets,
                    CancellationToken cancellationToken) =>
            {
                var form = await http.ReadFormAsync(cancellationToken);
                var streams = new List<Stream>();
                try
                {
                    var files = new List<UploadAttachmentRequest>();
                    foreach (var file in form.Files.Where(f => f.Name == "files"))
                    {
                        var stream = file.OpenReadStream();
                        streams.Add(stream);
                        files.Add(new UploadAttachmentRequest(file.FileName, file.Length, stream));
                    }

                    var request = new PortalSubmitTicketRequest(
                        form["subject"].ToString(),
                        form["description"].ToString(),
                        Guid.TryParse(form["categoryId"].ToString(), out var categoryId) ? categoryId : null,
                        files);
                    var ticket = await tickets.SubmitAsync(PortalUser.CustomerId(user), request, cancellationToken);
                    return Results.Created($"/api/portal/tickets/{ticket.Id}", ticket);
                }
                finally
                {
                    foreach (var stream in streams)
                    {
                        await stream.DisposeAsync();
                    }
                }
            })
            .DisableAntiforgery()
            .WithName("PortalSubmitTicket");

        // The customer's own tickets: anything else (another customer's ticket, unknown id) is 404.
        group.MapGet("/tickets", async (int? page, int? pageSize, ClaimsPrincipal user, IPortalTicketTracker tracker,
                    CancellationToken cancellationToken) =>
                Results.Ok(await tracker.ListAsync(
                    PortalUser.CustomerId(user), page ?? PagingDefaults.DefaultPage, pageSize ?? PagingDefaults.DefaultPageSize, cancellationToken)))
            .WithName("PortalListTickets");

        group.MapGet("/tickets/{id:guid}", async (Guid id, ClaimsPrincipal user, IPortalTicketTracker tracker,
                    CancellationToken cancellationToken) =>
                Results.Ok(await tracker.GetAsync(PortalUser.CustomerId(user), id, cancellationToken)))
            .WithName("PortalGetTicket");

        group.MapGet("/tickets/{id:guid}/messages", async (Guid id, ClaimsPrincipal user, IPortalTicketTracker tracker,
                    CancellationToken cancellationToken) =>
                Results.Ok(await tracker.ListMessagesAsync(PortalUser.CustomerId(user), id, cancellationToken)))
            .WithName("PortalListTicketMessages");

        group.MapPost("/tickets/{id:guid}/messages", async (Guid id, PortalReplyRequest request, ClaimsPrincipal user,
                    IPortalTicketTracker tracker, CancellationToken cancellationToken) =>
                Results.Created($"/api/portal/tickets/{id}/messages",
                    await tracker.ReplyAsync(PortalUser.CustomerId(user), id, request, cancellationToken)))
            .WithName("PortalReplyToTicket");

        group.MapGet("/tickets/{id:guid}/history", async (Guid id, ClaimsPrincipal user, IPortalTicketTracker tracker,
                    CancellationToken cancellationToken) =>
                Results.Ok(await tracker.ListHistoryAsync(PortalUser.CustomerId(user), id, cancellationToken)))
            .WithName("PortalTicketHistory");

        group.MapPost("/tickets/{id:guid}/reopen", async (Guid id, ClaimsPrincipal user, IPortalTicketTracker tracker,
                    CancellationToken cancellationToken) =>
                Results.Ok(await tracker.ReopenAsync(PortalUser.CustomerId(user), id, cancellationToken)))
            .WithName("PortalReopenTicket");

        return app;
    }
}
