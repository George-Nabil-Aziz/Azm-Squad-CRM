using Crm.Domain.Channels;

namespace Crm.Application.Channels;

/// <summary>
/// Sends messages through one external channel (CLAUDE.md: every channel implements this interface). Implementations
/// live in Crm.Infrastructure (SMTP via MailKit, WhatsApp Cloud API); tests use fakes. Callers never use a provider
/// directly but go through <see cref="IChannelSender"/>, which logs every attempt and retries failures.
/// </summary>
public interface IChannelProvider
{
    ChannelKind Channel { get; }

    /// <summary>False when the settings (host, token, …) are missing; sending then fails with "not configured".</summary>
    bool IsConfigured { get; }

    /// <summary>Sends the message. Expected failures (server down, rejected) are returned, not thrown.</summary>
    Task<ChannelSendResult> SendAsync(OutboundChannelMessage message, CancellationToken cancellationToken);
}
