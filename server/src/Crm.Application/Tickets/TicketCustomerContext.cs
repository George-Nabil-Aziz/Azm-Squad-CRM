using Crm.Application.Common.Exceptions;
using Crm.Application.Customers;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>One of the customer's tickets in the panel; <c>IsCurrent</c> marks the ticket the panel is shown on.</summary>
public sealed record CustomerTicketSummary(
    Guid Id, string Number, string Subject, string Status, string Priority, DateTime CreatedAt, bool IsCurrent);

/// <summary>
/// The customer context of a ticket (CRM-30): the ticket's current customer with every contact, how many tickets the
/// customer has in total and the last five (newest first). <c>CustomerDeleted</c> is true for a soft-deleted customer.
/// </summary>
public sealed record TicketCustomerContextResponse(
    CustomerResponse Customer, bool CustomerDeleted, int TotalTickets, IReadOnlyList<CustomerTicketSummary> RecentTickets);

/// <summary>What the repository reads: the customer (deleted ones included), the ticket count and the newest tickets.</summary>
public sealed record CustomerContextData(Customer Customer, bool CustomerDeleted, int TotalTickets, IReadOnlyList<Ticket> RecentTickets)
{
    /// <summary>How many tickets the panel lists.</summary>
    public const int RecentCount = 5;
}

/// <summary>Storage of the customer context (implemented in Crm.Infrastructure with EF Core).</summary>
public interface ICustomerContextRepository
{
    /// <summary>The context of the ticket's current customer, or null for an unknown ticket.</summary>
    Task<CustomerContextData?> FindAsync(Guid ticketId, CancellationToken cancellationToken);
}

/// <summary>The customer panel of the ticket page. <c>NotFoundException</c> 404 for an unknown ticket.</summary>
public interface ITicketCustomerContextService
{
    Task<TicketCustomerContextResponse> GetAsync(Guid ticketId, CancellationToken cancellationToken);
}

public sealed class TicketCustomerContextService(ICustomerContextRepository repository) : ITicketCustomerContextService
{
    public async Task<TicketCustomerContextResponse> GetAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var data = await repository.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        return new TicketCustomerContextResponse(
            CustomerService.ToResponse(data.Customer),
            data.CustomerDeleted,
            data.TotalTickets,
            [.. data.RecentTickets.Select(t => new CustomerTicketSummary(
                t.Id, t.DisplayNumber, t.Subject, TicketValues.StatusName(t.Status), TicketValues.PriorityName(t.Priority),
                t.CreatedAt, t.Id == ticketId))]);
    }
}
