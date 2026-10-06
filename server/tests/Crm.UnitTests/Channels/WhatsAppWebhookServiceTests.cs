using Crm.Application.Channels;
using Crm.Application.Channels.WhatsApp;
using Crm.Application.Common.Exceptions;

namespace Crm.UnitTests.Channels;

public class WhatsAppWebhookServiceTests
{
    private const string Secret = "test-app-secret";

    private readonly RecordingProcessor _processor = new();
    private readonly RecordingSender _sender = new();

    private WhatsAppWebhookService CreateService(string? verifyToken = "verify-me", string? secret = Secret) =>
        new(new WhatsAppChannelOptions { VerifyToken = verifyToken, AppSecret = secret }, _processor, _sender);

    private sealed class RecordingSender : IChannelSender
    {
        public List<(string Id, Crm.Domain.Channels.DeliveryStatus Status, string? Error)> Statuses { get; } = [];

        public Task<bool> ApplyDeliveryStatusAsync(
            string providerMessageId, Crm.Domain.Channels.DeliveryStatus status, string? error, CancellationToken cancellationToken)
        {
            Statuses.Add((providerMessageId, status, error));
            return Task.FromResult(true);
        }

        public Task EnsureCanSendAsync(ChannelReply reply, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> RetryDueAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ChannelStatusResponse GetStatus() => throw new NotSupportedException();
    }

    [Fact]
    public async Task Handle_AppliesDeliveryStatuses()
    {
        var body = WhatsAppPayloads.Status("wamid.OUT9", "failed", 1_791_273_600, "Message undeliverable");

        await CreateService().HandleAsync(body, WhatsAppSignature.Compute(body, Secret), CancellationToken.None);

        Assert.Equal(("wamid.OUT9", Crm.Domain.Channels.DeliveryStatus.Failed, "Message undeliverable"), Assert.Single(_sender.Statuses));
    }

    private sealed class RecordingProcessor : IInboundMessageProcessor
    {
        public List<InboundChannelMessage> Messages { get; } = [];

        public Task<InboundResult> ProcessAsync(InboundChannelMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(new InboundResult(false, Guid.NewGuid(), Guid.NewGuid(), false, null));
        }
    }

    [Fact]
    public void Verify_WithMatchingToken_ReturnsChallenge() =>
        Assert.Equal("1158201444", CreateService().VerifySubscription("subscribe", "verify-me", "1158201444"));

    [Theory]
    [InlineData("subscribe", "wrong", "123")]
    [InlineData("subscribe", null, "123")]
    [InlineData("unsubscribe", "verify-me", "123")]
    [InlineData("subscribe", "verify-me", null)]
    public void Verify_WithWrongInput_ThrowsForbidden(string? mode, string? token, string? challenge) =>
        Assert.Throws<ForbiddenException>(() => CreateService().VerifySubscription(mode, token, challenge));

    [Fact]
    public void Verify_WithoutAConfiguredToken_ThrowsForbidden() =>
        Assert.Throws<ForbiddenException>(() => CreateService(verifyToken: null).VerifySubscription("subscribe", "", "123"));

    [Fact]
    public async Task Handle_WithInvalidSignature_ThrowsUnauthorized()
    {
        var body = WhatsAppPayloads.Text("wamid.B1", "966501234567", "Nour", "Hi", 1_791_273_600);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateService().HandleAsync(body, WhatsAppSignature.Compute(body, "other-secret"), CancellationToken.None));
        Assert.Empty(_processor.Messages);
    }

    [Fact]
    public async Task Handle_WithoutAConfiguredSecret_ThrowsUnauthorized()
    {
        var body = WhatsAppPayloads.Text("wamid.B2", "966501234567", "Nour", "Hi", 1_791_273_600);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateService(secret: null).HandleAsync(body, WhatsAppSignature.Compute(body, Secret), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PassesMessagesToTheProcessor()
    {
        var body = WhatsAppPayloads.Text("wamid.B3", "966501234567", "Nour", "Hi", 1_791_273_600);

        await CreateService().HandleAsync(body, WhatsAppSignature.Compute(body, Secret), CancellationToken.None);

        Assert.Equal("wamid.B3", Assert.Single(_processor.Messages).ExternalId);
    }
}
