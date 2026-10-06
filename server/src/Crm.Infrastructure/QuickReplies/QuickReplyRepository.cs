using Crm.Application.QuickReplies;
using Crm.Domain.QuickReplies;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.QuickReplies;

/// <summary>EF Core storage of quick replies (CRM-32).</summary>
public sealed class QuickReplyRepository(CrmDbContext db) : IQuickReplyRepository
{
    public void Add(QuickReply reply) => db.QuickReplies.Add(reply);

    public void Remove(QuickReply reply) => db.QuickReplies.Remove(reply);

    public Task<QuickReply?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.QuickReplies.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<QuickReplyResponse>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken)
    {
        var rows = Rows().Where(r => r.Reply.IsShared || r.Reply.OwnerId == userId);
        if (search is not null)
        {
            // LIKE is case-insensitive on SQL Server and SQLite (ASCII); wildcards are escaped.
            var pattern = LikePattern.Contains(search);
            rows = rows.Where(r => EF.Functions.Like(r.Reply.Title, pattern, LikePattern.EscapeCharacter)
                                   || EF.Functions.Like(r.Reply.Shortcut!, pattern, LikePattern.EscapeCharacter));
        }

        return [.. (await rows.OrderBy(r => r.Reply.Title).ThenBy(r => r.Reply.Id).ToListAsync(cancellationToken))
            .Select(r => QuickReplyService.ToResponse(r.Reply, userId, r.OwnerName))];
    }

    public async Task<QuickReplyResponse?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        (await Rows().Where(r => r.Reply.Id == id).ToListAsync(cancellationToken))
            .Select(r => QuickReplyService.ToResponse(r.Reply, userId, r.OwnerName)).FirstOrDefault();

    public async Task<QuickReplyValues?> FindTicketValuesAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var row = await (from ticket in db.Tickets.AsNoTracking()
                         join customer in db.Customers.IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter]) on ticket.CustomerId equals customer.Id
                         where ticket.Id == ticketId
                         select new { ticket.Number, ticket.Subject, CustomerName = customer.Name })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new QuickReplyValues(row.CustomerName, Ticket.FormatNumber(row.Number), row.Subject, string.Empty);
    }

    public Task<string?> GetUserNameAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => (string?)u.FullName).FirstOrDefaultAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private IQueryable<Row> Rows() =>
        from reply in db.QuickReplies.AsNoTracking()
        join owner in db.Users on reply.OwnerId equals owner.Id into owners
        from owner in owners.DefaultIfEmpty()
        select new Row { Reply = reply, OwnerName = owner != null ? owner.FullName : null };

    private sealed class Row
    {
        public required QuickReply Reply { get; init; }

        public string? OwnerName { get; init; }
    }
}
