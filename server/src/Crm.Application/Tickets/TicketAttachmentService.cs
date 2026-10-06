using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Files;
using Crm.Application.Customers.Attachments;
using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>A file of a ticket: name, server-chosen content type, size in bytes, UTC time.</summary>
public sealed record TicketAttachmentResponse(Guid Id, string FileName, string ContentType, long Size, DateTime UploadedAt);

/// <summary>Ticket attachment storage (EF Core in Crm.Infrastructure).</summary>
public interface ITicketAttachmentRepository
{
    void Add(TicketAttachment attachment);

    /// <summary>The ticket's files, oldest first.</summary>
    Task<IReadOnlyList<TicketAttachment>> ListAsync(Guid ticketId, CancellationToken cancellationToken);

    Task<TicketAttachment?> FindAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Staff access to the files customers attached to a ticket (<c>tickets.view</c>). Failures: <c>NotFoundException</c> 404
/// (unknown ticket, attachment or missing file).
/// </summary>
public interface ITicketAttachmentService
{
    Task<IReadOnlyList<TicketAttachmentResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken);

    Task<AttachmentDownload> DownloadAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken);
}

public sealed class TicketAttachmentService(
    ITicketRepository tickets, ITicketAttachmentRepository attachments, IFileStorage storage) : ITicketAttachmentService
{
    public async Task<IReadOnlyList<TicketAttachmentResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        await EnsureTicketAsync(ticketId, cancellationToken);
        return [.. (await attachments.ListAsync(ticketId, cancellationToken)).Select(ToResponse)];
    }

    public async Task<AttachmentDownload> DownloadAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken)
    {
        await EnsureTicketAsync(ticketId, cancellationToken);
        var attachment = await attachments.FindAsync(ticketId, attachmentId, cancellationToken)
                         ?? throw new NotFoundException(TicketText.AttachmentNotFound);
        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken)
                      ?? throw new NotFoundException(TicketText.AttachmentNotFound);
        return new AttachmentDownload(content, attachment.ContentType, attachment.FileName);
    }

    public static TicketAttachmentResponse ToResponse(TicketAttachment attachment) =>
        new(attachment.Id, attachment.FileName, attachment.ContentType, attachment.Size, attachment.UploadedAt);

    private async Task EnsureTicketAsync(Guid ticketId, CancellationToken cancellationToken) =>
        _ = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
}
