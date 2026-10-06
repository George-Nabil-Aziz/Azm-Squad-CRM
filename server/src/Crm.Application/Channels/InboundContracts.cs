using Crm.Domain.Channels;

namespace Crm.Application.Channels;

/// <summary>
/// A customer message as any channel delivers it. <c>ExternalId</c>: email Message-Id / WhatsApp message id;
/// <c>From</c>: email address or phone number (E.164 for WhatsApp); <c>ReceivedAt</c> UTC.
/// </summary>
public sealed record InboundChannelMessage(
    ChannelKind Channel, string ExternalId, string From, string? FromName, string? Subject, string Body, DateTime ReceivedAt);

/// <summary>
/// What happened to an inbound message: <c>Duplicate</c> = already processed (ignored); otherwise the stored message,
/// the matched or created customer (null when the sender is unusable) and the ticket number from the subject.
/// </summary>
public sealed record InboundResult(bool Duplicate, Guid? ReceivedMessageId, Guid? CustomerId, bool NewCustomer, int? TicketNumber)
{
    public static InboundResult Ignored { get; } = new(true, null, null, false, null);
}
