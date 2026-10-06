namespace Crm.Domain.Tickets;

/// <summary>
/// One change of a ticket (audit trail): what changed, the old and the new value, who did it and when. Written once by the
/// feature that changed the ticket and never edited or deleted. Values are snapshots as text: status and priority as their
/// API codes ("open", "high"), assignee and category as names; null means none (unassigned, no category).
/// </summary>
public sealed class TicketHistoryEntry
{
    public const int ValueMaxLength = 200;

    private TicketHistoryEntry()
    {
        // EF Core materializes entries through this constructor.
    }

    /// <summary>Set by the database (identity): orders entries written at the same time.</summary>
    public long Id { get; private set; }

    public Guid TicketId { get; private set; }

    public TicketHistoryField Field { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    /// <summary>The staff user who made the change; null for the system (SLA rules, channels).</summary>
    public Guid? ChangedById { get; private set; }

    public DateTime ChangedAt { get; private set; }

    public static TicketHistoryEntry Create(
        Guid ticketId, TicketHistoryField field, string? oldValue, string? newValue, Guid? changedById, DateTime utcNow)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("The ticket id is required.", nameof(ticketId));
        }

        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        return new TicketHistoryEntry
        {
            TicketId = ticketId,
            Field = field,
            OldValue = Shorten(oldValue),
            NewValue = Shorten(newValue),
            ChangedById = changedById,
            ChangedAt = utcNow,
        };
    }

    private static string? Shorten(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= ValueMaxLength ? trimmed : trimmed[..ValueMaxLength];
    }
}
