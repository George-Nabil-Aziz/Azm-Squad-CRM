namespace Crm.Domain.Customers;

/// <summary>
/// One entry of a customer's timeline: something that happened with the customer (profile change, note, attachment,
/// ticket, message). Written once by the feature that did it and never changed. The text is not stored: the
/// <see cref="Event"/> code is translated by the client; <see cref="Details"/> holds a short value (name, number,
/// excerpt) shown next to it.
/// </summary>
public sealed class CustomerInteraction
{
    public const int EventMaxLength = 64;
    public const int DetailsMaxLength = 500;

    private CustomerInteraction()
    {
        // EF Core materializes entries through this constructor.
    }

    /// <summary>Set by the database (identity): orders entries written at the same time.</summary>
    public long Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public InteractionType Type { get; private set; }

    /// <summary>What happened, e.g. <see cref="InteractionEvents.CustomerCreated"/>.</summary>
    public string Event { get; private set; } = string.Empty;

    /// <summary>Short text shown with the entry (trimmed, at most <see cref="DetailsMaxLength"/> characters), or null.</summary>
    public string? Details { get; private set; }

    /// <summary>Id of the ticket / message / note / attachment the entry is about, if any.</summary>
    public Guid? SourceId { get; private set; }

    /// <summary>The staff user who did it; null for system and channel events.</summary>
    public Guid? ActorId { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public static CustomerInteraction Create(
        Guid customerId, InteractionType type, string @event, string? details, Guid? sourceId, Guid? actorId, DateTime utcNow)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("The customer id is required.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(@event);
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        return new CustomerInteraction
        {
            CustomerId = customerId,
            Type = type,
            Event = @event.Trim(),
            Details = Shorten(details),
            SourceId = sourceId,
            ActorId = actorId,
            OccurredAt = utcNow,
        };
    }

    private static string? Shorten(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return null;
        }

        var trimmed = details.Trim();
        return trimmed.Length <= DetailsMaxLength ? trimmed : string.Concat(trimmed.AsSpan(0, DetailsMaxLength - 1), "…");
    }
}
