using Crm.Domain.Tickets;

namespace Crm.Domain.Sla;

/// <summary>
/// An optional SLA override of one department and priority (CRM-61 AC 4). New tickets of the department (and priority
/// changes) use it instead of the global <see cref="SlaPolicy"/> of that priority; without a row the global one applies.
/// </summary>
public sealed class DepartmentSlaPolicy
{
    private DepartmentSlaPolicy()
    {
        // EF Core materializes overrides through this constructor.
    }

    public Guid DepartmentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public int ResponseMinutes { get; private set; }

    public int ResolutionMinutes { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static DepartmentSlaPolicy Create(
        Guid departmentId, TicketPriority priority, int responseMinutes, int resolutionMinutes, DateTime utcNow)
    {
        var policy = new DepartmentSlaPolicy { DepartmentId = departmentId, Priority = priority };
        policy.Update(responseMinutes, resolutionMinutes, utcNow);
        return policy;
    }

    /// <summary>Same rules as <see cref="SlaPolicy.Update"/>.</summary>
    public void Update(int responseMinutes, int resolutionMinutes, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        if (!SlaPolicy.AreValid(responseMinutes, resolutionMinutes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolutionMinutes),
                $"SLA times must be between 1 and {SlaPolicy.MaxMinutes} minutes and resolution >= response.");
        }

        ResponseMinutes = responseMinutes;
        ResolutionMinutes = resolutionMinutes;
        UpdatedAt = utcNow;
    }

    /// <summary>A detached global-shaped policy with these minutes, for <c>Ticket.ApplySla</c> (not tracked, never saved).</summary>
    public SlaPolicy ToPolicy() => SlaPolicy.Create(Priority, ResponseMinutes, ResolutionMinutes, UpdatedAt);
}
