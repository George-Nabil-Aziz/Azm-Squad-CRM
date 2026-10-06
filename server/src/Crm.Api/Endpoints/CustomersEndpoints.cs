using Crm.Application.Auth;
using Crm.Application.Customers;
using Crm.Application.Customers.Attachments;
using Crm.Application.Customers.Notes;
using Crm.Application.Customers.Timeline;

namespace Crm.Api.Endpoints;

public static class CustomersEndpoints
{
    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
    {
        // Every endpoint needs customers.view; the write endpoints also need customers.manage
        // (several RequireAuthorization calls combine with AND). 401 without a valid token.
        var group = app.MapGroup("/api/customers").RequireAuthorization(Permissions.CustomersView);

        group.MapGet("", async ([AsParameters] ListCustomersQuery query, ICustomerService customers,
                    CancellationToken cancellationToken) =>
                Results.Ok(await customers.ListAsync(query, cancellationToken)))
            .WithName("ListCustomers");

        // Exact match on a phone (phone or WhatsApp contact) or email; the email / WhatsApp channels use the same
        // ICustomerService.LookupAsync to find the customer of an incoming message.
        group.MapGet("/lookup", async ([AsParameters] CustomerLookupQuery query, ICustomerService customers,
                    CancellationToken cancellationToken) =>
                Results.Ok(await customers.LookupAsync(query, cancellationToken)))
            .WithName("LookupCustomers");

        group.MapGet("/{id:guid}", async (Guid id, ICustomerService customers, CancellationToken cancellationToken) =>
                Results.Ok(await customers.GetAsync(id, cancellationToken)))
            .WithName("GetCustomer");

        // Interaction history, newest first; ?type= filters (customer, note, attachment, ticket, message).
        group.MapGet("/{id:guid}/timeline", async (Guid id, [AsParameters] CustomerTimelineQuery query,
                    ICustomerTimelineService timeline, CancellationToken cancellationToken) =>
                Results.Ok(await timeline.ListAsync(id, query, cancellationToken)))
            .WithName("GetCustomerTimeline");

        group.MapGet("/{id:guid}/notes", async (Guid id, [AsParameters] ListCustomerNotesQuery query,
                    ICustomerNoteService notes, CancellationToken cancellationToken) =>
                Results.Ok(await notes.ListAsync(id, query, cancellationToken)))
            .WithName("ListCustomerNotes");

        group.MapPost("/{id:guid}/notes", async (Guid id, CustomerNoteRequest request, ICustomerNoteService notes,
                    CancellationToken cancellationToken) =>
            {
                var note = await notes.AddAsync(id, request, cancellationToken);
                return Results.Created($"/api/customers/{id}/notes/{note.Id}", note);
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("AddCustomerNote");

        group.MapGet("/{id:guid}/attachments", async (Guid id, ICustomerAttachmentService attachments,
                    CancellationToken cancellationToken) =>
                Results.Ok(await attachments.ListAsync(id, cancellationToken)))
            .WithName("ListCustomerAttachments");

        // Multipart upload, field "file". Size and type are checked by the Application layer (400 on "file").
        // No antiforgery: the API authenticates with a bearer header, not cookies, so CSRF does not apply.
        group.MapPost("/{id:guid}/attachments", async (Guid id, IFormFile? file, ICustomerAttachmentService attachments,
                    CancellationToken cancellationToken) =>
            {
                await using var content = file?.OpenReadStream();
                var attachment = await attachments.UploadAsync(
                    id, new UploadAttachmentRequest(file?.FileName, file?.Length ?? 0, content), cancellationToken);
                return Results.Created($"/api/customers/{id}/attachments/{attachment.Id}", attachment);
            })
            .DisableAntiforgery()
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("UploadCustomerAttachment");

        // Download only through this authorized endpoint; always as a download ("attachment"), never shown inline.
        group.MapGet("/{id:guid}/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId,
                    ICustomerAttachmentService attachments, HttpContext httpContext, CancellationToken cancellationToken) =>
            {
                var download = await attachments.DownloadAsync(id, attachmentId, cancellationToken);
                httpContext.Response.Headers.XContentTypeOptions = "nosniff";
                return Results.File(download.Content, download.ContentType, download.FileName);
            })
            .WithName("DownloadCustomerAttachment");

        group.MapPost("", async (CustomerRequest request, ICustomerService customers, CancellationToken cancellationToken) =>
            {
                var customer = await customers.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/customers/{customer.Id}", customer);
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("CreateCustomer");

        group.MapPut("/{id:guid}", async (Guid id, CustomerRequest request, ICustomerService customers,
                    CancellationToken cancellationToken) =>
                Results.Ok(await customers.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("UpdateCustomer");

        group.MapDelete("/{id:guid}", async (Guid id, ICustomerService customers, CancellationToken cancellationToken) =>
            {
                await customers.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("DeleteCustomer");

        group.MapPost("/{id:guid}/contacts", async (Guid id, CustomerContactRequest request, ICustomerService customers,
                    CancellationToken cancellationToken) =>
            {
                var contact = await customers.AddContactAsync(id, request, cancellationToken);
                return Results.Created($"/api/customers/{id}/contacts/{contact.Id}", contact);
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("AddCustomerContact");

        group.MapPost("/{id:guid}/contacts/{contactId:guid}/primary", async (Guid id, Guid contactId,
                    ICustomerService customers, CancellationToken cancellationToken) =>
            {
                await customers.MakeContactPrimaryAsync(id, contactId, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("MakeCustomerContactPrimary");

        group.MapDelete("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId,
                    ICustomerService customers, CancellationToken cancellationToken) =>
            {
                await customers.RemoveContactAsync(id, contactId, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(Permissions.CustomersManage)
            .WithName("RemoveCustomerContact");

        return app;
    }
}
