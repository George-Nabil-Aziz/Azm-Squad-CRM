namespace Crm.Domain.Sla;

/// <summary>
/// One SLA event of a ticket (CRM-21): a breach (later also warnings and escalations, CRM-22). At most one row per
/// (ticket, type, level): the unique index makes the monitor job idempotent.
/// </summary>
public sealed class TicketSlaEvent
{
    private TicketSlaEvent()
    {
        // EF Core materializes events through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public SlaEventType Type { get; private set; }

    /// <summary>0 for breaches; the escalation level for escalations (CRM-22).</summary>
    public int Level { get; private set; }

    /// <summary>The due time that was missed, when the event is a breach.</summary>
    public DateTime? DueAt { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public static TicketSlaEvent Create(Guid ticketId, SlaEventType type, int level, DateTime? dueAt, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        return new TicketSlaEvent
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Type = type,
            Level = level,
            DueAt = dueAt,
            OccurredAt = utcNow,
        };
    }
}
