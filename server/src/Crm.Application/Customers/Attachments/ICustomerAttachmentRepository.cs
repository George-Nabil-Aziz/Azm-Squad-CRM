using Crm.Domain.Customers;

namespace Crm.Application.Customers.Attachments;

/// <summary>Attachment metadata storage (implemented in Crm.Infrastructure with EF Core). The bytes are in <c>IFileStorage</c>.</summary>
public interface ICustomerAttachmentRepository
{
    void Add(CustomerAttachment attachment);

    /// <summary>The attachment when it belongs to the customer, otherwise null. Not tracked.</summary>
    Task<CustomerAttachment?> FindAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>The saved attachment with its uploader's name, or null.</summary>
    Task<CustomerAttachmentResponse?> GetAsync(Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>Every attachment of the customer, newest first.</summary>
    Task<IReadOnlyList<CustomerAttachmentResponse>> ListAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Saves the unit of work (the attachment row and its timeline entry together).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
