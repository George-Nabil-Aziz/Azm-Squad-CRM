using Crm.Application.Common.Exceptions;
using Crm.Application.Portal;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Portal;

public class TicketReopenWindowTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromDays(7);

    private static Ticket Resolved(DateTime at)
    {
        var ticket = Ticket.Create(Guid.NewGuid(), "Printer", null, null, TicketPriority.Mid, TicketChannel.Portal, null, Now);
        ticket.ChangeStatus(TicketStatus.Open, Now);
        ticket.ChangeStatus(TicketStatus.Resolved, at);
        return ticket;
    }

    [Fact]
    public void AResolvedTicket_CanBeReopened_InsideTheWindow_IncludingExactlyAtItsEnd()
    {
        var ticket = Resolved(Now);

        Assert.True(ticket.CanBeReopenedByCustomer(Now.AddDays(3), Window));
        Assert.True(ticket.CanBeReopenedByCustomer(Now.AddDays(7), Window));
    }

    [Fact]
    public void AResolvedTicket_CannotBeReopened_AfterTheWindow() =>
        Assert.False(Resolved(Now).CanBeReopenedByCustomer(Now.AddDays(7).AddTicks(1), Window));

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.Pending)]
    [InlineData(TicketStatus.Closed)]
    public void OnlyAResolvedTicket_CanBeReopenedByTheCustomer(TicketStatus status)
    {
        var ticket = Resolved(Now);
        TicketTestSupport.SetStatus(ticket, status);

        Assert.False(ticket.CanBeReopenedByCustomer(Now.AddHours(1), Window));
    }

    [Fact]
    public void ACustomerCanReplyOnNewOpenAndPendingTickets_ButNotOnResolvedOrClosed()
    {
        var ticket = Resolved(Now);
        foreach (var (status, expected) in new[]
                 {
                     (TicketStatus.New, true), (TicketStatus.Open, true), (TicketStatus.Pending, true),
                     (TicketStatus.Resolved, false), (TicketStatus.Closed, false),
                 })
        {
            TicketTestSupport.SetStatus(ticket, status);
            Assert.Equal(expected, ticket.AcceptsCustomerReply);
        }
    }
}

