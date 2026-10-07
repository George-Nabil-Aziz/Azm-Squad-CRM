using Crm.Application.Channels;
using Crm.Application.Channels.Sms;
using Crm.Application.Common.Exceptions;
using Crm.Application.Tickets;
using Crm.Domain.Channels;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Channels;

/// <summary>CRM-57: segments, the Twilio signature, the SMS webhooks and how SMS flows through the shared channel code.</summary>
public class SmsChannelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private const string Token = "sms-auth-token";
    private const string Url = "https://crm.example.com/api/webhooks/sms";

    // ---- segments (AC 4)

    [Theory]
    [InlineData("", 0, "gsm7")]
    [InlineData("Hello", 1, "gsm7")]
    public void Segments_ShortGsmText(string text, int segments, string encoding)
    {
        var info = SmsSegments.Analyze(text);

        Assert.Equal((segments, encoding), (info.Segments, info.Encoding));
    }

    [Theory]
    [InlineData(160, 1)]
    [InlineData(161, 2)]
    [InlineData(306, 2)]
    [InlineData(307, 3)]
    public void Segments_Gsm7_Use160Then153(int length, int segments)
    {
        Assert.Equal(segments, SmsSegments.Analyze(new string('a', length)).Segments);
    }

    [Fact]
    public void Segments_ExtensionCharactersCountTwice()
    {
        var info = SmsSegments.Analyze(new string('€', 80));

        Assert.Equal((160, 1), (info.Units, info.Segments));
        Assert.Equal(2, SmsSegments.Analyze(new string('€', 81)).Segments);
    }

    [Theory]
    [InlineData(70, 1)]
    [InlineData(71, 2)]
    [InlineData(134, 2)]
    [InlineData(135, 3)]
    public void Segments_ArabicText_IsUcs2_With70Then67(int length, int segments)
    {
        var info = SmsSegments.Analyze(new string('ع', length));

        Assert.Equal(("ucs2", segments), (info.Encoding, info.Segments));
    }

    [Fact]
    public void Segments_OneNonGsmCharacter_MakesTheWholeTextUcs2() =>
        Assert.Equal(("ucs2", 2), (SmsSegments.Analyze(new string('a', 70) + "ع").Encoding, SmsSegments.Analyze(new string('a', 70) + "ع").Segments));

    // ---- signature (AC 2)

    private static Dictionary<string, string> Form(string sid = "SM1", string from = "+966501234567", string body = "Hi") =>
        new() { ["MessageSid"] = sid, ["From"] = from, ["Body"] = body };

    [Fact]
    public void Signature_Matches_ForTheSameUrlParamsAndToken()
    {
        var form = Form();

        var signature = TwilioSignature.Compute(Url, form, Token);

        Assert.True(TwilioSignature.IsValid(Url, form, signature, Token));
    }

    [Fact]
    public void Signature_FailsForATamperedParameter_WrongToken_WrongUrl_OrMissingHeader()
    {
        var form = Form();
        var signature = TwilioSignature.Compute(Url, form, Token);

        Assert.False(TwilioSignature.IsValid(Url, Form(body: "Changed"), signature, Token));
        Assert.False(TwilioSignature.IsValid(Url, form, signature, "other-token"));
        Assert.False(TwilioSignature.IsValid(Url + "x", form, signature, Token));
        Assert.False(TwilioSignature.IsValid(Url, form, null, Token));
        Assert.False(TwilioSignature.IsValid(Url, form, signature, null));
    }

    [Fact]
    public void Signature_IsIndependentOfTheParameterOrder() =>
        Assert.Equal(
            TwilioSignature.Compute(Url, new Dictionary<string, string> { ["A"] = "1", ["B"] = "2" }, Token),
            TwilioSignature.Compute(Url, new Dictionary<string, string> { ["B"] = "2", ["A"] = "1" }, Token));

    // ---- webhook service

    private sealed class RecordingProcessor : IInboundMessageProcessor
    {
        public List<InboundChannelMessage> Messages { get; } = [];

        public Task<InboundResult> ProcessAsync(InboundChannelMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(new InboundResult(false, Guid.NewGuid(), Guid.NewGuid(), false, null));
        }
    }

    private sealed class StatusSender : IChannelSender
    {
        public List<(string Id, DeliveryStatus Status, string? Error)> Statuses { get; } = [];

        public Task<bool> ApplyDeliveryStatusAsync(string providerMessageId, DeliveryStatus status, string? error, CancellationToken cancellationToken)
        {
            Statuses.Add((providerMessageId, status, error));
            return Task.FromResult(true);
        }

        public Task EnsureCanSendAsync(ChannelReply reply, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> RetryDueAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ChannelStatusResponse GetStatus() => throw new NotSupportedException();
    }

    private readonly RecordingProcessor _processor = new();
    private readonly StatusSender _sender = new();

    private SmsWebhookService Service(string? token = Token) =>
        new(new SmsChannelOptions { AuthToken = token }, _processor, _sender, new ChannelClock(Now));

    [Fact]
    public async Task Inbound_WithAnInvalidSignature_ThrowsUnauthorized_AndStoresNothing()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service().HandleInboundAsync(Url, Form(), "bad", CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(token: null).HandleInboundAsync(Url, Form(), "bad", CancellationToken.None));

        Assert.Empty(_processor.Messages);
    }

    [Fact]
    public async Task Inbound_PassesTheSmsToTheProcessor()
    {
        var form = Form(sid: "SM42", from: "+966501234567", body: "Where is my order?");

        await Service().HandleInboundAsync(Url, form, TwilioSignature.Compute(Url, form, Token), CancellationToken.None);

        var message = Assert.Single(_processor.Messages);
        Assert.Equal((ChannelKind.Sms, "SM42", "+966501234567", "Where is my order?", Now.UtcDateTime),
            (message.Channel, message.ExternalId, message.From, message.Body, message.ReceivedAt));
    }

    [Theory]
    [InlineData("delivered", DeliveryStatus.Delivered)]
    [InlineData("undelivered", DeliveryStatus.Failed)]
    [InlineData("failed", DeliveryStatus.Failed)]
    public async Task Status_IsAppliedToTheOutboundMessage(string providerStatus, DeliveryStatus expected)
    {
        var form = new Dictionary<string, string> { ["MessageSid"] = "SM9", ["MessageStatus"] = providerStatus, ["ErrorCode"] = "30003" };

        await Service().HandleStatusAsync(Url + "/status", form, TwilioSignature.Compute(Url + "/status", form, Token), CancellationToken.None);

        var applied = Assert.Single(_sender.Statuses);
        Assert.Equal(("SM9", expected), (applied.Id, applied.Status));
    }

    [Fact]
    public async Task Status_Queued_IsIgnored()
    {
        var form = new Dictionary<string, string> { ["MessageSid"] = "SM9", ["MessageStatus"] = "queued" };

        await Service().HandleStatusAsync(Url, form, TwilioSignature.Compute(Url, form, Token), CancellationToken.None);

        Assert.Empty(_sender.Statuses);
    }

    // ---- shared channel code

    [Fact]
    public async Task Sender_WhenSmsIsNotConfigured_StoresAFailedMessage()
    {
        var provider = new FakeChannelProvider(ChannelKind.Sms, isConfigured: false);
        var sender = new ChannelSender([provider], new FakeOutboundMessageRepository(), new FakeReceivedMessageRepository(), new ChannelClock(Now));

        var result = await sender.SendAsync(new ChannelReply(ChannelKind.Sms, "+966501234567", null, "Hi", null, null), CancellationToken.None);

        Assert.Equal(("failed", "sms"), (result.Status, result.Channel));
        Assert.Equal(ChannelText.SmsNotConfigured, result.LastError);
    }

    [Fact]
    public async Task Sender_AFailedSms_IsRetriedWhenDue()
    {
        var provider = new FakeChannelProvider(ChannelKind.Sms) { NextResult = ChannelSendResult.Fail("provider down") };
        var clock = new ChannelClock(Now);
        var messages = new FakeOutboundMessageRepository();
        var sender = new ChannelSender([provider], messages, new FakeReceivedMessageRepository(), clock);
        var first = await sender.SendAsync(new ChannelReply(ChannelKind.Sms, "+966501234567", null, "Hi", null, null), CancellationToken.None);
        Assert.Equal("failed", first.Status);

        provider.NextResult = ChannelSendResult.Ok("SM1");
        clock.UtcNow = Now.AddMinutes(2);
        var retried = await sender.RetryDueAsync(CancellationToken.None);

        Assert.Equal(1, retried);
        Assert.Equal("sent", ChannelValues.StatusName(messages.Messages.Single().Status));
    }

    [Fact]
    public async Task Processor_UnknownSmsNumber_CreatesACustomerWithThePhone()
    {
        var customers = new FakeCustomerService();
        var processor = new InboundMessageProcessor(new FakeReceivedMessageRepository(), customers, new FakeChannelTicketService(), new ChannelClock(Now));

        var result = await processor.ProcessAsync(
            new InboundChannelMessage(ChannelKind.Sms, "SM1", "+966501234567", null, null, "Hi", Now.UtcDateTime), CancellationToken.None);

        Assert.True(result.NewCustomer);
        var created = Assert.Single(customers.Created);
        Assert.Equal(("+966501234567", "+966501234567", (string?)null), (created.Name, created.Phone, created.Email));
    }

    [Fact]
    public async Task Tickets_SmsJoinsTheOpenSmsTicket_OrOpensANewOne()
    {
        var tickets = new FakeTicketRepository(new FakeTicketCategoryRepository());
        var customer = tickets.AddCustomer("Nour");
        var messages = new FakeTicketMessageRepository();
        var service = new ChannelTicketService(tickets, messages, new FakeInteractionRecorder(),
            new Crm.UnitTests.Sla.FakeSlaPolicyRepository(Now.UtcDateTime), new ChannelClock(Now),
            new Crm.UnitTests.Settings.FakeSystemSettingsProvider());
        InboundChannelMessage Sms(string id, string body) => new(ChannelKind.Sms, id, "+966501234567", null, null, body, Now.UtcDateTime);

        var first = await service.AddInboundAsync(customer, Sms("SM1", "Where is my order?"), null, CancellationToken.None);
        var second = await service.AddInboundAsync(customer, Sms("SM2", "Hello?"), null, CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        var ticket = Assert.Single(tickets.Tickets);
        Assert.Equal((TicketChannel.Sms, "Where is my order?"), (ticket.Channel, ticket.Subject));
        Assert.Equal(2, messages.Messages.Count);
    }

    [Fact]
    public async Task Dispatcher_SmsTicket_RepliesToThePhoneNumber()
    {
        var customers = new FakeCustomerService();
        var customer = customers.AddCustomer("Nour", phone: "+966501234567");
        var tickets = new FakeTicketRepository(new FakeTicketCategoryRepository());
        var ticket = Ticket.Create(customer.Id, "Order", null, null, TicketPriority.Mid, TicketChannel.Sms, null, Now.UtcDateTime);
        ticket.AssignNumber(3);
        tickets.Tickets.Add(ticket);
        var sent = new List<ChannelReply>();
        var dispatcher = new ChannelTicketReplyDispatcher(new CapturingSender(sent), customers, tickets);

        await dispatcher.DispatchAsync(TicketMessage.Staff(ticket.Id, "On its way", false, TicketChannel.Sms, Guid.NewGuid(), Now.UtcDateTime), null, CancellationToken.None);

        var reply = Assert.Single(sent);
        Assert.Equal((ChannelKind.Sms, "+966501234567", (string?)null, "On its way"), (reply.Channel, reply.Recipient, reply.Subject, reply.Body));
    }

    private sealed class CapturingSender(List<ChannelReply> sent) : IChannelSender
    {
        public Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken)
        {
            sent.Add(reply);
            return Task.FromResult(new OutboundMessageResponse(Guid.NewGuid(), "sms", reply.Recipient, "sent", 1, null, null, reply.SourceId, Now.UtcDateTime, Now.UtcDateTime));
        }

        public Task<bool> ApplyDeliveryStatusAsync(string providerMessageId, DeliveryStatus status, string? error, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task EnsureCanSendAsync(ChannelReply reply, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> RetryDueAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ChannelStatusResponse GetStatus() => throw new NotSupportedException();
    }
}
