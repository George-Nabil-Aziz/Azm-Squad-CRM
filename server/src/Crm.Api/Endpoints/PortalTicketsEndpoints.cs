using System.Security.Claims;
using Crm.Api.Auth;
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

        return app;
    }
}
