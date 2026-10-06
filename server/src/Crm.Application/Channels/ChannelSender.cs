using Crm.Domain.Channels;

namespace Crm.Application.Channels;

/// <summary>Sends through the channel providers, logs every message and retries failed ones.</summary>
public sealed class ChannelSender(
    IEnumerable<IChannelProvider> providers,
    IOutboundMessageRepository messages,
    TimeProvider timeProvider) : IChannelSender
{
    /// <summary>How many due messages one retry run sends at most.</summary>
    public const int RetryBatchSize = 20;

    private readonly IReadOnlyList<IChannelProvider> _providers = [.. providers];

    public async Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reply);

        var message = OutboundMessage.Create(
            reply.Channel, reply.Recipient, reply.Subject, reply.Body, reply.TemplateName, reply.SourceId, UtcNow());
        messages.Add(message);
        await AttemptAsync(message, cancellationToken);
        await messages.SaveChangesAsync(cancellationToken);

        return ToResponse(message);
    }

    public async Task<int> RetryDueAsync(CancellationToken cancellationToken)
    {
        var due = await messages.ListDueForRetryAsync(UtcNow(), RetryBatchSize, cancellationToken);
        foreach (var message in due)
        {
            await AttemptAsync(message, cancellationToken);
        }

        if (due.Count > 0)
        {
            await messages.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    public ChannelStatusResponse GetStatus() =>
        new(new ChannelState(IsConfigured(ChannelKind.Email)), new ChannelState(IsConfigured(ChannelKind.WhatsApp)));

    private bool IsConfigured(ChannelKind channel) => Provider(channel)?.IsConfigured == true;

    private IChannelProvider? Provider(ChannelKind channel) => _providers.FirstOrDefault(p => p.Channel == channel);

    /// <summary>One send attempt; every outcome ends as Sent or Failed on the message (nothing is thrown, except cancellation).</summary>
    private async Task AttemptAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var provider = Provider(message.Channel);
        if (provider is null)
        {
            message.MarkFailed(ChannelText.ProviderMissing, UtcNow());
            return;
        }

        if (!provider.IsConfigured)
        {
            message.MarkFailed(
                message.Channel == ChannelKind.Email ? ChannelText.EmailNotConfigured : ChannelText.WhatsAppNotConfigured,
                UtcNow());
            return;
        }

        ChannelSendResult result;
        try
        {
            result = await provider.SendAsync(
                new OutboundChannelMessage(message.Id, message.Recipient, message.Subject, message.Body, message.TemplateName),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            result = ChannelSendResult.Fail(exception.Message);
        }

        if (result.Succeeded)
        {
            message.MarkSent(result.ProviderMessageId, UtcNow());
        }
        else
        {
            message.MarkFailed(result.Error ?? string.Empty, UtcNow());
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    internal static OutboundMessageResponse ToResponse(OutboundMessage message) =>
        new(
            message.Id,
            ChannelValues.ChannelName(message.Channel),
            message.Recipient,
            ChannelValues.StatusName(message.Status),
            message.Attempts,
            message.NextAttemptAt,
            message.LastError,
            message.SourceId,
            message.CreatedAt,
            message.UpdatedAt);
}
