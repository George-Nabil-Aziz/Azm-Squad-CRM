using Crm.Application.Common.Paging;
using Crm.Domain.Customers;

namespace Crm.Application.Customers.Notes;

/// <summary>Note storage (implemented in Crm.Infrastructure with EF Core); author names come from the user accounts.</summary>
public interface ICustomerNoteRepository
{
    void Add(CustomerNote note);

    /// <summary>The saved note with its author's name, or null.</summary>
    Task<CustomerNoteResponse?> GetAsync(Guid noteId, CancellationToken cancellationToken);

    /// <summary>One page of the customer's notes, newest first.</summary>
    Task<PagedResult<CustomerNoteResponse>> ListAsync(Guid customerId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Saves the unit of work (the note and its timeline entry together).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
