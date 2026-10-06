using Crm.Application.Channels;
using Crm.Domain.Channels;

namespace Crm.Infrastructure.Channels.WhatsApp;

/// <summary>The WhatsApp channel: sends replies through the Cloud API. Settings: <c>Channels:WhatsApp</c>.</summary>
public sealed class WhatsAppChannelProvider(WhatsAppChannelOptions options, WhatsAppCloudClient client) : IChannelProvider
{
    public ChannelKind Channel => ChannelKind.WhatsApp;

    public bool IsConfigured => options.IsConfigured;

    public async Task<ChannelSendResult> SendAsync(OutboundChannelMessage message, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return ChannelSendResult.Fail(ChannelText.WhatsAppNotConfigured);
        }

        try
        {
            var id = await client.SendAsync(message.Recipient, message.Body, message.TemplateName, cancellationToken);
            return ChannelSendResult.Ok(id);
        }
        catch (Exception exception) when (exception is WhatsAppApiException or HttpRequestException or TaskCanceledException)
        {
            // API error, network error or timeout: logged on the message and retried with back-off.
            return ChannelSendResult.Fail(exception.Message);
        }
    }
}
