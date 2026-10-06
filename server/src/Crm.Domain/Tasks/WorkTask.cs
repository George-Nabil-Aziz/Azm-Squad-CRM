namespace Crm.Domain.Tasks;

/// <summary>
/// A follow-up of one agent (CRM-31) with a due time (UTC) and an optional ticket. Done tasks leave the open list;
/// the reminder is sent once when the due time has come (<see cref="ReminderSentAt"/>).
/// </summary>
public sealed class WorkTask
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private WorkTask()
    {
        // EF Core materializes tasks through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime DueAt { get; private set; }

    public Guid? TicketId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public DateTime? ReminderSentAt { get; private set; }

    public bool IsDone => CompletedAt is not null;

    /// <summary>A new open task. The title is required; the due time must be UTC and later than <paramref name="utcNow"/>.</summary>
    public static WorkTask Create(Guid ownerId, string title, string? description, DateTime dueAt, Guid? ticketId, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc || dueAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Times must be UTC (DateTimeKind.Utc).");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (dueAt <= utcNow)
        {
            throw new ArgumentException("The due time must be in the future.", nameof(dueAt));
        }

        return new WorkTask
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DueAt = dueAt,
            TicketId = ticketId,
            CreatedAt = utcNow,
        };
    }

    /// <summary>Marks it done; false when it already was.</summary>
    public bool MarkDone(DateTime utcNow)
    {
        if (CompletedAt is not null)
        {
            return false;
        }

        CompletedAt = utcNow;
        return true;
    }

    /// <summary>Records that the reminder was sent; false when it already was.</summary>
    public bool MarkReminderSent(DateTime utcNow)
    {
        if (ReminderSentAt is not null)
        {
            return false;
        }

        ReminderSentAt = utcNow;
        return true;
    }
}
