namespace Crm.Domain.Ai;

/// <summary>
/// An agent's "useful / not useful" vote on an AI-suggested knowledge base article for a ticket (CRM-53): one row per
/// ticket, article and user; voting again replaces the vote. Times are UTC and come from the caller.
/// </summary>
public sealed class TicketSuggestionFeedback
{
    private TicketSuggestionFeedback()
    {
        // EF Core materializes feedback through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid ArticleId { get; private set; }

    public Guid UserId { get; private set; }

    public bool Useful { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static TicketSuggestionFeedback Create(Guid ticketId, Guid articleId, Guid userId, bool useful, DateTime utcNow)
    {
        if (ticketId == Guid.Empty || articleId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Feedback needs a ticket, an article and a user.");
        }

        var feedback = new TicketSuggestionFeedback
        {
            Id = Guid.NewGuid(), TicketId = ticketId, ArticleId = articleId, UserId = userId, CreatedAt = utcNow,
        };
        feedback.Set(useful, utcNow);
        return feedback;
    }

    public void Set(bool useful, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        Useful = useful;
        UpdatedAt = utcNow;
    }
}
