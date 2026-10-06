namespace Crm.Domain.Notifications;

/// <summary>What a <see cref="Notification"/> is about; stored by name.</summary>
public enum NotificationType
{
    /// <summary>80 % of the response time has passed without a response (CRM-22).</summary>
    SlaWarning,

    /// <summary>The ticket escalated after an SLA breach (CRM-22).</summary>
    SlaEscalation,

    /// <summary>A ticket was assigned to the user (CRM-28).</summary>
    Assignment,

    /// <summary>A task of the user is due (CRM-31).</summary>
    TaskReminder,

    /// <summary>The user was @mentioned in an internal note (CRM-33).</summary>
    Mention,
}

/// <summary>
/// An in-app notification for one user (CRM-28). Notifications for a role (e.g. the supervisors) are expanded to one row
/// per active user when they are created, so every user has their own read state. <see cref="DedupKey"/> identifies the
/// event: a user never gets two rows with the same key.
/// </summary>
public sealed class Notification
{
    /// <summary>Role that receives escalations (there is no team model yet).</summary>
    public const string SupervisorRole = "Supervisor";

    public const int TextMaxLength = 500;
    public const int DedupKeyMaxLength = 200;

    private Notification()
    {
        // EF Core materializes notifications through this constructor.
    }

    public Guid Id { get; private set; }

    /// <summary>The user the notification is for. Null only for rows stored by CRM-22 for a role (ignored since CRM-28).</summary>
    public Guid? RecipientUserId { get; private set; }

    /// <summary>The ticket it is about, if any (the notification opens it).</summary>
    public Guid? TicketId { get; private set; }

    public NotificationType Type { get; private set; }

    /// <summary>The escalation level for escalations, 0 otherwise.</summary>
    public int Level { get; private set; }

    /// <summary>Free text of the event (task title, mention excerpt), if any.</summary>
    public string? Text { get; private set; }

    /// <summary>Identifies the event, e.g. "sla-escalation:{ticketId}:2"; unique per user.</summary>
    public string DedupKey { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    /// <summary>A notification for one user.</summary>
    public static Notification ForUser(
        Guid userId, Guid? ticketId, NotificationType type, int level, string dedupKey, string? text, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A notification needs a user.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(dedupKey);
        if (dedupKey.Length > DedupKeyMaxLength)
        {
            throw new ArgumentException($"A dedup key has at most {DedupKeyMaxLength} characters.", nameof(dedupKey));
        }

        var trimmed = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = userId,
            TicketId = ticketId,
            Type = type,
            Level = level,
            Text = trimmed is { Length: > TextMaxLength } ? trimmed[..TextMaxLength] : trimmed,
            DedupKey = dedupKey,
            CreatedAt = utcNow,
        };
    }

    /// <summary>Marks it read; false (the first read time stays) when it was read before.</summary>
    public bool MarkRead(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        if (ReadAt is not null)
        {
            return false;
        }

        ReadAt = utcNow;
        return true;
    }
}
