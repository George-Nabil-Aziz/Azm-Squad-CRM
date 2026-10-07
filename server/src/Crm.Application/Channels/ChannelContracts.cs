using Crm.Domain.Channels;

namespace Crm.Application.Channels;

/// <summary>What a provider sends. <c>Id</c> is the <see cref="OutboundMessage"/> id (stable across retries).</summary>
public sealed record OutboundChannelMessage(Guid Id, string Recipient, string? Subject, string Body, string? TemplateName);

/// <summary>Outcome of one send attempt.</summary>
public sealed record ChannelSendResult(bool Succeeded, string? ProviderMessageId, string? Error)
{
    public static ChannelSendResult Ok(string? providerMessageId) => new(true, providerMessageId, null);

    public static ChannelSendResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// A message to a customer: <c>Recipient</c> = email address or E.164 number, <c>Subject</c> for email,
/// <c>TemplateName</c> = approved WhatsApp template (required outside the 24-hour window), <c>SourceId</c> = the ticket
/// reply it belongs to.
/// </summary>
public sealed record ChannelReply(
    ChannelKind Channel, string Recipient, string? Subject, string Body, string? TemplateName, Guid? SourceId);

/// <summary>
/// A logged outgoing message. <c>Channel</c>: "email" | "whatsapp"; <c>Status</c>: "pending" | "sent" | "delivered" |
/// "read" | "failed". Times are UTC.
/// </summary>
public sealed record OutboundMessageResponse(
    Guid Id,
    string Channel,
    string Recipient,
    string Status,
    int Attempts,
    DateTime? NextAttemptAt,
    string? LastError,
    Guid? SourceId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>GET /api/channels/status: which channels have their settings.</summary>
public sealed record ChannelStatusResponse(ChannelState Email, ChannelState WhatsApp, ChannelState? Sms = null);

public sealed record ChannelState(bool Configured);
