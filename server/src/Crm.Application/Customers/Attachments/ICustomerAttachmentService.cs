namespace Crm.Application.Customers.Attachments;

/// <summary>
/// Files attached to a customer (list and download need <c>customers.view</c>, upload <c>customers.manage</c> —
/// enforced by the API). Failures: <c>ValidationException</c> 400 on "file" (missing, empty, over 10 MB, type not
/// allowed), <c>NotFoundException</c> 404 (unknown or deleted customer, unknown attachment, file missing).
/// </summary>
public interface ICustomerAttachmentService
{
    Task<IReadOnlyList<CustomerAttachmentResponse>> ListAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Stores the file, saves its metadata and an "attachmentAdded" timeline entry.</summary>
    Task<CustomerAttachmentResponse> UploadAsync(Guid customerId, UploadAttachmentRequest request, CancellationToken cancellationToken);

    Task<AttachmentDownload> DownloadAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken);
}
