using Crm.Application.Customers.Attachments;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Customers;

/// <summary>EF Core storage of attachment metadata; the uploader's name comes from the Identity users table.</summary>
public sealed class CustomerAttachmentRepository(CrmDbContext db) : ICustomerAttachmentRepository
{
    public void Add(CustomerAttachment attachment) => db.CustomerAttachments.Add(attachment);

    public Task<CustomerAttachment?> FindAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken) =>
        db.CustomerAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Id == attachmentId, cancellationToken);

    public Task<CustomerAttachmentResponse?> GetAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        Project(db.CustomerAttachments.AsNoTracking().Where(a => a.Id == attachmentId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerAttachmentResponse>> ListAsync(Guid customerId, CancellationToken cancellationToken) =>
        await Project(db.CustomerAttachments.AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.UploadedAt).ThenBy(a => a.FileName))
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private IQueryable<CustomerAttachmentResponse> Project(IQueryable<CustomerAttachment> attachments) =>
        attachments.Select(a => new CustomerAttachmentResponse(
            a.Id,
            a.FileName,
            a.ContentType,
            a.Size,
            a.UploadedById,
            db.Users.Where(u => u.Id == a.UploadedById).Select(u => u.FullName).FirstOrDefault(),
            a.UploadedAt));
}
