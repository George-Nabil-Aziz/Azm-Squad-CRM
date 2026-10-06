using Crm.Domain.KnowledgeBase;

namespace Crm.Application.KnowledgeBase;

/// <summary>
/// One hit of the knowledge base search. <c>Type</c> is "article" or "faq"; <c>Title</c> is the title / question in the
/// request language (the other language when a version is missing); <c>Snippet</c> is the start of the body / answer.
/// <c>Score</c> orders the hits (higher = more relevant).
/// </summary>
public sealed record KbSearchResultResponse(string Type, Guid Id, string Title, string Snippet, int Score);

/// <summary>The candidates the repository found: published, non-deleted items whose search text contains every term.</summary>
public sealed record KbSearchCandidates(IReadOnlyList<KbArticle> Articles, IReadOnlyList<KbFaq> Faqs);

/// <summary>Search storage (EF Core in Crm.Infrastructure).</summary>
public interface IKbSearchRepository
{
    /// <summary>Published, non-deleted articles and FAQs whose normalized search text contains <b>every</b> term (at most <paramref name="max"/> of each kind).</summary>
    Task<KbSearchCandidates> FindCandidatesAsync(IReadOnlyList<string> terms, int max, CancellationToken cancellationToken);
}

/// <summary>
/// Searches articles and FAQs (CRM-38). Reading needs <c>kb.view</c> on the staff endpoint; the portal endpoint is anonymous.
/// Only published content is ever returned. An empty or blank query returns an empty list, not an error.
/// </summary>
public interface IKbSearchService
{
    Task<IReadOnlyList<KbSearchResultResponse>> SearchAsync(string? query, CancellationToken cancellationToken);
}

public sealed class KbSearchService(IKbSearchRepository repository) : IKbSearchService
{
    public const int MaxResults = 50;
    private const int MaxCandidates = 200;
    private const int SnippetLength = 160;
    private const int TitleTermScore = 10;
    private const int BodyTermScore = 3;
    private const int ExtraOccurrenceScore = 1;
    private const int MaxExtraOccurrences = 3;
    private const int PhraseInTitleScore = 20;

    public async Task<IReadOnlyList<KbSearchResultResponse>> SearchAsync(string? query, CancellationToken cancellationToken)
    {
        var terms = KbSearchText.Terms(query);
        if (terms.Count == 0)
        {
            return [];
        }

        var phrase = string.Join(' ', terms);
        var candidates = await repository.FindCandidatesAsync(terms, MaxCandidates, cancellationToken);

        var hits = new List<(KbSearchResultResponse Result, DateTime CreatedAt)>();
        foreach (var article in candidates.Articles.Where(a => a.IsPublished && !a.IsDeleted))
        {
            var score = Score(terms, phrase, KbSearchText.Build(article.TitleEn, article.TitleAr), KbSearchText.Build(article.BodyEn, article.BodyAr));
            hits.Add((new KbSearchResultResponse(
                "article", article.Id, KbLanguage.Pick(article.TitleEn, article.TitleAr),
                Snippet(KbLanguage.Pick(article.BodyEn, article.BodyAr)), score), article.CreatedAt));
        }

        foreach (var faq in candidates.Faqs.Where(f => f.IsPublished && !f.IsDeleted))
        {
            var score = Score(terms, phrase, KbSearchText.Build(faq.QuestionEn, faq.QuestionAr), KbSearchText.Build(faq.AnswerEn, faq.AnswerAr));
            hits.Add((new KbSearchResultResponse(
                "faq", faq.Id, KbLanguage.Pick(faq.QuestionEn, faq.QuestionAr),
                Snippet(KbLanguage.Pick(faq.AnswerEn, faq.AnswerAr)), score), faq.CreatedAt));
        }

        return [.. hits
            .OrderByDescending(hit => hit.Result.Score).ThenByDescending(hit => hit.CreatedAt)
            .Take(MaxResults).Select(hit => hit.Result)];
    }

    private static int Score(IReadOnlyList<string> terms, string phrase, string title, string body)
    {
        var score = title.Contains(phrase, StringComparison.Ordinal) ? PhraseInTitleScore : 0;
        foreach (var term in terms)
        {
            if (title.Contains(term, StringComparison.Ordinal))
            {
                score += TitleTermScore;
            }

            var occurrences = Count(body, term);
            if (occurrences > 0)
            {
                score += BodyTermScore + (Math.Min(occurrences, MaxExtraOccurrences + 1) - 1) * ExtraOccurrenceScore;
            }
        }

        return score;
    }

    private static int Count(string text, string term)
    {
        var count = 0;
        for (var index = text.IndexOf(term, StringComparison.Ordinal); index >= 0;
             index = text.IndexOf(term, index + term.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string Snippet(string text) => text.Length <= SnippetLength ? text : text[..SnippetLength];
}
