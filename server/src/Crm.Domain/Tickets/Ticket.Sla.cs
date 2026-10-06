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

    /// <summary>Copies the due times of the policy of this ticket's priority, counted from <see cref="CreatedAt"/>.</summary>
    public void ApplySla(SlaPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Priority != Priority)
        {
            throw new ArgumentException("The SLA policy must be the one of the ticket's priority.", nameof(policy));
        }

        ResponseDueAt = policy.ResponseDueAt(CreatedAt);
        ResolutionDueAt = policy.ResolutionDueAt(CreatedAt);
    }

    /// <summary>
    /// Changes the priority and recalculates the due times from <see cref="CreatedAt"/> with the new priority's current
    /// policy (null = no policy: the due times stay). The same priority changes nothing.
    /// </summary>
    public void ChangePriority(TicketPriority priority, SlaPolicy? policy, DateTime utcNow)
    {
        EnsureUtcTime(utcNow);
        if (priority == Priority)
        {
            return;
        }

        Priority = priority;
        if (policy is not null)
        {
            ApplySla(policy);
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
