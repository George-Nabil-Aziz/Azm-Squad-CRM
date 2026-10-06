using Crm.Application.Channels;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Files;
using Crm.Application.Common.Validation;
using Crm.Application.Customers;
using Crm.Application.Customers.Attachments;
using Crm.Application.Tickets;
using Crm.Domain.Channels;
using Crm.Domain.Tickets;
using FluentValidation;

namespace Crm.Application.Portal;

/// <summary>
/// What a customer sends from the portal: subject (required), description, category (optional, must be active) and up to
/// <see cref="PortalSubmitTicketRequestValidator.MaxFiles"/> files (multipart field <c>files</c>).
/// </summary>
public sealed record PortalSubmitTicketRequest(
    string? Subject, string? Description, Guid? CategoryId, IReadOnlyList<UploadAttachmentRequest> Files);

public sealed record PortalAttachmentResponse(Guid Id, string FileName, string ContentType, long Size);

/// <summary>The ticket a customer just opened: <c>Number</c> is "TKT-000001".</summary>
public sealed record PortalTicketResponse(
    Guid Id, string Number, string Subject, string Status, DateTime CreatedAt, IReadOnlyList<PortalAttachmentResponse> Attachments);

/// <summary>An active ticket category the customer can choose; <c>Name</c> is in the request language.</summary>
public sealed record PortalCategoryResponse(Guid Id, string Name);

/// <summary>Subject required, files checked with the attachment rules (10 MB, allowed types) and limited in number. Errors on "files" for the files.</summary>
public sealed class PortalSubmitTicketRequestValidator : AbstractValidator<PortalSubmitTicketRequest>
{
    public const int MaxFiles = 5;

    public PortalSubmitTicketRequestValidator(IValidator<UploadAttachmentRequest> fileValidator)
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength).WithName(_ => TicketText.SubjectField);
        RuleFor(x => x.Description).MaximumLength(Ticket.DescriptionMaxLength).WithName(_ => TicketText.DescriptionField);
        RuleFor(x => x.Files).Custom((files, context) =>
        {
            if (files.Count > MaxFiles)
            {
                context.AddFailure("files", PortalText.TooManyFiles(MaxFiles));
                return;
            }

            foreach (var file in files)
            {
                foreach (var failure in fileValidator.Validate(file).Errors)
                {
                    context.AddFailure("files", $"{file.FileName}: {failure.ErrorMessage}");
                }
            }
        });
    }
}

/// <summary>
/// Opening tickets from the portal (CRM-41). The customer is the signed-in customer (never taken from the request).
/// Failures: <c>ValidationException</c> 400 (subject, category, files), <c>NotFoundException</c> 404 (customer gone).
/// </summary>
public interface IPortalTicketService
{
    /// <summary>Creates a ticket (channel Portal, status New, SLA timers started), stores the files and mails the confirmation.</summary>
    Task<PortalTicketResponse> SubmitAsync(Guid customerId, PortalSubmitTicketRequest request, CancellationToken cancellationToken);

    /// <summary>The active categories, by name.</summary>
    Task<IReadOnlyList<PortalCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken);
}

public sealed class PortalTicketService(
    ITicketService tickets,
    ITicketAttachmentRepository attachments,
    IFileStorage storage,
    IChannelSender sender,
    ICustomerRepository customers,
    ITicketCategoryRepository categories,
    TimeProvider timeProvider,
    IValidator<PortalSubmitTicketRequest> validator) : IPortalTicketService
{
    public async Task<PortalTicketResponse> SubmitAsync(
        Guid customerId, PortalSubmitTicketRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);
        var customer = await customers.FindAsync(customerId, cancellationToken) ?? throw new NotFoundException(PortalText.CustomerNotFound);

        var ticket = await tickets.CreateForCustomerAsync(
            customerId,
            new CreateTicketRequest(customerId, request.Subject, request.Description, request.CategoryId, null),
            TicketChannel.Portal,
            cancellationToken);

        var stored = await StoreFilesAsync(ticket.Id, request.Files, cancellationToken);
        await SendConfirmationAsync(customer.Email, ticket, cancellationToken);

        return new PortalTicketResponse(
            ticket.Id, ticket.Number, ticket.Subject, ticket.Status, ticket.CreatedAt,
            [.. stored.Select(a => new PortalAttachmentResponse(a.Id, a.FileName, a.ContentType, a.Size))]);
    }

    public async Task<IReadOnlyList<PortalCategoryResponse>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        [.. (await categories.ListAsync(activeOnly: true, cancellationToken)).Select(c => new PortalCategoryResponse(c.Id, c.Name))];

    private async Task<IReadOnlyList<TicketAttachment>> StoreFilesAsync(
        Guid ticketId, IReadOnlyList<UploadAttachmentRequest> files, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var created = new List<TicketAttachment>();
        try
        {
            foreach (var file in files)
            {
                AttachmentRules.TryGetContentType(file.FileName, out var contentType);
                var attachment = TicketAttachment.Create(ticketId, AttachmentRules.SafeFileName(file.FileName!), contentType, file.Length, now);
                await storage.SaveAsync(attachment.StorageKey, file.Content!, cancellationToken);
                created.Add(attachment);
                attachments.Add(attachment);
            }

            if (created.Count > 0)
            {
                await attachments.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            // No row points at the files: remove them again (best effort), then report the original failure.
            foreach (var attachment in created)
            {
                await storage.DeleteAsync(attachment.StorageKey, CancellationToken.None);
            }

            throw;
        }

        return created;
    }

    /// <summary>The confirmation carries the "[TKT-n]" tag so the customer's email reply lands on the ticket. A send failure never fails the submit (the channel layer logs and retries it).</summary>
    private async Task SendConfirmationAsync(string? email, TicketResponse ticket, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        // The display number may carry a custom prefix (CRM-35, "ACME-000007"): the tag uses the sequence part.
        var digits = new string([.. ticket.Number.Reverse().TakeWhile(char.IsAsciiDigit).Reverse()]);
        var number = int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        await sender.SendAsync(
            new ChannelReply(
                ChannelKind.Email, email, TicketNumberTag.AppendTo(PortalText.ConfirmationSubject, number),
                PortalText.ConfirmationBody(ticket.Number, ticket.Subject), null, ticket.Id),
            cancellationToken);
    }
}
