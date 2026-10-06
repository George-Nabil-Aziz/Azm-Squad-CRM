namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// An agent inserted a knowledge base article into a ticket reply (CRM-39). Written once; the ticket keeps which
/// articles were linked, by whom and when.
/// </summary>
public sealed class TicketArticleLink
{
    private TicketArticleLink()
    {
        // EF Core materializes links through this constructor.
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid ArticleId { get; private set; }

    /// <summary>The staff user who inserted the article.</summary>
    public Guid? LinkedById { get; private set; }

    public DateTime LinkedAt { get; private set; }

    public static TicketArticleLink Create(Guid ticketId, Guid articleId, Guid? linkedById, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC (DateTimeKind.Utc).", nameof(utcNow));
        }

        return new TicketArticleLink
        {
            Id = Guid.NewGuid(), TicketId = ticketId, ArticleId = articleId, LinkedById = linkedById, LinkedAt = utcNow,
        };
    }
}
