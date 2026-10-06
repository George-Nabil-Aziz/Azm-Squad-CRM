using Crm.Application.Tickets;
using Crm.Domain.Notifications;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Notifications;

/// <summary>CRM-33: @mentions in internal notes notify the mentioned users.</summary>
public class MentionTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly Guid ColleagueId = Guid.NewGuid();

    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeTicketMessageRepository _messages = new();
    private readonly FakeNotificationDispatcher _notifications = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketMessageService _service;
    private readonly Ticket _ticket;

    public MentionTests()
    {
        _service = new TicketMessageService(_tickets, _messages, new FakeInteractionRecorder(), new FakeTicketReplyDispatcher(),
            new FakeCurrentUser(AuthorId), _clock, new AddTicketMessageRequestValidator(), _notifications);
        _ticket = Ticket.Create(_tickets.AddCustomer("Nour"), "Invoice", null, null, TicketPriority.High, TicketChannel.Manual, AuthorId, _clock.UtcNow.UtcDateTime);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
    }

    private Task<TicketMessageResponse> AddAsync(string body, bool internalNote, params Guid[] mentioned) =>
        _service.AddAsync(_ticket.Id, new AddTicketMessageRequest(body, internalNote, null, mentioned), CancellationToken.None);

    [Fact]
    public async Task AMentionInAnInternalNote_NotifiesTheMentionedUser_AboutTheTicket()
    {
        var note = await AddAsync("@Omar please check the invoice", internalNote: true, ColleagueId);

        var request = Assert.Single(_notifications.Requests);
        Assert.Equal((NotificationType.Mention, (Guid?)_ticket.Id, $"mention:{note.Id}"), (request.Type, request.TicketId, request.DedupKey)); // AC 1, 2
        Assert.Equal([ColleagueId], request.UserIds);
        Assert.Equal("@Omar please check the invoice", request.Text);
    }

    [Fact]
    public async Task TheExcerptIsShortened()
    {
        await AddAsync(new string('x', 400), internalNote: true, ColleagueId);

        Assert.Equal(100, _notifications.Requests.Single().Text!.Length);
    }

    [Fact]
    public async Task MentioningYourself_NotifiesNobody()
    {
        await AddAsync("note to self @me", internalNote: true, AuthorId);

        Assert.Empty(_notifications.Requests);
    }

    [Fact]
    public async Task AMentionInAPublicReply_NotifiesNobody()
    {
        await AddAsync("Hello @Omar", internalNote: false, ColleagueId); // AC 4: mentions belong to internal notes only

        Assert.Empty(_notifications.Requests);
    }

    [Fact]
    public async Task ANoteWithoutMentions_NotifiesNobody()
    {
        await AddAsync("just a note", internalNote: true);

        Assert.Empty(_notifications.Requests);
    }

    [Fact]
    public async Task Notes_AreNeverInTheCustomerVisibleThread()
    {
        await AddAsync("@Omar internal", internalNote: true, ColleagueId);
        await AddAsync("Hello customer", internalNote: false);

        var visible = await _service.ListCustomerVisibleAsync(_ticket.Id, CancellationToken.None);

        Assert.Equal(["Hello customer"], visible.Select(m => m.Body)); // AC 4
    }
}
