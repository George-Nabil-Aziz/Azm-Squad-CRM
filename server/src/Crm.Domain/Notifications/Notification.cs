namespace Crm.Domain.Notifications;

/// <summary>What a <see cref="Notification"/> is about; stored by name.</summary>
public enum NotificationType
{
    /// <summary>80 % of the response time has passed without a response (CRM-22).</summary>
    SlaWarning,

    /// <summary>The ticket escalated after an SLA breach (CRM-22).</summary>
    SlaEscalation,
}

/// <summary>
/// A stored notification (CRM-22): for one user (<see cref="RecipientUserId"/>) or for everybody holding a role
/// (<see cref="RecipientRole"/>). Only stored for now; the in-app notification UI is Phase 2.
/// </summary>
public sealed class Notification
{
    /// <summary>Role that receives escalations (there is no team model yet).</summary>
    public const string SupervisorRole = "Supervisor";

    private Notification()
    {
        // EF Core materializes notifications through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid? RecipientUserId { get; private set; }

    public string? RecipientRole { get; private set; }

    public Guid TicketId { get; private set; }

    public NotificationType Type { get; private set; }

    /// <summary>The escalation level for escalations, 0 otherwise.</summary>
    public int Level { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ReadAt { get; private set; }

    /// <summary>A notification for one user.</summary>
    public static Notification ForUser(Guid userId, Guid ticketId, NotificationType type, int level, DateTime utcNow) =>
        Create(userId, null, ticketId, type, level, utcNow);

    /// <summary>A notification for everybody holding <paramref name="role"/>.</summary>
    public static Notification ForRole(string role, Guid ticketId, NotificationType type, int level, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return Create(null, role, ticketId, type, level, utcNow);
    }

    private static Notification Create(Guid? userId, string? role, Guid ticketId, NotificationType type, int level, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        return new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = userId,
            RecipientRole = role,
            TicketId = ticketId,
            Type = type,
            Level = level,
            CreatedAt = utcNow,
        };
    }
}
