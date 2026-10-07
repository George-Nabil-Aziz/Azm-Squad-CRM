namespace Crm.Domain.Chat;

/// <summary>
/// A live chat between a website visitor and an agent (CRM-56). Waiting → Active (an agent accepts) → Ended; the
/// transcript becomes a ticket when it ends. The visitor proves who they are with a secret token, only its hash is stored.
/// </summary>
public sealed class ChatSession
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int TokenHashLength = 64;

    private readonly List<ChatMessage> _messages = [];

    private ChatSession()
    {
        // EF Core materializes sessions through this constructor.
    }

    public Guid Id { get; private set; }

    public string VisitorName { get; private set; } = string.Empty;

    /// <summary>Lower case; used to match or create the customer when the chat ends.</summary>
    public string VisitorEmail { get; private set; } = string.Empty;

    /// <summary>SHA-256 (hex) of the visitor token.</summary>
    public string VisitorTokenHash { get; private set; } = string.Empty;

    public ChatStatus Status { get; private set; }

    public Guid? AgentId { get; private set; }

    public string? AgentName { get; private set; }

    public DateTime StartedAt { get; private set; }

    public DateTime? AcceptedAt { get; private set; }

    public DateTime? EndedAt { get; private set; }

    /// <summary>The ticket the transcript was saved as (set when the chat ended).</summary>
    public Guid? TicketId { get; private set; }

    /// <summary>Display number of that ticket ("TKT-000001").</summary>
    public string? TicketNumber { get; private set; }

    public IReadOnlyList<ChatMessage> Messages => _messages;

    public static ChatSession Start(string visitorName, string visitorEmail, string visitorTokenHash, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(visitorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(visitorEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(visitorTokenHash);
        EnsureUtc(utcNow);
        return new ChatSession
        {
            Id = Guid.NewGuid(),
            VisitorName = Cut(visitorName.Trim(), NameMaxLength),
            VisitorEmail = Cut(visitorEmail.Trim().ToLowerInvariant(), EmailMaxLength),
            VisitorTokenHash = visitorTokenHash,
            Status = ChatStatus.Waiting,
            StartedAt = utcNow,
        };
    }

    /// <summary>An agent takes a waiting chat. Throws <see cref="InvalidOperationException"/> in any other status.</summary>
    public void Accept(Guid agentId, string agentName, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (Status != ChatStatus.Waiting)
        {
            throw new InvalidOperationException("Only a waiting chat can be accepted.");
        }

        Status = ChatStatus.Active;
        AgentId = agentId;
        AgentName = string.IsNullOrWhiteSpace(agentName) ? null : Cut(agentName.Trim(), NameMaxLength);
        AcceptedAt = utcNow;
    }

    public ChatMessage AddMessage(ChatSender sender, string senderName, string body, DateTime utcNow)
    {
        if (Status == ChatStatus.Ended)
        {
            throw new InvalidOperationException("The chat has ended.");
        }

        var message = ChatMessage.Create(Id, sender, senderName, body, utcNow);
        _messages.Add(message);
        return message;
    }

    public void End(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (Status == ChatStatus.Ended)
        {
            throw new InvalidOperationException("The chat has already ended.");
        }

        Status = ChatStatus.Ended;
        EndedAt = utcNow;
    }

    public void LinkTicket(Guid ticketId, string ticketNumber)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket id is required.", nameof(ticketId));
        }

        TicketId = ticketId;
        TicketNumber = ticketNumber;
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
