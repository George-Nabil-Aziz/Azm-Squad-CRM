namespace Crm.Domain.Chat;

/// <summary>One message of a live chat.</summary>
public sealed class ChatMessage
{
    public const int BodyMaxLength = 2_000;
    public const int SenderNameMaxLength = 200;

    private ChatMessage()
    {
        // EF Core materializes messages through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public ChatSender Sender { get; private set; }

    /// <summary>The visitor's or the agent's name when the message was written.</summary>
    public string SenderName { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public DateTime SentAt { get; private set; }

    internal static ChatMessage Create(Guid sessionId, ChatSender sender, string senderName, string body, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        var trimmed = body.Trim();
        var name = string.IsNullOrWhiteSpace(senderName) ? sender.ToString() : senderName.Trim();
        return new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Sender = sender,
            SenderName = name.Length <= SenderNameMaxLength ? name : name[..SenderNameMaxLength],
            Body = trimmed.Length <= BodyMaxLength ? trimmed : trimmed[..BodyMaxLength],
            SentAt = utcNow,
        };
    }
}
