using System.Text;
using System.Text.Json;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Localization;
using Crm.Application.Common.Security;
using Crm.Application.KnowledgeBase;
using Crm.Application.Tickets;
using Crm.Domain.Ai;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Ai;

/// <summary>A suggested knowledge base article: <c>Summary</c> is the start of its body; <c>Useful</c> is the current user's vote (null = none).</summary>
public sealed record SuggestedSolutionResponse(Guid ArticleId, string Title, string Summary, bool? Useful);

/// <summary>Body of PUT /api/tickets/{id}/ai-suggestions/{articleId}/feedback.</summary>
public sealed record SuggestionFeedbackRequest(bool? Useful);

public sealed record SuggestionFeedbackResponse(Guid ArticleId, bool Useful);

/// <summary>Feedback storage (EF Core in Crm.Infrastructure).</summary>
public interface ISuggestionFeedbackRepository
{
    /// <summary>True for a published, non-deleted article.</summary>
    Task<bool> IsPublishedArticleAsync(Guid articleId, CancellationToken cancellationToken);

    /// <summary>The tracked vote of the user, or null.</summary>
    Task<TicketSuggestionFeedback?> FindAsync(Guid ticketId, Guid articleId, Guid userId, CancellationToken cancellationToken);

    /// <summary>The user's votes on the ticket (article id → useful).</summary>
    Task<IReadOnlyDictionary<Guid, bool>> ListVotesAsync(Guid ticketId, Guid userId, CancellationToken cancellationToken);

    void Add(TicketSuggestionFeedback feedback);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// AI suggested solutions (CRM-53): the top 3 published articles for a ticket and the agent's feedback on them. Listing needs
/// <c>tickets.view</c> + <c>kb.view</c>, feedback <c>tickets.manage</c> + <c>kb.view</c> (enforced by the API). Without AI (or when it
/// fails) the keyword ranking is used. Failures: <c>NotFoundException</c> 404, <c>ValidationException</c> 400.
/// </summary>
public interface ISuggestedSolutionsService
{
    Task<IReadOnlyList<SuggestedSolutionResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken);

    Task<SuggestionFeedbackResponse> RecordFeedbackAsync(
        Guid ticketId, Guid articleId, SuggestionFeedbackRequest request, CancellationToken cancellationToken);
}

public sealed class SuggestedSolutionsService(
    ITicketRepository tickets,
    ITicketMessageRepository messages,
    IKbRetriever knowledgeBase,
    IAiTextService ai,
    ISuggestionFeedbackRepository feedback,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ISuggestedSolutionsService
{
    public const int Count = 3;
    private const int Candidates = 8;
    private const int SummaryLength = 200;
    private const int CandidateBodyChars = 400;

    public async Task<IReadOnlyList<SuggestedSolutionResponse>> ListAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        var thread = await messages.ListAsync(ticketId, includeInternal: false, cancellationToken);
        var lastCustomer = thread.Where(m => m.Direction == "inbound").OrderBy(m => m.CreatedAt).LastOrDefault();
        var language = AiLanguage.Detect([lastCustomer?.Body, ticket.Description], ticket.Subject);
        var query = string.Join(' ', new[] { ticket.Subject, lastCustomer?.Body, ticket.Description }.Where(t => !string.IsNullOrWhiteSpace(t)));

        var candidates = await knowledgeBase.FindAsync(query, Candidates, language, cancellationToken);
        var chosen = await RankWithAiAsync(query, candidates, cancellationToken) ?? [.. candidates.Take(Count)];

        var votes = currentUser.UserId is { } userId
            ? await feedback.ListVotesAsync(ticketId, userId, cancellationToken)
            : new Dictionary<Guid, bool>();
        return [.. chosen.Select(a => new SuggestedSolutionResponse(
            a.Id, a.Title, a.Body.Length <= SummaryLength ? a.Body : a.Body[..SummaryLength], votes.TryGetValue(a.Id, out var useful) ? useful : null))];
    }

    public async Task<SuggestionFeedbackResponse> RecordFeedbackAsync(
        Guid ticketId, Guid articleId, SuggestionFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (request.Useful is not { } useful)
        {
            throw FieldError("useful", AiText.UsefulRequired);
        }

        _ = await tickets.FindAsync(ticketId, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        if (!await feedback.IsPublishedArticleAsync(articleId, cancellationToken))
        {
            throw FieldError("articleId", AiText.ArticleNotPublished);
        }

        var userId = currentUser.UserId ?? throw new ForbiddenException(ErrorText.Forbidden);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (await feedback.FindAsync(ticketId, articleId, userId, cancellationToken) is { } existing)
        {
            existing.Set(useful, now);
        }
        else
        {
            feedback.Add(TicketSuggestionFeedback.Create(ticketId, articleId, userId, useful, now));
        }

        await feedback.SaveChangesAsync(cancellationToken);
        return new SuggestionFeedbackResponse(articleId, useful);
    }

    /// <summary>The AI's pick (at most 3, in its order, candidates only); null when AI is off, fails or picks nothing usable.</summary>
    private async Task<IReadOnlyList<KbRetrievedArticle>?> RankWithAiAsync(
        string query, IReadOnlyList<KbRetrievedArticle> candidates, CancellationToken cancellationToken)
    {
        if (!ai.IsConfigured || candidates.Count == 0)
        {
            return null;
        }

        var prompt = new StringBuilder("Ticket: ").AppendLine(PiiMasker.Mask(query.Length > 1_500 ? query[..1_500] : query)).AppendLine().AppendLine("Candidate articles:");
        foreach (var candidate in candidates)
        {
            var body = candidate.Body.Length > CandidateBodyChars ? candidate.Body[..CandidateBodyChars] : candidate.Body;
            prompt.Append("- ").Append(candidate.Id).Append(": ").Append(candidate.Title).Append(" — ").AppendLine(body.ReplaceLineEndings(" "));
        }

        string answer;
        try
        {
            answer = await ai.CompleteAsync(new AiRequest(
                "You pick the knowledge base articles that best solve a customer support ticket. Reply with one JSON object only: " +
                "{\"articleIds\": [up to 3 ids from the list, best first]}. Pick only articles that really help; an empty list is allowed.",
                prompt.ToString().TrimEnd(), 200), cancellationToken);
        }
        catch (Exception exception) when (exception is AiFailedException or AiNotConfiguredException)
        {
            return null;
        }

        var ids = ParseIds(answer);
        var picked = ids.Select(id => candidates.FirstOrDefault(c => c.Id == id)).OfType<KbRetrievedArticle>().Distinct().Take(Count).ToList();
        return picked.Count == 0 ? null : picked;
    }

    private static List<Guid> ParseIds(string answer)
    {
        var start = answer.IndexOf('{');
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(answer[start..(end + 1)]);
            if (!document.RootElement.TryGetProperty("articleIds", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return [.. array.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => Guid.TryParse(e.GetString(), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ValidationException FieldError(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
