using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tickets;

/// <summary>
/// EF Core storage of tickets. Ticket views read the customer <b>without</b> the soft-delete filter, so tickets of a
/// deleted customer keep showing it (CRM-8: "its tickets remain").
/// </summary>
public sealed class TicketRepository(CrmDbContext db) : ITicketRepository
{
    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken);

    public async Task<int> NextNumberAsync(CancellationToken cancellationToken) =>
        (await db.Tickets.MaxAsync(t => (int?)t.Number, cancellationToken) ?? 0) + 1;

    public void Add(Ticket ticket) => db.Tickets.Add(ticket);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another API instance saved a ticket with the same number first (unique index IX_Tickets_Number).
            if (await NumberTakenAsync(cancellationToken))
            {
                throw new ConflictException(TicketText.NumberTaken);
            }

            throw;
        }
    }

    public async Task<TicketView?> GetViewAsync(Guid id, CancellationToken cancellationToken) =>
        (await Rows().FirstOrDefaultAsync(r => r.Ticket.Id == id, cancellationToken))?.ToView();

    /// <summary>
    /// Tickets with customer (deleted ones included), category and assignee names; not tracked. A member-init
    /// projection, so callers can still filter, order and page on it before it is read.
    /// </summary>
    internal IQueryable<TicketRow> Rows() =>
        from ticket in db.Tickets.AsNoTracking()
        join customer in db.Customers.IgnoreQueryFilters([CrmDbContext.SoftDeleteFilter])
            on ticket.CustomerId equals customer.Id
        join category in db.TicketCategories on ticket.CategoryId equals category.Id into categories
        from category in categories.DefaultIfEmpty()
        join assignee in db.Users on ticket.AssigneeId equals assignee.Id into assignees
        from assignee in assignees.DefaultIfEmpty()
        select new TicketRow
        {
            Ticket = ticket,
            CustomerName = customer.Name,
            CategoryName = category != null ? category.Name : null,
            AssigneeName = assignee != null ? assignee.FullName : null,
        };

    private async Task<bool> NumberTakenAsync(CancellationToken cancellationToken)
    {
        var added = db.ChangeTracker.Entries<Ticket>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        foreach (var ticket in added)
        {
            if (await db.Tickets.AsNoTracking().AnyAsync(t => t.Number == ticket.Number && t.Id != ticket.Id, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    internal sealed class TicketRow
    {
        public required Ticket Ticket { get; init; }

        public required string CustomerName { get; init; }

        public string? CategoryName { get; init; }

        public string? AssigneeName { get; init; }

        public TicketView ToView() => new(Ticket, CustomerName, CategoryName, AssigneeName);
    }
}
