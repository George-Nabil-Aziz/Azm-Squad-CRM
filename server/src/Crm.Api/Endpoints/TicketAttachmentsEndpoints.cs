using Crm.Application.Auth;
using Crm.Application.Tickets;

namespace Crm.Api.Endpoints;

/// <summary>Staff access to the files customers attached to a ticket (needs tickets.view).</summary>
public static class TicketAttachmentsEndpoints
{
    public static IEndpointRouteBuilder MapTicketAttachmentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tickets/{id:guid}/attachments").RequireAuthorization(Permissions.TicketsView);

        group.MapGet("", async (Guid id, ITicketAttachmentService attachments, CancellationToken cancellationToken) =>
                Results.Ok(await attachments.ListAsync(id, cancellationToken)))
            .WithName("ListTicketAttachments");

        // Always as a download, never shown inline.
        group.MapGet("/{attachmentId:guid}", async (Guid id, Guid attachmentId, ITicketAttachmentService attachments,
                    HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var download = await attachments.DownloadAsync(id, attachmentId, cancellationToken);
                httpContext.Response.Headers.XContentTypeOptions = "nosniff";
                return Results.File(download.Content, download.ContentType, download.FileName);
            })
            .WithName("DownloadTicketAttachment");

        return app;
    }
}
