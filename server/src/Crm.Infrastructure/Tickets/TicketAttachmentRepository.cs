using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tickets;

/// <summary>EF Core storage of ticket attachment metadata.</summary>
public sealed class TicketAttachmentRepository(CrmDbContext db) : ITicketAttachmentRepository
{
    public void Add(TicketAttachment attachment) => db.TicketAttachments.Add(attachment);

    public async Task<IReadOnlyList<TicketAttachment>> ListAsync(Guid ticketId, CancellationToken cancellationToken) =>
        await db.TicketAttachments.AsNoTracking().Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.UploadedAt).ThenBy(a => a.FileName).ToListAsync(cancellationToken);

    public Task<TicketAttachment?> FindAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken) =>
        db.TicketAttachments.AsNoTracking().FirstOrDefaultAsync(a => a.TicketId == ticketId && a.Id == attachmentId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
