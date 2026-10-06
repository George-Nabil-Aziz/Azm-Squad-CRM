using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public class TicketMessageServiceTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeTicketMessageRepository _messages = new();
    private readonly FakeInteractionRecorder _timeline = new();
    private readonly FakeTicketReplyDispatcher _dispatcher = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketMessageService _service;
    private readonly Ticket _ticket;

    public TicketMessageServiceTests()
    {
        _service = new TicketMessageService(_tickets, _messages, _timeline, _dispatcher, new FakeCurrentUser(AgentId), _clock,
            new AddTicketMessageRequestValidator());
        var customerId = _tickets.AddCustomer("Nour Trading");
        _ticket = Ticket.Create(customerId, "Invoice is wrong", null, null, TicketPriority.High, TicketChannel.Manual, AgentId, _clock.UtcNow.UtcDateTime);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
        _messages.Authors[AgentId] = "Sara Agent";
    }

    private Task<TicketMessageResponse> ReplyAsync(string? body = "We are on it.", bool? internalNote = null) =>
        _service.AddAsync(_ticket.Id, new AddTicketMessageRequest(body, internalNote), CancellationToken.None);

    [Fact]
    public async Task Reply_AppearsInTheThread_WithAuthorAndTime()
    {
        var added = await ReplyAsync();
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        await ReplyAsync("Fixed.");

        var thread = await _service.ListAsync(_ticket.Id, CancellationToken.None);

        Assert.Equal("outbound", added.Direction);
        Assert.False(added.IsInternal);
        Assert.Equal(["We are on it.", "Fixed."], thread.Select(m => m.Body));
        Assert.All(thread, m => Assert.Equal(("Sara Agent", AgentId), (m.AuthorName, m.AuthorId)));
        Assert.Equal(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), thread[0].CreatedAt);
        Assert.Equal(DateTimeKind.Utc, thread[0].CreatedAt.Kind);
    }

    [Fact]
    public async Task Note_IsFlaggedInternal_AndHiddenFromTheCustomerView()
    {
        await ReplyAsync("Public answer.");
        var note = await ReplyAsync("Customer is a VIP.", internalNote: true);

        var staff = await _service.ListAsync(_ticket.Id, CancellationToken.None);
        var customer = await _service.ListCustomerVisibleAsync(_ticket.Id, CancellationToken.None);

        Assert.Equal("internal", note.Direction);
        Assert.True(note.IsInternal);
        Assert.Equal(2, staff.Count);
        Assert.Equal(["Public answer."], customer.Select(m => m.Body));
    }

    [Fact]
    public async Task Note_IsNeverDispatched_AndNeverRecordedInTheCustomerTimeline()
    {
        await ReplyAsync("Customer is a VIP.", internalNote: true);

        Assert.Empty(_dispatcher.Dispatched);
        Assert.Empty(_timeline.Entries);
    }

    [Fact]
    public async Task Reply_IsDispatched_AfterSaving_AndRecordedInTheTimeline()
    {
        var added = await ReplyAsync();

        var dispatched = Assert.Single(_dispatcher.Dispatched);
        Assert.Equal(added.Id, dispatched.Id);
        var entry = Assert.Single(_timeline.Entries);
        Assert.Equal((_ticket.CustomerId, InteractionType.Message, InteractionEvents.MessageSent, added.Id),
            (entry.CustomerId, entry.Type, entry.Event, entry.SourceId));
        Assert.Equal(1, _tickets.SaveCount);
    }

    [Fact]
    public async Task FirstAgentReply_SetsFirstResponseAt_AndLaterRepliesKeepIt()
    {
        var firstTime = _clock.UtcNow.UtcDateTime;
        await ReplyAsync();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await ReplyAsync("Second");

        Assert.Equal(firstTime, _ticket.FirstResponseAt);
    }

    [Fact]
    public async Task InternalNote_DoesNotSetFirstResponseAt()
    {
        await ReplyAsync("Thinking about it.", internalNote: true);

        Assert.Null(_ticket.FirstResponseAt);
    }

    [Fact]
    public async Task Reply_ToAClosedTicket_ThrowsValidationException()
    {
        TicketTestSupport.SetStatus(_ticket, TicketStatus.Closed);

        var error = await Assert.ThrowsAsync<ValidationException>(() => ReplyAsync());
        await Assert.ThrowsAsync<ValidationException>(() => ReplyAsync("note", internalNote: true));

        Assert.Contains("status", error.Errors.Keys);
        Assert.Empty(_messages.Messages);
        Assert.Null(_ticket.FirstResponseAt);
    }

    [Fact]
    public async Task UnknownTicket_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddAsync(Guid.NewGuid(), new AddTicketMessageRequest("Hi", null), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListCustomerVisibleAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task BlankBody_ThrowsValidationException_OnBody(string? body)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => ReplyAsync(body));

        Assert.Contains("body", error.Errors.Keys);
    }

    [Fact]
    public async Task TooLongBody_ThrowsValidationException_OnBody()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => ReplyAsync(new string('x', TicketMessage.BodyMaxLength + 1)));

        Assert.Contains("body", error.Errors.Keys);
    }
}
