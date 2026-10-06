using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tickets;

/// <summary>EF Core storage of ticket messages; the author name comes from the Identity users table.</summary>
public sealed class TicketMessageRepository(CrmDbContext db) : ITicketMessageRepository
{
    public void Add(TicketMessage message) => db.TicketMessages.Add(message);

    public async Task<TicketMessageResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await Rows(db.TicketMessages.AsNoTracking().Where(m => m.Id == id)).FirstOrDefaultAsync(cancellationToken))?.ToResponse();

    public async Task<IReadOnlyList<TicketMessageResponse>> ListAsync(
        Guid ticketId, bool includeInternal, CancellationToken cancellationToken)
    {
        var messages = db.TicketMessages.AsNoTracking().Where(m => m.TicketId == ticketId);
        if (!includeInternal)
        {
            messages = messages.Where(m => m.Direction != MessageDirection.InternalNote);
        }

        var rows = await Rows(messages.OrderBy(m => m.CreatedAt)).ToListAsync(cancellationToken);
        return [.. rows.Select(r => r.ToResponse())];
    }

    private IQueryable<MessageRow> Rows(IQueryable<TicketMessage> messages) =>
        messages.Select(m => new MessageRow
        {
            Message = m,
            AuthorName = db.Users.Where(u => u.Id == m.AuthorId).Select(u => u.FullName).FirstOrDefault(),
        });

    private sealed class MessageRow
    {
        public required TicketMessage Message { get; init; }

        public string? AuthorName { get; init; }

        public TicketMessageResponse ToResponse() => TicketMessageService.ToResponse(Message, AuthorName);
    }
}
