using Crm.Domain.Tickets;

namespace Crm.Domain.Ai;

/// <summary>
/// The AI category / priority suggestion of a ticket (CRM-52): one row per ticket, with the confidence (0..1), whether it was
/// applied automatically, and the latest agent override (a change to a value that differs from the suggestion).
/// Times are UTC and come from the caller.
/// </summary>
public sealed class TicketAiClassification
{
    private TicketAiClassification()
    {
        // EF Core materializes classifications through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid? SuggestedCategoryId { get; private set; }

    public TicketPriority SuggestedPriority { get; private set; }

    public double Confidence { get; private set; }

    /// <summary>The suggested category was set on the ticket automatically.</summary>
    public bool CategoryApplied { get; private set; }

    /// <summary>The suggested priority was set on the ticket automatically.</summary>
    public bool PriorityApplied { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? CategoryOverriddenAt { get; private set; }

    public Guid? CategoryOverriddenTo { get; private set; }

    public DateTime? PriorityOverriddenAt { get; private set; }

    public TicketPriority? PriorityOverriddenTo { get; private set; }

    public Guid? OverriddenById { get; private set; }

    public bool IsApplied => CategoryApplied || PriorityApplied;

    public static TicketAiClassification Create(
        Guid ticketId, Guid? suggestedCategoryId, TicketPriority suggestedPriority, double confidence,
        bool categoryApplied, bool priorityApplied, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A classification needs a ticket.", nameof(ticketId));
        }

        if (double.IsNaN(confidence) || confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "The confidence is between 0 and 1.");
        }

        return new TicketAiClassification
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SuggestedCategoryId = suggestedCategoryId,
            SuggestedPriority = suggestedPriority,
            Confidence = confidence,
            CategoryApplied = categoryApplied,
            PriorityApplied = priorityApplied,
            CreatedAt = utcNow,
        };
    }

    /// <summary>The category was changed. Returns true when it is now an override; going back to the suggestion clears it.</summary>
    public bool RecordCategoryChange(Guid? newCategoryId, Guid? userId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (newCategoryId == SuggestedCategoryId)
        {
            CategoryOverriddenAt = null;
            CategoryOverriddenTo = null;
            return false;
        }

        CategoryOverriddenAt = utcNow;
        CategoryOverriddenTo = newCategoryId;
        OverriddenById = userId;
        return true;
    }

    /// <summary>The priority was changed. Returns true when it is now an override; going back to the suggestion clears it.</summary>
    public bool RecordPriorityChange(TicketPriority newPriority, Guid? userId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (newPriority == SuggestedPriority)
        {
            PriorityOverriddenAt = null;
            PriorityOverriddenTo = null;
            return false;
        }

        PriorityOverriddenAt = utcNow;
        PriorityOverriddenTo = newPriority;
        OverriddenById = userId;
        return true;
    }

    private static void EnsureUtc(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }
    }
}
