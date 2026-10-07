using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.WebForms;

internal sealed class FakeTicketService : ITicketService
{
    public List<(Guid CustomerId, CreateTicketRequest Request, TicketChannel Channel)> Created { get; } = [];

    public Task<TicketResponse> CreateForCustomerAsync(
        Guid customerId, CreateTicketRequest request, TicketChannel channel, CancellationToken cancellationToken)
    {
        Created.Add((customerId, request, channel));
        var number = Created.Count;
        var now = DateTime.UtcNow;
        return Task.FromResult(new TicketResponse(
            Guid.NewGuid(), $"TKT-{number:D6}", request.Subject!, request.Description, "new", "mid", "webform",
            customerId, "customer", null, null, null, null, now, now));
    }

    public Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<PagedResult<TicketResponse>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<TicketAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<TicketResponse> ChangePriorityAsync(Guid id, ChangeTicketPriorityRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}

