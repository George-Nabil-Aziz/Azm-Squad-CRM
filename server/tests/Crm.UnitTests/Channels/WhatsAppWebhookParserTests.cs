using System.Text;
using Crm.Application.Channels.WhatsApp;
using Crm.Domain.Channels;

namespace Crm.UnitTests.Channels;

public class WhatsAppWebhookParserTests
{
    private const long Timestamp = 1_791_273_600; // 2026-10-06 08:00:00 UTC

    private static readonly DateTime At = DateTimeOffset.FromUnixTimeSeconds(Timestamp).UtcDateTime;

    [Fact]
    public void Parse_TextMessage_ReadsIdNumberNameTextAndTime()
    {
        var payload = WhatsAppWebhookParser.Parse(WhatsAppPayloads.Text("wamid.A1", "966501234567", "Nour", "Hello, my order?", Timestamp));

        var message = Assert.Single(payload.Messages);
        Assert.Equal(ChannelKind.WhatsApp, message.Channel);
        Assert.Equal("wamid.A1", message.ExternalId);
        Assert.Equal("+966501234567", message.From);
        Assert.Equal("Nour", message.FromName);
        Assert.Null(message.Subject);
        Assert.Equal("Hello, my order?", message.Body);
        Assert.Equal(At, message.ReceivedAt);
        Assert.Equal(DateTimeKind.Utc, message.ReceivedAt.Kind);
        Assert.Empty(payload.Statuses);
    }

    [Fact]
    public void Parse_MessageWithoutProfile_HasNoName() =>
        Assert.Null(Assert.Single(WhatsAppWebhookParser.Parse(
            WhatsAppPayloads.Text("wamid.A2", "966501234567", null, "Hi", Timestamp)).Messages).FromName);

    [Fact]
    public void Parse_NonTextMessage_IsKeptWithItsType() =>
        Assert.Equal("[image message]", Assert.Single(WhatsAppWebhookParser.Parse(
            WhatsAppPayloads.Image("wamid.A3", "966501234567", Timestamp)).Messages).Body);

    [Theory]
    [InlineData("sent", DeliveryStatus.Sent)]
    [InlineData("delivered", DeliveryStatus.Delivered)]
    [InlineData("read", DeliveryStatus.Read)]
    public void Parse_Status_ReadsIdStatusAndTime(string status, DeliveryStatus expected)
    {
        var payload = WhatsAppWebhookParser.Parse(WhatsAppPayloads.Status("wamid.OUT1", status, Timestamp));

        var update = Assert.Single(payload.Statuses);
        Assert.Equal(("wamid.OUT1", expected, At, (string?)null), (update.MessageId, update.Status, update.Timestamp, update.Error));
        Assert.Empty(payload.Messages);
    }

    [Fact]
    public void Parse_FailedStatus_ReadsTheError()
    {
        var update = Assert.Single(WhatsAppWebhookParser.Parse(
            WhatsAppPayloads.Status("wamid.OUT2", "failed", Timestamp, "Re-engagement message")).Statuses);

        Assert.Equal((DeliveryStatus.Failed, "Re-engagement message"), (update.Status, update.Error));
    }

    [Fact]
    public void Parse_UnknownStatus_IsSkipped() =>
        Assert.Empty(WhatsAppWebhookParser.Parse(WhatsAppPayloads.Status("wamid.OUT3", "deleted", Timestamp)).Statuses);

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"object\":\"page\"}")]
    [InlineData("{\"entry\":[{\"changes\":[{\"value\":{\"messages\":[{\"type\":\"text\"}]}}]}]}")]
    public void Parse_InvalidOrUnknownPayload_IsEmpty(string body)
    {
        var payload = WhatsAppWebhookParser.Parse(Encoding.UTF8.GetBytes(body));

        Assert.Empty(payload.Messages);
        Assert.Empty(payload.Statuses);
    }
}
