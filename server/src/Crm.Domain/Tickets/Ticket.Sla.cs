using Crm.Domain.Sla;

namespace Crm.Domain.Tickets;

/// <summary>
/// SLA part of a ticket (CRM-20): due times copied from the <see cref="SlaPolicy"/> of the priority when the ticket is
/// created (and when its priority changes), so later policy changes never move them; and the two targets they measure —
/// the first agent response (set by CRM-15 replies) and the resolution (set by CRM-17 status changes).
/// </summary>
public sealed partial class Ticket
{
    /// <summary>When the first response is due (UTC); null for tickets created before SLA timers existed.</summary>
    public DateTime? ResponseDueAt { get; private set; }

    /// <summary>When the ticket must be resolved (UTC); null for tickets created before SLA timers existed.</summary>
    public DateTime? ResolutionDueAt { get; private set; }

    /// <summary>The first agent response (UTC); set once by <see cref="MarkFirstResponse"/> (CRM-15).</summary>
    public DateTime? FirstResponseAt { get; private set; }

    /// <summary>When the ticket was resolved (UTC); set by <see cref="MarkResolved"/>, cleared by <see cref="Reopen"/> (CRM-17).</summary>
    public DateTime? ResolvedAt { get; private set; }

    /// <summary>The response target was missed and the SLA job flagged it (CRM-21); never cleared.</summary>
    public bool ResponseBreached { get; private set; }

    /// <summary>The resolution target was missed and the SLA job flagged it (CRM-21); never cleared.</summary>
    public bool ResolutionBreached { get; private set; }

    /// <summary>
    /// True when the response is late at <paramref name="utcNow"/>: no first response and the due time has come, or the
    /// first response came after the due time. Never true without a due time.
    /// </summary>
    public bool IsResponseBreachedAt(DateTime utcNow) =>
        ResponseDueAt is { } due && (FirstResponseAt is { } answered ? answered > due : due <= utcNow);

    /// <summary>The same rule for the resolution (<see cref="ResolvedAt"/> after the due time, or still unresolved past it).</summary>
    public bool IsResolutionBreachedAt(DateTime utcNow) =>
        ResolutionDueAt is { } due && (ResolvedAt is { } resolved ? resolved > due : due <= utcNow);

    /// <summary>Part of the response window after which the assignee is warned (CRM-22): 80 %.</summary>
    public const double WarningFraction = 0.8;

    /// <summary>When 80 % of the response window has passed (UTC); null for tickets created before CRM-22.</summary>
    public DateTime? ResponseWarningAt { get; private set; }

    /// <summary>When the "response is due soon" warning was sent; set once.</summary>
    public DateTime? ResponseWarnedAt { get; private set; }

    /// <summary>How often the ticket escalated (0 = never); each SLA breach raises it by one, never repeating a level.</summary>
    public int EscalationLevel { get; private set; }

    /// <summary>When the ticket last escalated (UTC).</summary>
    public DateTime? EscalatedAt { get; private set; }

    /// <summary>
    /// Marks the response warning as sent when 80 % of the response window has passed but the due time has not, nobody
    /// answered yet and the ticket is not resolved. False (and nothing changes) otherwise, and after the first warning.
    /// </summary>
    public bool TryWarnResponse(DateTime utcNow)
    {
        EnsureUtcTime(utcNow);
        if (ResponseWarningAt is not { } warningAt || ResponseWarnedAt is not null || FirstResponseAt is not null
            || ResolvedAt is not null || utcNow < warningAt || (ResponseDueAt is { } due && utcNow >= due))
        {
            return false;
        }

        ResponseWarnedAt = utcNow;
        return true;
    }

    /// <summary>Raises the escalation level by one and returns it. A resolved ticket does not escalate.</summary>
    public int Escalate(DateTime utcNow)
    {
        EnsureUtcTime(utcNow);
        if (ResolvedAt is not null)
        {
            throw new InvalidOperationException("A resolved ticket cannot be escalated.");
        }

        EscalatedAt = utcNow;
        return ++EscalationLevel;
    }

    /// <summary>Flags the response breach; false when it was flagged before.</summary>
    public bool MarkResponseBreached()
    {
        if (ResponseBreached)
        {
            return false;
        }

        ResponseBreached = true;
        return true;
    }

    /// <summary>Flags the resolution breach; false when it was flagged before.</summary>
    public bool MarkResolutionBreached()
    {
        if (ResolutionBreached)
        {
            return false;
        }

        ResolutionBreached = true;
        return true;
    }

    /// <summary>Copies the due times of the policy of this ticket's priority, counted from <see cref="CreatedAt"/>.</summary>
    public void ApplySla(SlaPolicy policy, BusinessCalendar? calendar = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Priority != Priority)
        {
            throw new ArgumentException("The SLA policy must be the one of the ticket's priority.", nameof(policy));
        }

        ResponseDueAt = policy.ResponseDueAt(CreatedAt, calendar);
        ResolutionDueAt = policy.ResolutionDueAt(CreatedAt, calendar);
        // 80 % of the response window: of the real time (24/7) or of the business minutes (business hours, CRM-35).
        ResponseWarningAt = calendar is null
            ? CreatedAt + (ResponseDueAt.Value - CreatedAt) * WarningFraction
            : calendar.AddBusinessMinutes(CreatedAt, policy.ResponseMinutes * WarningFraction);
    }

    /// <summary>
    /// Changes the priority and recalculates the due times from <see cref="CreatedAt"/> with the new priority's current
    /// policy (null = no policy: the due times stay). The same priority changes nothing.
    /// </summary>
    public void ChangePriority(TicketPriority priority, SlaPolicy? policy, DateTime utcNow, BusinessCalendar? calendar = null)
    {
        EnsureUtcTime(utcNow);
        if (priority == Priority)
        {
            return;
        }

        Priority = priority;
        if (policy is not null)
        {
            ApplySla(policy, calendar);
        }

        UpdatedAt = utcNow;
    }

    /// <summary>Records the first agent response; later responses keep the first time.</summary>
    public void MarkFirstResponse(DateTime utcNow)
    {
        EnsureUtcTime(utcNow);
        FirstResponseAt ??= utcNow;
    }

    /// <summary>Records the resolution time (the latest one when resolved again after a reopen).</summary>
    public void MarkResolved(DateTime utcNow)
    {
        EnsureUtcTime(utcNow);
        ResolvedAt = utcNow;
    }

    /// <summary>The ticket is open again: the resolution timer runs again.</summary>
    public void Reopen() => ResolvedAt = null;

    private static void EnsureUtcTime(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
