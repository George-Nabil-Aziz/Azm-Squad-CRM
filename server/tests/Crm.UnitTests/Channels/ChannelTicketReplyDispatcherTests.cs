using Crm.Application.Channels;
using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Channels;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Channels;

/// <summary>CRM-23 / CRM-25 Phase 2: who a reply is sent to, subject tag, delivery status follow-up.</summary>
public class ChannelTicketReplyDispatcherTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeCustomerService _customers = new();
    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly RecordingSender _sender = new();

    private ChannelTicketReplyDispatcher Dispatcher() => new(_sender, _customers, _tickets);

    private sealed class RecordingSender : IChannelSender
    {
        public List<ChannelReply> Sent { get; } = [];

        public List<ChannelReply> Checked { get; } = [];

        public Task EnsureCanSendAsync(ChannelReply reply, CancellationToken cancellationToken)
        {
            Checked.Add(reply);
            return Task.CompletedTask;
        }

        public Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken)
        {
            Sent.Add(reply);
            return Task.FromResult(new OutboundMessageResponse(Guid.NewGuid(), "email", reply.Recipient, "sent", 1, null, null, reply.SourceId, Now, Now));
        }

        public Task<bool> ApplyDeliveryStatusAsync(string id, DeliveryStatus status, string? error, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> RetryDueAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ChannelStatusResponse GetStatus() => throw new NotSupportedException();
    }

    private (Ticket Ticket, TicketMessage Reply) Arrange(TicketChannel channel, string? email = null, string? phone = null, string? whatsApp = null)
    {
        var customer = _customers.AddCustomer("Nour", email: email, phone: phone, whatsApp: whatsApp);
        var ticket = Ticket.Create(customer.Id, "Printer broken", null, null, TicketPriority.Mid, channel, null, Now);
        ticket.AssignNumber(12);
        _tickets.Tickets.Add(ticket);
        return (ticket, TicketMessage.Staff(ticket.Id, "We are on it.", false, channel, Guid.NewGuid(), Now));
    }

    [Fact]
    public async Task EmailTicket_SendsToThePrimaryEmail_WithTheTicketTagInTheSubject()
    {
        var (_, reply) = Arrange(TicketChannel.Email, email: "nour@example.com");

        await Dispatcher().DispatchAsync(reply, null, CancellationToken.None);

        var sent = Assert.Single(_sender.Sent);
        Assert.Equal((ChannelKind.Email, "nour@example.com", "Re: Printer broken [TKT-000012]", "We are on it.", (Guid?)reply.Id),
            (sent.Channel, sent.Recipient, sent.Subject, sent.Body, sent.SourceId));
    }

    [Fact]
    public async Task WebFormTicket_RepliesByEmail()
    {
        var (_, reply) = Arrange(TicketChannel.WebForm, email: "visitor@example.com");

        await Dispatcher().DispatchAsync(reply, null, CancellationToken.None);

        var sent = Assert.Single(_sender.Sent);
        Assert.Equal((ChannelKind.Email, "visitor@example.com", "Re: Printer broken [TKT-000012]"), (sent.Channel, sent.Recipient, sent.Subject));
    }

    [Fact]
    public async Task WhatsAppTicket_PrefersTheWhatsAppContact_ElseThePhone_AndPassesTheTemplate()
    {
        var (_, withBoth) = Arrange(TicketChannel.WhatsApp, phone: "+966501111111", whatsApp: "+966502222222");
        var (_, phoneOnly) = Arrange(TicketChannel.WhatsApp, phone: "+966503333333");

        await Dispatcher().DispatchAsync(withBoth, "order_update", CancellationToken.None);
        await Dispatcher().DispatchAsync(phoneOnly, null, CancellationToken.None);

        Assert.Equal(["+966502222222", "+966503333333"], _sender.Sent.Select(s => s.Recipient));
        Assert.Equal(["order_update", null], _sender.Sent.Select(s => s.TemplateName));
    }

    [Fact]
    public async Task Validate_WithoutAnAddress_Throws_AndAsksTheSenderToCheckTheWindow()
    {
        var (noEmail, _) = Arrange(TicketChannel.Email);
        var (whatsApp, _) = Arrange(TicketChannel.WhatsApp, whatsApp: "+966502222222");

        await Assert.ThrowsAsync<ValidationException>(() => Dispatcher().ValidateAsync(noEmail, null, CancellationToken.None));
        await Dispatcher().ValidateAsync(whatsApp, null, CancellationToken.None);

        Assert.Equal("+966502222222", Assert.Single(_sender.Checked).Recipient);
    }

    [Fact]
    public async Task ManualTicket_DeliversNothing()
    {
        var (ticket, reply) = Arrange(TicketChannel.Manual, email: "nour@example.com");

        await Dispatcher().ValidateAsync(ticket, null, CancellationToken.None);
        await Dispatcher().DispatchAsync(reply, null, CancellationToken.None);

        Assert.Empty(_sender.Sent);
        Assert.Empty(_sender.Checked);
    }

    [Fact]
    public async Task DeliveryObserver_FollowsTheOutboundMessage()
    {
        var (_, reply) = Arrange(TicketChannel.Email, email: "nour@example.com");
        var messages = new FakeTicketMessageRepository();
        messages.Add(reply);
        var observer = new TicketDeliveryObserver(messages);
        var outbound = OutboundMessage.Create(ChannelKind.Email, "nour@example.com", "s", "b", null, reply.Id, Now);

        outbound.MarkFailed("421", Now);
        await observer.OnDeliveryChangedAsync(outbound, CancellationToken.None);
        Assert.Equal(MessageDeliveryStatus.Failed, reply.DeliveryStatus);

        outbound.MarkSent("<id@x>", Now);
        await observer.OnDeliveryChangedAsync(outbound, CancellationToken.None);
        Assert.Equal((MessageDeliveryStatus.Sent, "<id@x>"), (reply.DeliveryStatus, reply.ExternalMessageId));
    }
}
