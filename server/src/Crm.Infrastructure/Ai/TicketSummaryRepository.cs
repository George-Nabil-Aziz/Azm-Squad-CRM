using Crm.Application.Ai;
using Crm.Domain.Ai;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Ai;

/// <summary>EF Core storage of the AI summaries of tickets.</summary>
public sealed class TicketSummaryRepository(CrmDbContext db) : ITicketSummaryRepository
{
    public Task<TicketAiSummary?> FindAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.TicketAiSummaries.FirstOrDefaultAsync(s => s.TicketId == ticketId, cancellationToken);

    public void Add(TicketAiSummary summary) => db.TicketAiSummaries.Add(summary);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

/// <summary>EF Core storage of the AI classifications; they are saved by the ticket's own save.</summary>
public sealed class AiClassificationRepository(CrmDbContext db) : ITicketAiClassificationRepository
{
    public Task<TicketAiClassification?> FindAsync(Guid ticketId, CancellationToken cancellationToken) =>
        db.TicketAiClassifications.FirstOrDefaultAsync(c => c.TicketId == ticketId, cancellationToken);

    public void Add(TicketAiClassification classification) => db.TicketAiClassifications.Add(classification);
}

/// <summary>EF Core storage of the votes on AI-suggested articles.</summary>
public sealed class SuggestionFeedbackRepository(CrmDbContext db) : ISuggestionFeedbackRepository
{
    public Task<bool> IsPublishedArticleAsync(Guid articleId, CancellationToken cancellationToken) =>
        db.KbArticles.AnyAsync(a => a.Id == articleId && a.Status == Crm.Domain.KnowledgeBase.KbArticleStatus.Published, cancellationToken);

    public Task<TicketSuggestionFeedback?> FindAsync(Guid ticketId, Guid articleId, Guid userId, CancellationToken cancellationToken) =>
        db.TicketSuggestionFeedback.FirstOrDefaultAsync(f => f.TicketId == ticketId && f.ArticleId == articleId && f.UserId == userId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, bool>> ListVotesAsync(Guid ticketId, Guid userId, CancellationToken cancellationToken) =>
        await db.TicketSuggestionFeedback.AsNoTracking().Where(f => f.TicketId == ticketId && f.UserId == userId)
            .ToDictionaryAsync(f => f.ArticleId, f => f.Useful, cancellationToken);

    public void Add(TicketSuggestionFeedback feedback) => db.TicketSuggestionFeedback.Add(feedback);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
