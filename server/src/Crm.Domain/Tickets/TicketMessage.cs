namespace Crm.Domain.Tickets;

/// <summary>
/// One entry of a ticket's conversation: a customer message (Inbound), an agent reply (Outbound) or an internal note.
/// Written once; who wrote it and when are kept. Times are UTC and come from the caller.
/// </summary>
public sealed class TicketMessage
{
    public const int BodyMaxLength = 10_000;
    public const int ExternalMessageIdMaxLength = 255;

    private TicketMessage()
    {
        // EF Core materializes messages through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public MessageDirection Direction { get; private set; }

    /// <summary>The staff user who wrote it; null for customer (inbound) messages.</summary>
    public Guid? AuthorId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    /// <summary>The channel the message travelled through (an agent's reply uses the ticket's channel).</summary>
    public TicketChannel Channel { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>Delivery of an outbound reply through a real channel; null when nothing is delivered (notes, inbound, manual tickets).</summary>
    public MessageDeliveryStatus? DeliveryStatus { get; private set; }

    /// <summary>The id the channel gave the message (email Message-ID, WhatsApp message id), if any.</summary>
    public string? ExternalMessageId { get; private set; }

    public bool IsInternal => Direction == MessageDirection.InternalNote;

    /// <summary>An agent's reply to the customer, or (isInternal) an internal note.</summary>
    public static TicketMessage Staff(Guid ticketId, string body, bool isInternal, TicketChannel channel, Guid? authorId, DateTime utcNow)
    {
        var message = Create(ticketId, body, channel, utcNow);
        message.Direction = isInternal ? MessageDirection.InternalNote : MessageDirection.Outbound;
        message.AuthorId = authorId;
        if (!isInternal && channel != TicketChannel.Manual)
        {
            message.DeliveryStatus = MessageDeliveryStatus.Pending;
        }

        return message;
    }

    /// <summary>A message from the customer, received through a channel.</summary>
    public static TicketMessage Inbound(Guid ticketId, string body, TicketChannel channel, string? externalMessageId, DateTime utcNow)
    {
        var message = Create(ticketId, body, channel, utcNow);
        message.Direction = MessageDirection.Inbound;
        message.ExternalMessageId = externalMessageId;
        return message;
    }

    public void MarkSent(string? externalMessageId)
    {
        DeliveryStatus = MessageDeliveryStatus.Sent;
        ExternalMessageId = externalMessageId;
    }

    public void MarkFailed() => DeliveryStatus = MessageDeliveryStatus.Failed;

    private static TicketMessage Create(Guid ticketId, string body, TicketChannel channel, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var trimmed = body.Trim();
        if (trimmed.Length > BodyMaxLength)
        {
            throw new ArgumentException($"A message has at most {BodyMaxLength} characters.", nameof(body));
        }

        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        return new TicketMessage { Id = Guid.NewGuid(), TicketId = ticketId, Body = trimmed, Channel = channel, CreatedAt = utcNow };
    }
}
