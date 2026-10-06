using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

internal sealed class FakeCustomerContextRepository : ICustomerContextRepository
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    public Dictionary<Guid, Customer> Customers { get; } = [];

    public List<Ticket> Tickets { get; } = [];

    public HashSet<Guid> Deleted { get; } = [];

    public Task<CustomerContextData?> FindAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket is null)
        {
            return Task.FromResult<CustomerContextData?>(null);
        }

        var all = Tickets.Where(t => t.CustomerId == ticket.CustomerId).OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Number).ToList();
        return Task.FromResult<CustomerContextData?>(new CustomerContextData(
            Customers[ticket.CustomerId], Deleted.Contains(ticket.CustomerId), all.Count, [.. all.Take(CustomerContextData.RecentCount)]));
    }

    public Customer AddCustomer(string name, string? email = null, string? phone = null)
    {
        var customer = Customer.Create(name, email, phone, Created);
        Customers[customer.Id] = customer;
        return customer;
    }

    public Ticket AddTicket(Customer customer, string subject, int number)
    {
        var ticket = Ticket.Create(customer.Id, subject, null, null, TicketPriority.Mid, TicketChannel.Manual, null, Created.AddMinutes(number));
        ticket.AssignNumber(number);
        Tickets.Add(ticket);
        return ticket;
    }
}

public class TicketCustomerContextServiceTests
{
    private readonly FakeCustomerContextRepository _repository = new();
    private readonly TicketCustomerContextService _service;

    public TicketCustomerContextServiceTests() => _service = new TicketCustomerContextService(_repository);

    [Fact]
    public async Task Returns_TheCustomer_TheTotalAndTheLastFiveTickets()
    {
        var customer = _repository.AddCustomer("Nour Trading", "info@nour.example", "+966501234567");
        var tickets = Enumerable.Range(1, 7).Select(n => _repository.AddTicket(customer, $"Ticket {n}", n)).ToList();
        TicketTestSupport.SetStatus(tickets[5], TicketStatus.Pending);

        var context = await _service.GetAsync(tickets[2].Id, CancellationToken.None);

        Assert.Equal("Nour Trading", context.Customer.Name);
        Assert.Equal("info@nour.example", context.Customer.Email);
        Assert.Equal(2, context.Customer.Contacts.Count);
        Assert.Equal(7, context.TotalTickets); // AC 1
        Assert.Equal(["TKT-000007", "TKT-000006", "TKT-000005", "TKT-000004", "TKT-000003"], context.RecentTickets.Select(t => t.Number)); // AC 2
        Assert.Equal("pending", context.RecentTickets[1].Status);
        Assert.Equal("new", context.RecentTickets[0].Status);
        Assert.Equal(tickets[2].Id, Assert.Single(context.RecentTickets, t => t.IsCurrent).Id);
        Assert.False(context.CustomerDeleted);
    }

    [Fact]
    public async Task FollowsTheTicketsCurrentCustomer()
    {
        var first = _repository.AddCustomer("First");
        var second = _repository.AddCustomer("Second");
        var ticket = _repository.AddTicket(first, "Printer", 1);
        _repository.AddTicket(second, "Other", 2);

        Assert.Equal("First", (await _service.GetAsync(ticket.Id, CancellationToken.None)).Customer.Name);

        typeof(Ticket).GetProperty(nameof(Ticket.CustomerId))!.SetValue(ticket, second.Id); // AC 4: the ticket moved to another customer

        var after = await _service.GetAsync(ticket.Id, CancellationToken.None);
        Assert.Equal(("Second", 2), (after.Customer.Name, after.TotalTickets));
    }

    [Fact]
    public async Task UnknownTicket_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task DeletedCustomer_IsFlagged()
    {
        var customer = _repository.AddCustomer("Gone");
        var ticket = _repository.AddTicket(customer, "Old", 1);
        _repository.Deleted.Add(customer.Id);

        Assert.True((await _service.GetAsync(ticket.Id, CancellationToken.None)).CustomerDeleted);
    }
}
