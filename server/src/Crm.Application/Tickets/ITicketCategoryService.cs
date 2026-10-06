namespace Crm.Application.Tickets;

/// <summary>
/// Ticket categories (reads need <c>tickets.view</c>, writes <c>categories.manage</c> — enforced by the API).
/// Failures: <c>ValidationException</c> 400 (missing or duplicate name), <c>NotFoundException</c> 404.
/// </summary>
public interface ITicketCategoryService
{
    /// <summary>Categories ordered by name; with <c>ActiveOnly</c> only those new tickets may use.</summary>
    Task<IReadOnlyList<TicketCategoryResponse>> ListAsync(ListTicketCategoriesQuery query, CancellationToken cancellationToken);

    Task<TicketCategoryResponse> CreateAsync(TicketCategoryRequest request, CancellationToken cancellationToken);

    Task<TicketCategoryResponse> UpdateAsync(Guid id, TicketCategoryRequest request, CancellationToken cancellationToken);
}
