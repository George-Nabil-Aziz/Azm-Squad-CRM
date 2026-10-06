using Crm.Application.Channels;
using Crm.Application.Tickets;
using Crm.Domain.Channels;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Channels;

/// <summary>CRM-24 / CRM-26 Phase 2: inbound messages become tickets and thread messages.</summary>
public class ChannelTicketServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeTicketCategoryRepository _categories = new();
    private readonly FakeTicketRepository _tickets;
    private readonly FakeTicketMessageRepository _messages = new();
    private readonly FakeInteractionRecorder _timeline = new();
    private readonly ChannelTicketService _service;
    private readonly Guid _customer;

    public ChannelTicketServiceTests()
    {
        _tickets = new FakeTicketRepository(_categories);
        _customer = _tickets.AddCustomer("Nour");
        _service = new ChannelTicketService(_tickets, _messages, _timeline,
            new Crm.UnitTests.Sla.FakeSlaPolicyRepository(Now.UtcDateTime), new ChannelClock(Now),
            new Crm.UnitTests.Settings.FakeSystemSettingsProvider());
    }

    private static InboundChannelMessage Email(string? subject, string body = "It does not print.") =>
        new(ChannelKind.Email, "<m1@mail>", "nour@example.com", "Nour", subject, body, Now.UtcDateTime);

    private static InboundChannelMessage WhatsApp(string body = "Where is my order?") =>
        new(ChannelKind.WhatsApp, "wamid.1", "+966501234567", "Nour", null, body, Now.UtcDateTime);

    [Fact]
    public async Task Email_WithoutTag_CreatesAnEmailTicket_WithSlaAndFirstMessage()
    {
        var result = await _service.AddInboundAsync(_customer, Email("Printer broken"), null, CancellationToken.None);

        Assert.True(result.Created);
        var ticket = Assert.Single(_tickets.Tickets);
        Assert.Equal((TicketChannel.Email, "Printer broken", "It does not print.", (Guid?)null), (ticket.Channel, ticket.Subject, ticket.Description, ticket.CreatedById));
        Assert.NotNull(ticket.ResponseDueAt);
        var message = Assert.Single(_messages.Messages);
        Assert.Equal((MessageDirection.Inbound, "<m1@mail>"), (message.Direction, message.ExternalMessageId));
        Assert.Contains(_timeline.Entries, e => e.Event == InteractionEvents.TicketCreated);
        Assert.Contains(_timeline.Entries, e => e.Event == InteractionEvents.MessageReceived);
    }

    [Fact]
    public async Task Email_Subject_HasTheTagRemoved_AndFallsBackWhenEmpty()
    {
        await _service.AddInboundAsync(_customer, Email("  [TKT-000099]  "), 99, CancellationToken.None);

        Assert.Equal(ChannelText.NoSubject, Assert.Single(_tickets.Tickets).Subject);
    }

    [Fact]
    public async Task Email_WithTagOfAnOpenTicketOfTheCustomer_IsAddedToIt_WithoutFirstResponse()
    {
        var first = await _service.AddInboundAsync(_customer, Email("Printer broken"), null, CancellationToken.None);
        var ticket = _tickets.Tickets.Single();

        var second = await _service.AddInboundAsync(_customer, Email("Re: Printer broken [TKT-000001]", "Any news?"), ticket.Number, CancellationToken.None);

        Assert.False(second.Created);
        Assert.Equal(first.TicketId, second.TicketId);
        Assert.Single(_tickets.Tickets);
        Assert.Equal(2, _messages.Messages.Count);
        Assert.Null(ticket.FirstResponseAt);
    }

    [Fact]
    public async Task Email_WithTagOfAClosedTicket_OpensANewTicket()
    {
        await _service.AddInboundAsync(_customer, Email("Printer broken"), null, CancellationToken.None);
        var ticket = _tickets.Tickets.Single();
        TicketTestSupport.SetStatus(ticket, TicketStatus.Closed);

        var again = await _service.AddInboundAsync(_customer, Email("Re: [TKT-000001]"), ticket.Number, CancellationToken.None);

        Assert.True(again.Created);
        Assert.Equal(2, _tickets.Tickets.Count);
    }

    [Fact]
    public async Task WhatsApp_CreatesATicketWithTheTextAsSubject_ThenAddsToTheOpenOne()
    {
        var first = await _service.AddInboundAsync(_customer, WhatsApp(new string('x', 120)), null, CancellationToken.None);
        var second = await _service.AddInboundAsync(_customer, WhatsApp("Hello?"), null, CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        var ticket = Assert.Single(_tickets.Tickets);
        Assert.Equal((TicketChannel.WhatsApp, 80), (ticket.Channel, ticket.Subject.Length));
        Assert.Equal(2, _messages.Messages.Count);
    }

    [Fact]
    public async Task WhatsApp_AfterResolved_CreatesANewTicket()
    {
        await _service.AddInboundAsync(_customer, WhatsApp(), null, CancellationToken.None);
        TicketTestSupport.SetStatus(_tickets.Tickets.Single(), TicketStatus.Resolved);

        var again = await _service.AddInboundAsync(_customer, WhatsApp("New"), null, CancellationToken.None);

        Assert.True(again.Created);
    }
}
