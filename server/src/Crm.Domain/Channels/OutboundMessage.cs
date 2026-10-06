namespace Crm.Domain.Channels;

/// <summary>
/// One message sent (or to be sent) to a customer through a channel: the send log and retry queue. A failed send is
/// retried with back-off (1, 5, 15, 60 minutes) until <see cref="MaxAttempts"/>; the delivery status is updated by
/// the provider's webhook (WhatsApp).
/// </summary>
public sealed class OutboundMessage
{
    public const int MaxAttempts = 5;
    public const int RecipientMaxLength = 320;
    public const int SubjectMaxLength = 300;
    public const int BodyMaxLength = 10_000;
    public const int TemplateNameMaxLength = 100;
    public const int ErrorMaxLength = 1000;
    public const int ProviderMessageIdMaxLength = 200;

    /// <summary>Wait before the next attempt, by the number of failed attempts so far (1 → 1 minute, …).</summary>
    private static readonly TimeSpan[] BackOff =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    private OutboundMessage()
    {
        // EF Core materializes messages through this constructor.
    }

    public Guid Id { get; private set; }

    public ChannelKind Channel { get; private set; }

    /// <summary>Email address or E.164 phone number.</summary>
    public string Recipient { get; private set; } = string.Empty;

    /// <summary>Email subject; null for WhatsApp.</summary>
    public string? Subject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    /// <summary>Approved WhatsApp template sent instead of free text (outside the 24-hour window).</summary>
    public string? TemplateName { get; private set; }

    /// <summary>What the message belongs to (the ticket reply id), if anything.</summary>
    public Guid? SourceId { get; private set; }

    public DeliveryStatus Status { get; private set; }

    /// <summary>Send attempts made so far (successful or not).</summary>
    public int Attempts { get; private set; }

    /// <summary>When a failed message is sent again; null when it is not (any more) retried.</summary>
    public DateTime? NextAttemptAt { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>Id the provider gave the message (email Message-Id, WhatsApp "wamid…"); matches status webhooks.</summary>
    public string? ProviderMessageId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static OutboundMessage Create(
        ChannelKind channel, string recipient, string? subject, string body, string? templateName, Guid? sourceId,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentNullException.ThrowIfNull(body);
        EnsureUtc(utcNow);

        return new OutboundMessage
        {
            Id = Guid.NewGuid(),
            Channel = channel,
            Recipient = Cut(recipient.Trim(), RecipientMaxLength),
            Subject = string.IsNullOrWhiteSpace(subject) ? null : Cut(subject.Trim(), SubjectMaxLength),
            Body = Cut(body, BodyMaxLength),
            TemplateName = string.IsNullOrWhiteSpace(templateName) ? null : Cut(templateName.Trim(), TemplateNameMaxLength),
            SourceId = sourceId,
            Status = DeliveryStatus.Pending,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    /// <summary>The provider accepted the message.</summary>
    public void MarkSent(string? providerMessageId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        Attempts++;
        Status = DeliveryStatus.Sent;
        ProviderMessageId = string.IsNullOrWhiteSpace(providerMessageId) ? null : Cut(providerMessageId, ProviderMessageIdMaxLength);
        NextAttemptAt = null;
        LastError = null;
        UpdatedAt = utcNow;
    }

    /// <summary>The attempt failed: keeps the error and schedules the next attempt (none after <see cref="MaxAttempts"/>).</summary>
    public void MarkFailed(string error, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        Attempts++;
        Status = DeliveryStatus.Failed;
        LastError = Cut(string.IsNullOrWhiteSpace(error) ? "Unknown error" : error, ErrorMaxLength);
        NextAttemptAt = Attempts < MaxAttempts ? utcNow + BackOff[Math.Min(Attempts, BackOff.Length) - 1] : null;
        UpdatedAt = utcNow;
    }

    /// <summary>True for a failed message whose next attempt time has come.</summary>
    public bool IsDueForRetry(DateTime utcNow) =>
        Status == DeliveryStatus.Failed && NextAttemptAt is { } next && next <= utcNow;

    /// <summary>
    /// Status reported later by the provider (WhatsApp webhook). Never moves backwards (Sent &lt; Delivered &lt; Read);
    /// <see cref="DeliveryStatus.Failed"/> always applies (with the error) and is not retried — the provider already
    /// accepted the message.
    /// </summary>
    public void ApplyDeliveryStatus(DeliveryStatus status, string? error, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (status == DeliveryStatus.Failed)
        {
            Status = DeliveryStatus.Failed;
            LastError = Cut(string.IsNullOrWhiteSpace(error) ? "Delivery failed" : error, ErrorMaxLength);
            NextAttemptAt = null;
            UpdatedAt = utcNow;
            return;
        }

        if (Status == DeliveryStatus.Failed || status <= Status)
        {
            return;
        }

        Status = status;
        UpdatedAt = utcNow;
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }

    private static string Cut(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
