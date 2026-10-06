using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketHistoryServiceTests
{
    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeTicketHistoryRepository _history = new();
    private readonly TicketHistoryService _service;
    private readonly Ticket _ticket;

    public TicketHistoryServiceTests()
    {
        _service = new TicketHistoryService(_tickets, _history);
        var now = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        _ticket = Ticket.Create(_tickets.AddCustomer("Nour"), "Invoice is wrong", null, null, TicketPriority.High, TicketChannel.Manual, null, now);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
    }

    [Fact]
    public async Task List_ReturnsTheEntriesOldestFirst_WithFieldValuesUserAndTime()
    {
        var userId = Guid.NewGuid();
        var t1 = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        _history.Items.Add(new TicketHistoryItemResponse("h2", "assignee", null, "Sara", userId, "Team Lead", t1.AddHours(1)));
        _history.Items.Add(new TicketHistoryItemResponse("h1", "status", "new", "open", userId, "Team Lead", t1));

        var items = await _service.ListAsync(_ticket.Id, CancellationToken.None);

        Assert.Equal(["status", "assignee"], items.Select(i => i.Field));
        Assert.Equal(("new", "open", userId, "Team Lead", t1), (items[0].OldValue, items[0].NewValue, items[0].ChangedById, items[0].ChangedByName, items[0].ChangedAt));
    }

    [Fact]
    public async Task List_OfATicketWithoutChanges_IsEmpty() =>
        Assert.Empty(await _service.ListAsync(_ticket.Id, CancellationToken.None));

    [Fact]
    public async Task List_OfAnUnknownTicket_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(Guid.NewGuid(), CancellationToken.None));
}
