using Crm.Application.Chat;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.RateLimiting;
using Crm.Application.WebForms;
using Crm.Domain.Chat;
using Crm.Domain.Tickets;
using Crm.UnitTests.Channels;
using Crm.UnitTests.Tickets;
using Crm.UnitTests.WebForms;

namespace Crm.UnitTests.Chat;

public class ChatServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeChatRepository _repository = new();
    private readonly FakeChatNotifier _notifier = new();
    private readonly InMemoryAgentPresence _presence = new();
    private readonly FakeCustomerService _customers = new();
    private readonly FakeTicketService _tickets = new();
    private readonly TestClock _clock = new(Start);
    private readonly Guid _agent = Guid.NewGuid();

    private ChatService Service(int limit = 100) => new(
        _repository, _notifier, _presence, _customers, _tickets, new StartChatRequestValidator(),
        new FixedWindowRateLimiter(_clock), new WebFormOptions { RateLimitRequests = limit, RateLimitWindowSeconds = 60 }, _clock);

    private void AgentOnline() => _presence.Connected(_agent, "conn-1");

    private static StartChatRequest Start1(string? message = "Hello") => new("Nour Ali", "nour@example.com", message);

    private static ChatParticipant Visitor => new(ChatSender.Visitor, null, null);

    private ChatParticipant Agent => new(ChatSender.Agent, _agent, "Sara");

    [Fact]
    public async Task Start_WithoutAnAgentOnline_Throws409()
    {
        await Assert.ThrowsAsync<ConflictException>(() => Service().StartAsync(Start1(), "10.0.0.1", CancellationToken.None));

        Assert.Empty(_repository.Sessions);
    }

    [Fact]
    public async Task Start_StoresAWaitingSession_ReturnsAToken_AndNotifiesTheAgents()
    {
        AgentOnline();

        var started = await Service().StartAsync(Start1(), "10.0.0.1", CancellationToken.None);

        var session = Assert.Single(_repository.Sessions);
        Assert.Equal(ChatStatus.Waiting, session.Status);
        Assert.NotEqual(started.VisitorToken, session.VisitorTokenHash); // only the hash is stored
        Assert.Equal(started.Session.Id, session.Id);
        Assert.Equal("Hello", Assert.Single(session.Messages).Body);
        Assert.Equal(started.Session.Id, Assert.Single(_notifier.Started).Id);
        Assert.True(await Service().AuthorizeVisitorAsync(session.Id, started.VisitorToken, CancellationToken.None));
    }

    [Fact]
    public async Task Start_WithMissingFields_ThrowsValidation()
    {
        AgentOnline();

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service().StartAsync(new StartChatRequest("", "not-an-email", null), "10.0.0.1", CancellationToken.None));

        Assert.Contains("name", error.Errors.Keys);
        Assert.Contains("email", error.Errors.Keys);
    }

    [Fact]
    public async Task Start_BeyondTheRateLimit_Throws429()
    {
        AgentOnline();
        var service = Service(limit: 1);
        await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);

        await Assert.ThrowsAsync<RateLimitExceededException>(() => service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None));
    }

    [Fact]
    public async Task AuthorizeVisitor_WithAWrongTokenOrSession_IsFalse()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);

        Assert.False(await service.AuthorizeVisitorAsync(started.Session.Id, "wrong", CancellationToken.None));
        Assert.False(await service.AuthorizeVisitorAsync(started.Session.Id, null, CancellationToken.None));
        Assert.False(await service.AuthorizeVisitorAsync(Guid.NewGuid(), started.VisitorToken, CancellationToken.None));
    }

    [Fact]
    public async Task Accept_MakesTheSessionActive_AndTellsTheVisitorAndAgents()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);

        var accepted = await service.AcceptAsync(started.Session.Id, _agent, "Sara", CancellationToken.None);

        Assert.Equal(("active", _agent), (accepted.Status, accepted.AgentId));
        Assert.Equal(started.Session.Id, Assert.Single(_notifier.Accepted).Id);
    }

    [Fact]
    public async Task Accept_AnAlreadyAcceptedChat_Throws409()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);
        await service.AcceptAsync(started.Session.Id, _agent, "Sara", CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => service.AcceptAsync(started.Session.Id, Guid.NewGuid(), "Omar", CancellationToken.None));
    }

    [Fact]
    public async Task Send_StoresTheMessage_AndPushesItToTheChat()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);
        await service.AcceptAsync(started.Session.Id, _agent, "Sara", CancellationToken.None);

        var fromAgent = await service.SendAsync(started.Session.Id, Agent, "How can I help?", CancellationToken.None);
        var fromVisitor = await service.SendAsync(started.Session.Id, Visitor, "My printer is broken", CancellationToken.None);

        Assert.Equal(("agent", "Sara", "How can I help?"), (fromAgent.Sender, fromAgent.SenderName, fromAgent.Body));
        Assert.Equal(("visitor", "Nour Ali"), (fromVisitor.Sender, fromVisitor.SenderName));
        Assert.Equal([fromAgent.Id, fromVisitor.Id], _notifier.Messages.Select(m => m.Id));
        Assert.Equal(3, _repository.Sessions.Single().Messages.Count); // the first message + two
    }

    [Fact]
    public async Task Send_ByAnAgentWhoDidNotAcceptTheChat_IsForbidden()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);
        await service.AcceptAsync(started.Session.Id, _agent, "Sara", CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.SendAsync(started.Session.Id, new ChatParticipant(ChatSender.Agent, Guid.NewGuid(), "Omar"), "Hi", CancellationToken.None));
    }

    [Fact]
    public async Task Send_EmptyOrTooLong_ThrowsValidationOnBody()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(null), "10.0.0.1", CancellationToken.None);

        var empty = await Assert.ThrowsAsync<ValidationException>(() => service.SendAsync(started.Session.Id, Visitor, " ", CancellationToken.None));
        var tooLong = await Assert.ThrowsAsync<ValidationException>(
            () => service.SendAsync(started.Session.Id, Visitor, new string('a', ChatMessage.BodyMaxLength + 1), CancellationToken.None));

        Assert.Contains("body", empty.Errors.Keys);
        Assert.Contains("body", tooLong.Errors.Keys);
    }

    [Fact]
    public async Task Send_AfterTheChatEnded_Throws409()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);
        await service.EndAsync(started.Session.Id, Visitor, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => service.SendAsync(started.Session.Id, Visitor, "Anyone?", CancellationToken.None));
    }

    [Fact]
    public async Task End_SavesTheTranscriptAsAChatTicket()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);
        await service.AcceptAsync(started.Session.Id, _agent, "Sara", CancellationToken.None);
        await service.SendAsync(started.Session.Id, Agent, "How can I help?", CancellationToken.None);

        var ended = await service.EndAsync(started.Session.Id, Agent, CancellationToken.None);

        var (customerId, request, channel) = Assert.Single(_tickets.Created);
        Assert.Equal(TicketChannel.Chat, channel);
        Assert.Equal("Chat with Nour Ali", request.Subject);
        Assert.Contains("Nour Ali: Hello", request.Description);
        Assert.Contains("Sara: How can I help?", request.Description);
        Assert.Equal(_customers.Customers.Single().Id, customerId);
        Assert.Equal("ended", ended.Status);
        Assert.NotNull(ended.TicketNumber);
        Assert.Equal(started.Session.Id, Assert.Single(_notifier.Ended).Id);
    }

    [Fact]
    public async Task End_Twice_CreatesOneTicket()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);

        await service.EndAsync(started.Session.Id, Visitor, CancellationToken.None);
        var again = await service.EndAsync(started.Session.Id, Visitor, CancellationToken.None);

        Assert.Single(_tickets.Created);
        Assert.Equal("ended", again.Status);
    }

    [Fact]
    public async Task End_ReusesTheCustomerWithTheSameEmail()
    {
        AgentOnline();
        var existing = _customers.AddCustomer("Nour", email: "nour@example.com");
        var service = Service();
        var started = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);

        await service.EndAsync(started.Session.Id, Visitor, CancellationToken.None);

        Assert.Equal(existing.Id, Assert.Single(_tickets.Created).CustomerId);
        Assert.Empty(_customers.Created);
    }

    [Fact]
    public async Task List_ReturnsTheWaitingChats_AndTheAgentsActiveOnes()
    {
        AgentOnline();
        var service = Service();
        var waiting = await service.StartAsync(Start1(), "10.0.0.1", CancellationToken.None);
        var mine = await service.StartAsync(Start1(), "10.0.0.2", CancellationToken.None);
        await service.AcceptAsync(mine.Session.Id, _agent, "Sara", CancellationToken.None);

        var queue = await service.ListAsync("waiting", _agent, CancellationToken.None);
        var active = await service.ListAsync("active", _agent, CancellationToken.None);

        Assert.Equal(waiting.Session.Id, Assert.Single(queue).Id);
        Assert.Equal(mine.Session.Id, Assert.Single(active).Id);
    }

    [Fact]
    public async Task TranscriptIsCutAtTheTicketDescriptionLimit()
    {
        AgentOnline();
        var service = Service();
        var started = await service.StartAsync(Start1(null), "10.0.0.1", CancellationToken.None);
        for (var i = 0; i < 8; i++)
        {
            await service.SendAsync(started.Session.Id, Visitor, new string('x', 1_900), CancellationToken.None);
        }

        await service.EndAsync(started.Session.Id, Visitor, CancellationToken.None);

        Assert.True(Assert.Single(_tickets.Created).Request.Description!.Length <= Crm.Domain.Tickets.Ticket.DescriptionMaxLength);
    }
}
