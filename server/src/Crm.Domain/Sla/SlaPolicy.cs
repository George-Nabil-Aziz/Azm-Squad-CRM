using Crm.Domain.Tickets;

namespace Crm.Domain.Sla;

/// <summary>
/// SLA targets of one ticket priority (CRM-19): minutes allowed until the first response and until the ticket is
/// resolved. One row per <see cref="TicketPriority"/>, seeded with <see cref="Defaults"/>. The clock runs 24/7
/// (no business hours). Tickets copy their due times when created (CRM-20), so changing a policy never moves the
/// due times of existing tickets.
/// </summary>
public sealed class SlaPolicy
{
    /// <summary>Largest allowed time (one year), so due-time arithmetic stays in range.</summary>
    public const int MaxMinutes = 525_600;

    /// <summary>Time stamped on the seeded rows (fixed, so the EF migration seed data never changes).</summary>
    public static readonly DateTime DefaultsSeededAt = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Seeded values: High 2 h / 8 h, Mid 4 h / 24 h, Low 8 h / 72 h.</summary>
    public static IReadOnlyList<(TicketPriority Priority, int ResponseMinutes, int ResolutionMinutes)> Defaults { get; } =
    [
        (TicketPriority.High, 120, 480),
        (TicketPriority.Mid, 240, 1440),
        (TicketPriority.Low, 480, 4320),
    ];

    private SlaPolicy()
    {
        // EF Core materializes policies through this constructor.
    }

    public TicketPriority Priority { get; private set; }

    public int ResponseMinutes { get; private set; }

    public int ResolutionMinutes { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static SlaPolicy Create(TicketPriority priority, int responseMinutes, int resolutionMinutes, DateTime utcNow)
    {
        var policy = new SlaPolicy { Priority = priority };
        policy.Update(responseMinutes, resolutionMinutes, utcNow);
        return policy;
    }

    /// <summary>Both times greater than 0, at most <see cref="MaxMinutes"/>, and resolution &gt;= response.</summary>
    public static bool AreValid(int responseMinutes, int resolutionMinutes) =>
        responseMinutes is > 0 and <= MaxMinutes
        && resolutionMinutes is > 0 and <= MaxMinutes
        && resolutionMinutes >= responseMinutes;

    public void Update(int responseMinutes, int resolutionMinutes, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (!AreValid(responseMinutes, resolutionMinutes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolutionMinutes),
                $"SLA times must be between 1 and {MaxMinutes} minutes and resolution >= response.");
        }

        ResponseMinutes = responseMinutes;
        ResolutionMinutes = resolutionMinutes;
        UpdatedAt = utcNow;
    }

    /// <summary>When the first response is due for a ticket started at <paramref name="startUtc"/>.</summary>
    public DateTime ResponseDueAt(DateTime startUtc) => startUtc.AddMinutes(ResponseMinutes);

    /// <summary>When the resolution is due for a ticket started at <paramref name="startUtc"/>.</summary>
    public DateTime ResolutionDueAt(DateTime startUtc) => startUtc.AddMinutes(ResolutionMinutes);

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
