using Crm.Application.Common.Paging;
using Crm.Application.Customers.Notes;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Customers;

/// <summary>EF Core storage of customer notes; the author's name comes from the Identity users table.</summary>
public sealed class CustomerNoteRepository(CrmDbContext db) : ICustomerNoteRepository
{
    public void Add(CustomerNote note) => db.CustomerNotes.Add(note);

    public Task<CustomerNoteResponse?> GetAsync(Guid noteId, CancellationToken cancellationToken) =>
        Project(db.CustomerNotes.AsNoTracking().Where(n => n.Id == noteId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<CustomerNoteResponse>> ListAsync(
        Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var notes = db.CustomerNotes.AsNoTracking().Where(n => n.CustomerId == customerId);
        var totalCount = await notes.CountAsync(cancellationToken);
        var items = await Project(notes
                .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerNoteResponse>(items, page, pageSize, totalCount);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private IQueryable<CustomerNoteResponse> Project(IQueryable<CustomerNote> notes) =>
        notes.Select(n => new CustomerNoteResponse(
            n.Id,
            n.Text,
            n.AuthorId,
            db.Users.Where(u => u.Id == n.AuthorId).Select(u => u.FullName).FirstOrDefault(),
            n.CreatedAt));
}
