using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Files;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;
using FluentValidation;

namespace Crm.Application.Customers.Attachments;

public sealed class CustomerAttachmentService(
    ICustomerRepository customers,
    ICustomerAttachmentRepository attachments,
    IFileStorage storage,
    IInteractionRecorder timeline,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<UploadAttachmentRequest> uploadValidator) : ICustomerAttachmentService
{
    public async Task<IReadOnlyList<CustomerAttachmentResponse>> ListAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await EnsureCustomerAsync(customerId, cancellationToken);
        return await attachments.ListAsync(customerId, cancellationToken);
    }

    public async Task<CustomerAttachmentResponse> UploadAsync(
        Guid customerId, UploadAttachmentRequest request, CancellationToken cancellationToken)
    {
        await uploadValidator.ValidateOrThrowAsync(request, cancellationToken);
        await EnsureCustomerAsync(customerId, cancellationToken);

        AttachmentRules.TryGetContentType(request.FileName, out var contentType);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var attachment = CustomerAttachment.Create(
            customerId, AttachmentRules.SafeFileName(request.FileName!), contentType, request.Length, currentUser.UserId, now);

        await storage.SaveAsync(attachment.StorageKey, request.Content!, cancellationToken);
        try
        {
            attachments.Add(attachment);
            timeline.Record(customerId, InteractionType.Attachment, InteractionEvents.AttachmentAdded, attachment.FileName,
                attachment.Id, now);
            await attachments.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // No row points at the file: remove it again (best effort), then report the original failure.
            await storage.DeleteAsync(attachment.StorageKey, CancellationToken.None);
            throw;
        }

        return await attachments.GetAsync(attachment.Id, cancellationToken)
               ?? throw new InvalidOperationException("The saved attachment was not found.");
    }

    public async Task<AttachmentDownload> DownloadAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken)
    {
        await EnsureCustomerAsync(customerId, cancellationToken);
        var attachment = await attachments.FindAsync(customerId, attachmentId, cancellationToken)
                         ?? throw new NotFoundException(CustomerText.AttachmentNotFound);
        var content = await storage.OpenReadAsync(attachment.StorageKey, cancellationToken)
                      ?? throw new NotFoundException(CustomerText.AttachmentNotFound);

        return new AttachmentDownload(content, attachment.ContentType, attachment.FileName);
    }

    private async Task EnsureCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        _ = await customers.FindAsync(customerId, cancellationToken) ?? throw new NotFoundException(CustomerText.NotFound);
}