public class PortalTicketTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeTicketMessageRepository _messages = new();
    private readonly FakeTicketHistoryRepository _historyItems = new();
    private readonly FakeTicketHistoryRecorder _recorder = new();
    private readonly Crm.UnitTests.Tickets.FakeInteractionRecorder _timeline = new();
    private readonly TestClock _clock = new(Start);
    private readonly PortalTicketTracker _tracker;
    private readonly Guid _me;
    private readonly Guid _other;

    public PortalTicketTrackerTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _me = _tickets.AddCustomer("Nour Trading");
        _other = _tickets.AddCustomer("Other Co");
        _tracker = new PortalTicketTracker(
            _tickets, _messages, _historyItems, _recorder, _timeline, new PortalOptions { ReopenWindowDays = 7 }, _clock);
    }

    private async Task<Ticket> AddTicketAsync(Guid customerId, string subject = "Printer", TicketStatus status = TicketStatus.New)
    {
        var ticket = Ticket.Create(customerId, subject, "d", null, TicketPriority.Mid, TicketChannel.Portal, null, Start.UtcDateTime);
        ticket.AssignNumber(_tickets.Tickets.Count + 1);
        _tickets.Add(ticket);
        await _tickets.SaveChangesAsync(CancellationToken.None);
        if (status == TicketStatus.Resolved)
        {
            ticket.ChangeStatus(TicketStatus.Open, Start.UtcDateTime);
            ticket.ChangeStatus(TicketStatus.Resolved, Start.UtcDateTime);
        }
        else if (status != TicketStatus.New)
        {
            TicketTestSupport.SetStatus(ticket, status);
        }

        return ticket;
    }

    [Fact]
    public async Task List_ReturnsOnlyTheCustomersOwnTickets_WithoutStaffFields()
    {
        var mine = await AddTicketAsync(_me, "Mine");
        await AddTicketAsync(_other, "Theirs");

        var page = await _tracker.ListAsync(_me, 1, 20, CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(mine.Id, item.Id);
        Assert.Equal("Mine", item.Subject);
        Assert.Equal("new", item.Status);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(_me, _tickets.LastFilter!.CustomerId);
    }

    [Fact]
    public async Task Get_AnotherCustomersTicket_IsNotFound()
    {
        var theirs = await AddTicketAsync(_other);

        await Assert.ThrowsAsync<NotFoundException>(() => _tracker.GetAsync(_me, theirs.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _tracker.GetAsync(_me, Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _tracker.ListMessagesAsync(_me, theirs.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _tracker.ListHistoryAsync(_me, theirs.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _tracker.ReplyAsync(_me, theirs.Id, new PortalReplyRequest("hi"), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _tracker.ReopenAsync(_me, theirs.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Get_ShowsTheStatus_AndWhatTheCustomerMayDo()
    {
        var open = await AddTicketAsync(_me, "Open one", TicketStatus.Open);
        var resolved = await AddTicketAsync(_me, "Resolved one", TicketStatus.Resolved);
        var closed = await AddTicketAsync(_me, "Closed one", TicketStatus.Closed);

        var a = await _tracker.GetAsync(_me, open.Id, CancellationToken.None);
        var b = await _tracker.GetAsync(_me, resolved.Id, CancellationToken.None);
        var c = await _tracker.GetAsync(_me, closed.Id, CancellationToken.None);

        Assert.Equal(("open", true, false), (a.Status, a.CanReply, a.CanReopen));
        Assert.Equal(("resolved", false, true), (b.Status, b.CanReply, b.CanReopen));
        Assert.Equal(("closed", false, false), (c.Status, c.CanReply, c.CanReopen));
    }

    [Fact]
    public async Task Messages_ShowPublicRepliesOnly_NeverInternalNotes()
    {
        var ticket = await AddTicketAsync(_me);
        var agent = Guid.NewGuid();
        _messages.Authors[agent] = "Sara Agent";
        _messages.Add(TicketMessage.Inbound(ticket.Id, "My printer is broken", TicketChannel.Portal, null, Start.UtcDateTime));
        _messages.Add(TicketMessage.Staff(ticket.Id, "INTERNAL: customer is difficult", true, TicketChannel.Portal, agent, Start.UtcDateTime.AddMinutes(1)));
        _messages.Add(TicketMessage.Staff(ticket.Id, "Try turning it off and on", false, TicketChannel.Portal, agent, Start.UtcDateTime.AddMinutes(2)));

        var list = await _tracker.ListMessagesAsync(_me, ticket.Id, CancellationToken.None);

        Assert.Equal(["My printer is broken", "Try turning it off and on"], list.Select(m => m.Body));
        Assert.Equal([true, false], list.Select(m => m.FromCustomer));
        Assert.Equal("Sara Agent", list[1].AuthorName);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.Pending)]
    public async Task Reply_OnAnOpenTicket_AddsAnInboundPortalMessage_AndTouchesTheTicket(TicketStatus status)
    {
        var ticket = await AddTicketAsync(_me, status: status);
        _clock.UtcNow = Start.AddHours(1);

        var reply = await _tracker.ReplyAsync(_me, ticket.Id, new PortalReplyRequest("  Still broken  "), CancellationToken.None);

        var message = Assert.Single(_messages.Messages);
        Assert.Equal(MessageDirection.Inbound, message.Direction);
        Assert.Equal(TicketChannel.Portal, message.Channel);
        Assert.Equal("Still broken", message.Body);
        Assert.Null(message.AuthorId);
        Assert.True(reply.FromCustomer);
        Assert.Equal(Start.AddHours(1).UtcDateTime, ticket.UpdatedAt);
        Assert.Null(ticket.FirstResponseAt); // only agents answer
        Assert.Contains(_timeline.Entries, e => e.CustomerId == _me && e.Event == "messageReceived");
    }

    [Fact]
    public async Task Reply_OnAPendingTicket_ReopensIt_AndRecordsTheStatusChange()
    {
        var ticket = await AddTicketAsync(_me, status: TicketStatus.Pending);

        await _tracker.ReplyAsync(_me, ticket.Id, new PortalReplyRequest("Here is the info"), CancellationToken.None);

        Assert.Equal(TicketStatus.Open, ticket.Status);
        var entry = Assert.Single(_recorder.Entries);
        Assert.Equal((TicketHistoryField.Status, "pending", "open"), (entry.Field, entry.OldValue, entry.NewValue));
    }

    [Fact]
    public async Task Reply_OnAnOpenTicket_KeepsTheStatus()
    {
        var ticket = await AddTicketAsync(_me, status: TicketStatus.Open);

        await _tracker.ReplyAsync(_me, ticket.Id, new PortalReplyRequest("Hello"), CancellationToken.None);

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Empty(_recorder.Entries);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task Reply_OnAResolvedOrClosedTicket_ThrowsValidation_OnStatus(TicketStatus status)
    {
        var ticket = await AddTicketAsync(_me, status: status);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _tracker.ReplyAsync(_me, ticket.Id, new PortalReplyRequest("Hello"), CancellationToken.None));

        Assert.Contains("status", error.Errors.Keys);
        Assert.Empty(_messages.Messages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Reply_WithoutText_ThrowsValidation_OnBody(string? body)
    {
        var ticket = await AddTicketAsync(_me);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => _tracker.ReplyAsync(_me, ticket.Id, new PortalReplyRequest(body), CancellationToken.None));

        Assert.Contains("body", error.Errors.Keys);
    }

    [Fact]
    public async Task Reopen_AResolvedTicket_InsideTheWindow_MakesItOpen_AndRecordsIt()
    {
        var ticket = await AddTicketAsync(_me, status: TicketStatus.Resolved);
        _clock.UtcNow = Start.AddDays(6);

        var result = await _tracker.ReopenAsync(_me, ticket.Id, CancellationToken.None);

        Assert.Equal("open", result.Status);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.ResolvedAt); // the resolution timer runs again
        var entry = Assert.Single(_recorder.Entries);
        Assert.Equal((TicketHistoryField.Status, "resolved", "open"), (entry.Field, entry.OldValue, entry.NewValue));
    }

    [Fact]
    public async Task Reopen_AfterTheWindow_ThrowsValidation_OnStatus()
    {
        var ticket = await AddTicketAsync(_me, status: TicketStatus.Resolved);
        _clock.UtcNow = Start.AddDays(7).AddMinutes(1);

        var error = await Assert.ThrowsAsync<ValidationException>(() => _tracker.ReopenAsync(_me, ticket.Id, CancellationToken.None));

        Assert.Contains("status", error.Errors.Keys);
        Assert.Equal(TicketStatus.Resolved, ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.Closed)]
    public async Task Reopen_ATicketThatIsNotResolved_ThrowsValidation(TicketStatus status)
    {
        var ticket = await AddTicketAsync(_me, status: status);

        await Assert.ThrowsAsync<ValidationException>(() => _tracker.ReopenAsync(_me, ticket.Id, CancellationToken.None));
    }

    [Fact]
    public async Task History_ShowsCreationAndStatusChangesOnly()
    {
        var ticket = await AddTicketAsync(_me);
        _historyItems.Items.Add(new TicketHistoryItemResponse("1", "status", "new", "open", Guid.NewGuid(), "Sara Agent", Start.UtcDateTime.AddHours(1)));
        _historyItems.Items.Add(new TicketHistoryItemResponse("2", "assignee", null, "Sara Agent", Guid.NewGuid(), "Sara Agent", Start.UtcDateTime.AddHours(2)));
        _historyItems.Items.Add(new TicketHistoryItemResponse("3", "priority", "mid", "high", null, null, Start.UtcDateTime.AddHours(3)));
        _historyItems.Items.Add(new TicketHistoryItemResponse("4", "escalation", null, "1", null, null, Start.UtcDateTime.AddHours(4)));
        _historyItems.Items.Add(new TicketHistoryItemResponse("5", "status", "open", "resolved", null, null, Start.UtcDateTime.AddHours(5)));

        var history = await _tracker.ListHistoryAsync(_me, ticket.Id, CancellationToken.None);

        Assert.Equal(["created", "status", "status"], history.Select(h => h.Type));
        Assert.Equal([null, "open", "resolved"], history.Select(h => h.Status));
        Assert.Equal(Start.UtcDateTime, history[0].At);
        Assert.Equal(Start.UtcDateTime.AddHours(5), history[2].At);
    }
}
