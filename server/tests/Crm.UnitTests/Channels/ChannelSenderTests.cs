using Crm.Application.Channels;
using Crm.Domain.Channels;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.Channels;

public class ChannelSenderTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeChannelProvider _email = new(ChannelKind.Email);
    private readonly FakeChannelProvider _whatsApp = new(ChannelKind.WhatsApp);
    private readonly FakeOutboundMessageRepository _messages = new();
    private readonly FakeReceivedMessageRepository _received = new();
    private readonly ChannelClock _clock = new(Start);

    private ChannelSender CreateSender() => new([_email, _whatsApp], _messages, _received, _clock);

    private const string Number = "+966501234567";

    private void CustomerWroteAt(DateTime receivedAt) =>
        _received.Add(ReceivedMessage.Create(
            ChannelKind.WhatsApp, $"wamid.{Guid.NewGuid():N}", Number, null, null, "Hi", null, null, receivedAt, receivedAt));

    private static ChannelReply WhatsAppReply(string? template = null) =>
        new(ChannelKind.WhatsApp, Number, null, "Your order ships today.", template, null);

    [Fact]
    public async Task WhatsApp_FreeTextInsideTheWindow_IsSent()
    {
        CustomerWroteAt(Start.UtcDateTime.AddHours(-23));

        var result = await CreateSender().SendAsync(WhatsAppReply(), CancellationToken.None);

        Assert.Equal("sent", result.Status);
        Assert.Equal(("+966501234567", "Your order ships today.", (string?)null),
            (_whatsApp.Sent.Single().Recipient, _whatsApp.Sent.Single().Body, _whatsApp.Sent.Single().TemplateName));
    }

    [Fact]
    public async Task WhatsApp_FreeTextOutsideTheWindow_ThrowsValidation_OnBody()
    {
        CustomerWroteAt(Start.UtcDateTime.AddHours(-25));

        var error = await UiCulture.Use("en", () => Assert.ThrowsAsync<Crm.Application.Common.Exceptions.ValidationException>(
            () => CreateSender().SendAsync(WhatsAppReply(), CancellationToken.None)));

        Assert.Equal(
            "The last customer message is older than 24 hours. Send an approved template instead.",
            Assert.Single(error.Errors["body"]));
        Assert.Empty(_whatsApp.Sent);
        Assert.Empty(_messages.Messages);
    }

    [Fact]
    public async Task WhatsApp_FreeTextWithoutAnyCustomerMessage_IsBlocked() =>
        await Assert.ThrowsAsync<Crm.Application.Common.Exceptions.ValidationException>(
            () => CreateSender().SendAsync(WhatsAppReply(), CancellationToken.None));

    [Fact]
    public async Task WhatsApp_TemplateOutsideTheWindow_IsSent()
    {
        var result = await CreateSender().SendAsync(WhatsAppReply(template: "order_update"), CancellationToken.None);

        Assert.Equal("sent", result.Status);
        Assert.Equal("order_update", Assert.Single(_whatsApp.Sent).TemplateName);
    }

    [Fact]
    public async Task ApplyDeliveryStatus_UpdatesTheMessage()
    {
        _whatsApp.NextResult = ChannelSendResult.Ok("wamid.OUT1");
        var sender = CreateSender();
        await sender.SendAsync(WhatsAppReply(template: "order_update"), CancellationToken.None);

        _clock.UtcNow = Start.AddMinutes(2);
        Assert.True(await sender.ApplyDeliveryStatusAsync("wamid.OUT1", DeliveryStatus.Delivered, null, CancellationToken.None));
        Assert.True(await sender.ApplyDeliveryStatusAsync("wamid.OUT1", DeliveryStatus.Read, null, CancellationToken.None));

        var stored = Assert.Single(_messages.Messages);
        Assert.Equal(DeliveryStatus.Read, stored.Status);
        Assert.Equal(Start.UtcDateTime.AddMinutes(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task ApplyDeliveryStatus_UnknownId_IsIgnored() =>
        Assert.False(await CreateSender().ApplyDeliveryStatusAsync("wamid.UNKNOWN", DeliveryStatus.Read, null, CancellationToken.None));

    private static ChannelReply EmailReply(Guid? sourceId = null) =>
        new(ChannelKind.Email, "nour@example.com", "Re: help [TKT-000001]", "Hello Nour", null, sourceId);

    [Fact]
    public async Task Send_UsesTheProviderOfTheChannel_AndMarksSent()
    {
        var sourceId = Guid.NewGuid();

        var result = await CreateSender().SendAsync(EmailReply(sourceId), CancellationToken.None);

        var sent = Assert.Single(_email.Sent);
        Assert.Empty(_whatsApp.Sent);
        Assert.Equal("nour@example.com", sent.Recipient);
        Assert.Equal("Re: help [TKT-000001]", sent.Subject);
        Assert.Equal("Hello Nour", sent.Body);
        var stored = Assert.Single(_messages.Messages);
        Assert.Equal(sent.Id, stored.Id);
        Assert.Equal(DeliveryStatus.Sent, stored.Status);
        Assert.Equal("provider-id", stored.ProviderMessageId);
        Assert.Equal(sourceId, stored.SourceId);
        Assert.Equal("sent", result.Status);
        Assert.Equal("email", result.Channel);
        Assert.True(_messages.SaveCount >= 1);
    }

    [Fact]
    public async Task Send_WhenTheProviderFails_MarksFailed_AndSchedulesARetry()
    {
        _email.NextResult = ChannelSendResult.Fail("550 mailbox unavailable");

        var result = await CreateSender().SendAsync(EmailReply(), CancellationToken.None);

        var stored = Assert.Single(_messages.Messages);
        Assert.Equal(DeliveryStatus.Failed, stored.Status);
        Assert.Equal("550 mailbox unavailable", stored.LastError);
        Assert.Equal(Start.UtcDateTime.AddMinutes(1), stored.NextAttemptAt);
        Assert.Equal("failed", result.Status);
    }

    [Fact]
    public async Task Send_WhenTheProviderThrows_MarksFailed()
    {
        _email.Throw = new IOException("Connection refused");

        var result = await CreateSender().SendAsync(EmailReply(), CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.Equal("Connection refused", Assert.Single(_messages.Messages).LastError);
    }

    [Fact]
    public async Task Send_WhenNotConfigured_MarksFailed_WithNotConfigured()
    {
        _email.IsConfigured = false;

        var result = await UiCulture.Use("en", () => CreateSender().SendAsync(EmailReply(), CancellationToken.None));

        Assert.Empty(_email.Sent);
        Assert.Equal("failed", result.Status);
        Assert.Equal("Email is not configured.", Assert.Single(_messages.Messages).LastError);
    }

    [Fact]
    public async Task RetryDue_ResendsDueFailedMessages()
    {
        _email.NextResult = ChannelSendResult.Fail("SMTP down");
        var sender = CreateSender();
        await sender.SendAsync(EmailReply(), CancellationToken.None);

        _email.NextResult = ChannelSendResult.Ok("id-2");
        _clock.UtcNow = Start.AddMinutes(1);
        var retried = await sender.RetryDueAsync(CancellationToken.None);

        Assert.Equal(1, retried);
        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(_email.Sent[0].Id, _email.Sent[1].Id); // the same message, sent again
        var stored = Assert.Single(_messages.Messages);
        Assert.Equal(DeliveryStatus.Sent, stored.Status);
        Assert.Equal(2, stored.Attempts);
        Assert.Equal("id-2", stored.ProviderMessageId);
    }

    [Fact]
    public async Task RetryDue_SkipsMessagesNotDueYet()
    {
        _email.NextResult = ChannelSendResult.Fail("SMTP down");
        var sender = CreateSender();
        await sender.SendAsync(EmailReply(), CancellationToken.None);

        _clock.UtcNow = Start.AddSeconds(30);
        var retried = await sender.RetryDueAsync(CancellationToken.None);

        Assert.Equal(0, retried);
        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task Send_ForAChannelWithoutProvider_MarksFailed()
    {
        var sender = new ChannelSender([_email], _messages, _received, _clock);

        var result = await sender.SendAsync(
            new ChannelReply(ChannelKind.WhatsApp, "+966501234567", null, "Hi", "hello_world", null), CancellationToken.None);

        Assert.Equal("failed", result.Status);
        Assert.NotNull(Assert.Single(_messages.Messages).LastError);
    }

    [Fact]
    public void GetStatus_ReportsWhichChannelsAreConfigured()
    {
        _whatsApp.IsConfigured = false;

        var status = CreateSender().GetStatus();

        Assert.True(status.Email.Configured);
        Assert.False(status.WhatsApp.Configured);
    }
}
