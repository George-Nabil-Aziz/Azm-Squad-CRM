using Crm.Application.Tickets;
using Crm.UnitTests.Sla;

namespace Crm.UnitTests.Tickets;

/// <summary>CRM-62: a new ticket takes the branch of its customer.</summary>
public class TicketServiceBranchTests
{
    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketService _service;

    public TicketServiceBranchTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _service = new TicketService(_tickets, _categories, new FakeInteractionRecorder(), new FakeCurrentUser(Guid.NewGuid()), _clock,
            new CreateTicketRequestValidator(), new ListTicketsQueryValidator(), new FakeSlaPolicyRepository(_clock.UtcNow.UtcDateTime),
            new FakeTicketHistoryRecorder(), new Crm.UnitTests.Settings.FakeSystemSettingsProvider());
    }

    [Fact]
    public async Task Create_ForACustomerOfABranch_StampsTheTicketWithThatBranch()
    {
        var branchId = Guid.NewGuid();
        var customerId = _tickets.AddCustomer("Nour");
        _tickets.CustomerBranches[customerId] = branchId;

        var response = await _service.CreateAsync(new CreateTicketRequest(customerId, "Subject", null, null, "mid"), CancellationToken.None);

        Assert.Equal(branchId, Assert.Single(_tickets.Tickets).BranchId);
        Assert.Equal(branchId, response.BranchId);
    }

    [Fact]
    public async Task Create_ForACustomerWithoutABranch_LeavesTheTicketWithoutOne()
    {
        var customerId = _tickets.AddCustomer("Nour");

        var response = await _service.CreateAsync(new CreateTicketRequest(customerId, "Subject", null, null, "mid"), CancellationToken.None);

        Assert.Null(response.BranchId);
    }
}
