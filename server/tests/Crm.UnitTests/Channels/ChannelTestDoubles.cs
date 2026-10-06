using Crm.Application.Channels;
using Crm.Domain.Channels;

namespace Crm.UnitTests.Channels;

/// <summary>A channel provider that records what it sent and answers with <see cref="NextResult"/> (or throws).</summary>
internal sealed class FakeChannelProvider(ChannelKind channel, bool isConfigured = true) : IChannelProvider
{
    public ChannelKind Channel { get; } = channel;

    public bool IsConfigured { get; set; } = isConfigured;

    public List<OutboundChannelMessage> Sent { get; } = [];

    public ChannelSendResult NextResult { get; set; } = ChannelSendResult.Ok("provider-id");

    public Exception? Throw { get; set; }

    public Task<ChannelSendResult> SendAsync(OutboundChannelMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Throw is null ? Task.FromResult(NextResult) : Task.FromException<ChannelSendResult>(Throw);
    }
}

/// <summary>Outbound messages in memory.</summary>
internal sealed class FakeOutboundMessageRepository : IOutboundMessageRepository
{
    public List<OutboundMessage> Messages { get; } = [];

    public int SaveCount { get; private set; }

    public void Add(OutboundMessage message) => Messages.Add(message);

    public Task<OutboundMessage?> FindByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken) =>
        Task.FromResult(Messages.FirstOrDefault(m => m.ProviderMessageId == providerMessageId));

    public Task<IReadOnlyList<OutboundMessage>> ListDueForRetryAsync(DateTime utcNow, int max, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OutboundMessage>>([.. Messages.Where(m => m.IsDueForRetry(utcNow)).Take(max)]);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>A clock the test sets by hand.</summary>
internal sealed class ChannelClock(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
