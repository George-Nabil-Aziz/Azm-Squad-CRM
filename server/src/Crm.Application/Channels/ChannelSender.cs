using Crm.Application.Common.Exceptions;
using Crm.Domain.Channels;

namespace Crm.Application.Channels;

/// <summary>Sends through the channel providers, logs every message and retries failed ones.</summary>
public sealed class ChannelSender(
    IEnumerable<IChannelProvider> providers,
    IOutboundMessageRepository messages,
    IReceivedMessageRepository received,
    TimeProvider timeProvider,
    IEnumerable<IChannelDeliveryObserver>? observers = null) : IChannelSender
{
    /// <summary>How many due messages one retry run sends at most.</summary>
    public const int RetryBatchSize = 20;

    private readonly IReadOnlyList<IChannelProvider> _providers = [.. providers];
    private readonly IReadOnlyList<IChannelDeliveryObserver> _observers = [.. observers ?? []];

    public async Task<OutboundMessageResponse> SendAsync(ChannelReply reply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reply);
        await EnsureCanSendAsync(reply, cancellationToken);

        var message = OutboundMessage.Create(
            reply.Channel, reply.Recipient, reply.Subject, reply.Body, reply.TemplateName, reply.SourceId, UtcNow());
        messages.Add(message);
        await AttemptAsync(message, cancellationToken);
        await messages.SaveChangesAsync(cancellationToken);

        return ToResponse(message);
    }

    public async Task<bool> ApplyDeliveryStatusAsync(
        string providerMessageId, DeliveryStatus status, string? error, CancellationToken cancellationToken)
    {
        var message = await messages.FindByProviderMessageIdAsync(providerMessageId, cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.ApplyDeliveryStatus(status, error, UtcNow());
        await NotifyAsync(message, cancellationToken);
        await messages.SaveChangesAsync(cancellationToken);
        return true;
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

    /// <summary>WhatsApp free text is allowed only within 24 hours of the customer's last message; otherwise a template is needed.</summary>
    public async Task EnsureCanSendAsync(ChannelReply reply, CancellationToken cancellationToken)
    {
        if (reply.Channel != ChannelKind.WhatsApp || !string.IsNullOrWhiteSpace(reply.TemplateName))
        {
            return;
        }

        var last = await received.LastReceivedAtAsync(ChannelKind.WhatsApp, reply.Recipient, cancellationToken);
        if (!WhatsAppWindow.IsOpen(last, UtcNow()))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["body"] = [ChannelText.WhatsAppWindowClosed] });
        }
    }

    private bool IsConfigured(ChannelKind channel) => Provider(channel)?.IsConfigured == true;

    private IChannelProvider? Provider(ChannelKind channel) => _providers.FirstOrDefault(p => p.Channel == channel);

    /// <summary>One send attempt; every outcome ends as Sent or Failed on the message (nothing is thrown, except cancellation).</summary>
    private async Task AttemptAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        await TrySendAsync(message, cancellationToken);
        await NotifyAsync(message, cancellationToken);
    }

    private async Task NotifyAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        foreach (var observer in _observers)
        {
            await observer.OnDeliveryChangedAsync(message, cancellationToken);
        }
    }

    private async Task TrySendAsync(OutboundMessage message, CancellationToken cancellationToken)
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
