namespace Crm.Domain.Channels;

/// <summary>
/// A message a customer sent through a channel (email, WhatsApp), kept exactly once: <see cref="ExternalId"/> (the
/// email Message-Id / WhatsApp message id) is unique per channel, so a message delivered twice is ignored. Linked to
/// the matched (or newly created) customer.
/// </summary>
public sealed class ReceivedMessage
{
    public const int ExternalIdMaxLength = 300;
    public const int FromMaxLength = 320;
    public const int FromNameMaxLength = 200;
    public const int SubjectMaxLength = 300;
    public const int BodyMaxLength = 10_000;

    private ReceivedMessage()
    {
        // EF Core materializes messages through this constructor.
    }

    public Guid Id { get; private set; }

    public ChannelKind Channel { get; private set; }

    /// <summary>Email Message-Id or WhatsApp message id ("wamid…").</summary>
    public string ExternalId { get; private set; } = string.Empty;

    /// <summary>Sender: lower-case email address or E.164 number.</summary>
    public string From { get; private set; } = string.Empty;

    public string? FromName { get; private set; }

    public string? Subject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    /// <summary>The ticket number found in the subject ("[TKT-000123]"), if any.</summary>
    public int? TicketNumber { get; private set; }

    /// <summary>The customer the sender was matched to (null when the sender could not be used).</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>The ticket created from or extended by this message (CRM-24 / CRM-26); null until linked.</summary>
    public Guid? TicketId { get; private set; }

    /// <summary>When the channel received the message (UTC).</summary>
    public DateTime ReceivedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static ReceivedMessage Create(
        ChannelKind channel, string externalId, string from, string? fromName, string? subject, string body,
        int? ticketNumber, Guid? customerId, DateTime receivedAt, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentNullException.ThrowIfNull(body);
        EnsureUtc(receivedAt, nameof(receivedAt));
        EnsureUtc(utcNow, nameof(utcNow));

        return new ReceivedMessage
        {
            Id = Guid.NewGuid(),
            Channel = channel,
            ExternalId = Cut(externalId.Trim(), ExternalIdMaxLength),
            From = Cut(from.Trim(), FromMaxLength),
            FromName = string.IsNullOrWhiteSpace(fromName) ? null : Cut(fromName.Trim(), FromNameMaxLength),
            Subject = string.IsNullOrWhiteSpace(subject) ? null : Cut(subject.Trim(), SubjectMaxLength),
            Body = Cut(body.Trim(), BodyMaxLength),
            TicketNumber = ticketNumber,
            CustomerId = customerId == Guid.Empty ? null : customerId,
            ReceivedAt = receivedAt,
            CreatedAt = utcNow,
        };
    }

    /// <summary>Links the message to the ticket it created or was added to.</summary>
    public void LinkToTicket(Guid ticketId)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket id is required.", nameof(ticketId));
        }

        TicketId = ticketId;
    }

    private static void EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", name);
        }
    }

    private static string Cut(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
