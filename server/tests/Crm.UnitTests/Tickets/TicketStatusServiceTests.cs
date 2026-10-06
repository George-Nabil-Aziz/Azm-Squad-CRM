using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketStatusServiceTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeTicketHistoryRecorder _history = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketStatusService _service;
    private readonly Ticket _ticket;

    public TicketStatusServiceTests()
    {
        _service = new TicketStatusService(_tickets, _history, _clock);
        var customerId = _tickets.AddCustomer("Nour Trading");
        _ticket = Ticket.Create(customerId, "Invoice is wrong", null, null, TicketPriority.High, TicketChannel.Manual, AgentId, _clock.UtcNow.UtcDateTime);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
    }

    private Task<TicketResponse> MoveAsync(string? status) =>
        _service.ChangeAsync(_ticket.Id, new ChangeTicketStatusRequest(status), CancellationToken.None);

    private async Task MoveThroughAsync(params string[] statuses)
    {
        foreach (var status in statuses)
        {
            await MoveAsync(status);
        }
    }

    [Fact]
    public async Task ValidTransition_Succeeds_AndRecordsOneHistoryEntry()
    {
        await MoveAsync("open");
        _history.Entries.Clear();

        var response = await MoveAsync("pending");

        Assert.Equal("pending", response.Status);
        Assert.Equal(TicketStatus.Pending, _ticket.Status);
        var entry = Assert.Single(_history.Entries);
        Assert.Equal((_ticket.Id, TicketHistoryField.Status, "open", "pending", _clock.UtcNow.UtcDateTime),
            (entry.TicketId, entry.Field, entry.OldValue, entry.NewValue, entry.UtcNow));
    }

    [Fact]
    public async Task ClosedToPending_ThrowsValidationException_OnStatus_AndRecordsNothing()
    {
        await MoveThroughAsync("open", "resolved", "closed");
        _history.Entries.Clear();
        var saves = _tickets.SaveCount;

        var error = await Assert.ThrowsAsync<ValidationException>(() => MoveAsync("pending"));

        Assert.Contains("status", error.Errors.Keys);
        Assert.Equal(TicketStatus.Closed, _ticket.Status);
        Assert.Empty(_history.Entries);
        Assert.Equal(saves, _tickets.SaveCount);
    }

    [Fact]
    public async Task MovingToResolved_SetsResolvedAt()
    {
        await MoveAsync("open");
        _clock.UtcNow = _clock.UtcNow.AddHours(2);

        var response = await MoveAsync("resolved");

        Assert.Equal(_clock.UtcNow.UtcDateTime, response.ResolvedAt);
        Assert.Equal(_clock.UtcNow.UtcDateTime, _ticket.ResolvedAt);
    }

    [Fact]
    public async Task ReopeningAResolvedTicket_ClearsResolvedAt()
    {
        await MoveThroughAsync("open", "resolved");

        var response = await MoveAsync("open");

        Assert.Equal("open", response.Status);
        Assert.Null(response.ResolvedAt);
        Assert.Null(_ticket.ResolvedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("done")]
    [InlineData("1")]
    public async Task UnknownStatus_ThrowsValidationException_OnStatus(string? status)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => MoveAsync(status));

        Assert.Contains("status", error.Errors.Keys);
    }

    [Fact]
    public async Task MovingToTheCurrentStatus_ThrowsValidationException()
    {
        await MoveAsync("open");

        await Assert.ThrowsAsync<ValidationException>(() => MoveAsync("open"));
    }

    [Fact]
    public async Task Response_ListsTheAllowedNextStatuses()
    {
        var response = await MoveAsync("open");

        Assert.Equal(["pending", "resolved"], response.AllowedStatuses);
    }

    [Fact]
    public async Task UnknownTicket_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ChangeAsync(Guid.NewGuid(), new ChangeTicketStatusRequest("open"), CancellationToken.None));
}
