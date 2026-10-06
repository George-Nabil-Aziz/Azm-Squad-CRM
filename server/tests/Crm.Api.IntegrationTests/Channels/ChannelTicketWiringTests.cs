using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Channels;
using Crm.Application.Common.Exceptions;
using Crm.Application.Customers;
using Crm.Application.Tickets;
using Crm.Domain.Channels;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.IntegrationTests.Channels;

/// <summary>
/// Phase 2 of CRM-23..26 against the real database: inbound email / WhatsApp becomes tickets and thread messages, and
/// agent replies on those tickets go out through the channel providers (fakes, no network).
/// </summary>
public class ChannelTicketWiringTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed class ScriptedProvider(ChannelKind channel) : IChannelProvider
    {
        public ChannelKind Channel => channel;

        public bool IsConfigured => true;

        public ChannelSendResult Next { get; set; } = ChannelSendResult.Ok("provider-id-1");

        public List<OutboundChannelMessage> Sent { get; } = [];

        public Task<ChannelSendResult> SendAsync(OutboundChannelMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.FromResult(Next);
        }
    }

    private (WebApplicationFactory<Program> App, ScriptedProvider Email, ScriptedProvider WhatsApp) CreateApp()
    {
        var email = new ScriptedProvider(ChannelKind.Email);
        var whatsApp = new ScriptedProvider(ChannelKind.WhatsApp);
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IChannelProvider>();
            services.AddSingleton<IChannelProvider>(email);
            services.AddSingleton<IChannelProvider>(whatsApp);
        }));
        return (app, email, whatsApp);
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@customer.example";

    private static string NewNumber() => "+96650" + Random.Shared.Next(1_000_000, 9_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private InboundChannelMessage EmailMessage(string from, string subject, string text = "It does not print.") =>
        new(ChannelKind.Email, $"<{Guid.NewGuid():N}@mail.example>", from, "Nour", subject, text, factory.Time.GetUtcNow().UtcDateTime);

    private InboundChannelMessage WhatsAppMessage(string from, string text = "Where is my order?") =>
        new(ChannelKind.WhatsApp, $"wamid.{Guid.NewGuid():N}", from, "Nour", null, text, factory.Time.GetUtcNow().UtcDateTime);

    private static async Task<InboundResult> ProcessAsync(IServiceProvider services, InboundChannelMessage message)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IInboundMessageProcessor>().ProcessAsync(message, CancellationToken.None);
    }

    private static async Task<(Ticket Ticket, TicketMessage[] Messages)> LoadAsync(IServiceProvider services, Guid ticketId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        return (await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId),
            await db.TicketMessages.AsNoTracking().Where(m => m.TicketId == ticketId).OrderBy(m => m.CreatedAt).ToArrayAsync());
    }

    [Fact]
    public async Task Email_FromUnknownSender_CreatesCustomerAndTicket_WithTheEmailAsFirstMessage()
    {
        var from = NewEmail();

        var result = await ProcessAsync(factory.Services, EmailMessage(from, "Printer broken"));

        Assert.True(result.TicketCreated);
        Assert.True(result.NewCustomer);
        var (ticket, messages) = await LoadAsync(factory.Services, result.TicketId!.Value);
        Assert.Equal((TicketChannel.Email, "Printer broken", result.CustomerId), (ticket.Channel, ticket.Subject, ticket.CustomerId));
        Assert.Null(ticket.CreatedById);
        Assert.NotNull(ticket.ResponseDueAt); // SLA due times are applied
        var message = Assert.Single(messages);
        Assert.Equal((MessageDirection.Inbound, "It does not print."), (message.Direction, message.Body));
        Assert.Null(ticket.FirstResponseAt);
    }

    [Fact]
    public async Task Email_FromKnownCustomer_CreatesATicketForThatCustomer_AndTagAddsToIt()
    {
        var from = NewEmail();
        Guid customerId;
        using (var scope = factory.Services.CreateScope())
        {
            customerId = (await scope.ServiceProvider.GetRequiredService<ICustomerService>()
                .CreateAsync(new CustomerRequest("Known", from, null), CancellationToken.None)).Id;
        }

        var first = await ProcessAsync(factory.Services, EmailMessage(from, "Invoice wrong"));
        var (ticket, _) = await LoadAsync(factory.Services, first.TicketId!.Value);
        var second = await ProcessAsync(factory.Services, EmailMessage(from, $"Re: Invoice wrong [{Ticket.FormatNumber(ticket.Number)}]", "Any news?"));

        Assert.Equal(customerId, first.CustomerId);
        Assert.False(second.TicketCreated);
        Assert.Equal(first.TicketId, second.TicketId);
        var (_, messages) = await LoadAsync(factory.Services, first.TicketId.Value);
        Assert.Equal(["It does not print.", "Any news?"], messages.Select(m => m.Body));
    }

    [Fact]
    public async Task Email_WithTagOfAnotherCustomersTicket_CreatesANewTicket()
    {
        var other = await ProcessAsync(factory.Services, EmailMessage(NewEmail(), "Other"));
        var (otherTicket, _) = await LoadAsync(factory.Services, other.TicketId!.Value);

        var result = await ProcessAsync(factory.Services, EmailMessage(NewEmail(), $"Re: x [TKT-{otherTicket.Number:D6}]"));

        Assert.True(result.TicketCreated);
        Assert.NotEqual(other.TicketId, result.TicketId);
    }

    [Fact]
    public async Task SameEmailTwice_CreatesOneTicket()
    {
        var message = EmailMessage(NewEmail(), "Twice");

        var first = await ProcessAsync(factory.Services, message);
        var second = await ProcessAsync(factory.Services, message);

        Assert.False(first.Duplicate);
        Assert.True(second.Duplicate);
        Assert.Null(second.TicketId);
        var (_, messages) = await LoadAsync(factory.Services, first.TicketId!.Value);
        Assert.Single(messages);
    }

    [Fact]
    public async Task WhatsApp_UnknownNumber_CreatesCustomerAndTicket_ThenNextMessageJoinsIt()
    {
        var number = NewNumber();

        var first = await ProcessAsync(factory.Services, WhatsAppMessage(number, "Where is my order?"));
        var second = await ProcessAsync(factory.Services, WhatsAppMessage(number, "Hello?"));

        Assert.True(first.TicketCreated);
        Assert.True(first.NewCustomer);
        Assert.False(second.TicketCreated);
        Assert.Equal(first.TicketId, second.TicketId);
        var (ticket, messages) = await LoadAsync(factory.Services, first.TicketId!.Value);
        Assert.Equal((TicketChannel.WhatsApp, "Where is my order?"), (ticket.Channel, ticket.Subject));
        Assert.Equal(["Where is my order?", "Hello?"], messages.Select(m => m.Body));
    }

    [Fact]
    public async Task WhatsApp_AfterTheTicketIsClosed_CreatesANewTicket()
    {
        var number = NewNumber();
        var first = await ProcessAsync(factory.Services, WhatsAppMessage(number));
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets
                .Where(t => t.Id == first.TicketId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Closed));
        }

        var second = await ProcessAsync(factory.Services, WhatsAppMessage(number, "New problem"));

        Assert.True(second.TicketCreated);
        Assert.NotEqual(first.TicketId, second.TicketId);
    }

    [Fact]
    public async Task EmailTicketReply_SendsThroughTheEmailProvider_WithTheTicketNumber()
    {
        var (app, email, _) = CreateApp();
        var from = NewEmail();
        var inbound = await ProcessAsync(app.Services, EmailMessage(from, "Printer broken"));

        TicketMessageResponse reply;
        using (var scope = app.Services.CreateScope())
        {
            reply = await scope.ServiceProvider.GetRequiredService<ITicketMessageService>()
                .AddAsync(inbound.TicketId!.Value, new AddTicketMessageRequest("We are on it.", false), CancellationToken.None);
        }

        var sent = Assert.Single(email.Sent);
        var (ticket, _) = await LoadAsync(app.Services, inbound.TicketId.Value);
        Assert.Equal((from, "We are on it."), (sent.Recipient, sent.Body));
        Assert.Equal($"Re: Printer broken [{Ticket.FormatNumber(ticket.Number)}]", sent.Subject);
        Assert.Equal("sent", reply.DeliveryStatus);
        using var check = app.Services.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<CrmDbContext>().TicketMessages.AsNoTracking().SingleAsync(m => m.Id == reply.Id);
        Assert.Equal(("provider-id-1", MessageDeliveryStatus.Sent), (stored.ExternalMessageId, stored.DeliveryStatus));
    }

    [Fact]
    public async Task EmailTicketReply_WhenSmtpFails_IsFailed_AndSentAfterTheRetry()
    {
        var (app, email, _) = CreateApp();
        email.Next = ChannelSendResult.Fail("421 Service not available");
        var inbound = await ProcessAsync(app.Services, EmailMessage(NewEmail(), "Retry me"));

        TicketMessageResponse reply;
        using (var scope = app.Services.CreateScope())
        {
            reply = await scope.ServiceProvider.GetRequiredService<ITicketMessageService>()
                .AddAsync(inbound.TicketId!.Value, new AddTicketMessageRequest("Hello", false), CancellationToken.None);
        }

        Assert.Equal("failed", reply.DeliveryStatus);

        email.Next = ChannelSendResult.Ok("<ok@azm.example>");
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IChannelSender>().RetryDueAsync(CancellationToken.None);
        }

        using var check = app.Services.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<CrmDbContext>().TicketMessages.AsNoTracking().SingleAsync(m => m.Id == reply.Id);
        Assert.Equal(MessageDeliveryStatus.Sent, stored.DeliveryStatus);
    }

    [Fact]
    public async Task WhatsAppTicketReply_InsideTheWindow_SendsThroughTheCloudProvider()
    {
        var (app, _, whatsApp) = CreateApp();
        var number = NewNumber();
        var inbound = await ProcessAsync(app.Services, WhatsAppMessage(number));

        using var scope = app.Services.CreateScope();
        var reply = await scope.ServiceProvider.GetRequiredService<ITicketMessageService>()
            .AddAsync(inbound.TicketId!.Value, new AddTicketMessageRequest("Shipping today.", false), CancellationToken.None);

        var sent = Assert.Single(whatsApp.Sent);
        Assert.Equal((number, "Shipping today.", (string?)null), (sent.Recipient, sent.Body, sent.TemplateName));
        Assert.Equal("sent", reply.DeliveryStatus);
    }

    [Fact]
    public async Task WhatsAppTicketReply_OutsideTheWindow_IsRejected_NothingSaved_UnlessATemplateIsUsed()
    {
        var (app, _, whatsApp) = CreateApp();
        var inbound = await ProcessAsync(app.Services, WhatsAppMessage(NewNumber()));
        factory.Time.Advance(TimeSpan.FromHours(25));

        using (var scope = app.Services.CreateScope())
        {
            var error = await Assert.ThrowsAsync<ValidationException>(() => scope.ServiceProvider
                .GetRequiredService<ITicketMessageService>()
                .AddAsync(inbound.TicketId!.Value, new AddTicketMessageRequest("Late answer", false), CancellationToken.None));
            Assert.Contains("body", error.Errors.Keys);
        }

        var (_, afterRejected) = await LoadAsync(app.Services, inbound.TicketId!.Value);
        Assert.Single(afterRejected); // only the customer's message
        Assert.Empty(whatsApp.Sent);

        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITicketMessageService>()
                .AddAsync(inbound.TicketId.Value, new AddTicketMessageRequest("Late answer", false, "order_update"), CancellationToken.None);
        }

        Assert.Equal("order_update", Assert.Single(whatsApp.Sent).TemplateName);
    }

    [Fact]
    public async Task WhatsAppStatusWebhook_Failed_MarksTheTicketReplyFailed()
    {
        var (app, _, whatsApp) = CreateApp();
        whatsApp.Next = ChannelSendResult.Ok("wamid.OUTBOUND1");
        var inbound = await ProcessAsync(app.Services, WhatsAppMessage(NewNumber()));
        TicketMessageResponse reply;
        using (var scope = app.Services.CreateScope())
        {
            reply = await scope.ServiceProvider.GetRequiredService<ITicketMessageService>()
                .AddAsync(inbound.TicketId!.Value, new AddTicketMessageRequest("Hi", false), CancellationToken.None);
        }

        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IChannelSender>()
                .ApplyDeliveryStatusAsync("wamid.OUTBOUND1", DeliveryStatus.Failed, "Undeliverable", CancellationToken.None);
        }

        using var check = app.Services.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<CrmDbContext>().TicketMessages.AsNoTracking().SingleAsync(m => m.Id == reply.Id);
        Assert.Equal(MessageDeliveryStatus.Failed, stored.DeliveryStatus);
    }
}
