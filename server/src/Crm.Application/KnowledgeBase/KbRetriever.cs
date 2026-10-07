using Crm.Application.Common.Localization;
using Crm.Domain.KnowledgeBase;

namespace Crm.Application.KnowledgeBase;

/// <summary>A published article found for a text (CRM-51, 53, 54), in the language asked for. <c>Score</c> orders the hits (higher = better).</summary>
public sealed record KbRetrievedArticle(Guid Id, string Title, string Body, int Score);

/// <summary>Retrieval storage (EF Core in Crm.Infrastructure).</summary>
public interface IKbRetrievalRepository
{
    /// <summary>Published, non-deleted articles whose normalized search text contains <paramref name="term"/> (at most <paramref name="max"/>, newest first).</summary>
    Task<IReadOnlyList<KbArticle>> FindPublishedByTermAsync(string term, int max, CancellationToken cancellationToken);
}

/// <summary>
/// Finds the published articles that best match a free text (a ticket, a chat question): articles matching <b>any</b> word,
/// scored by where the words occur. Draft and deleted articles are never returned. Unlike the staff search (every word must
/// match) this tolerates long texts.
/// </summary>
public interface IKbRetriever
{
    /// <param name="text">The text to match; blank gives an empty list.</param>
    /// <param name="max">How many articles at most.</param>
    /// <param name="language">"ar" or "en": the version returned (the other one when it is missing).</param>
    Task<IReadOnlyList<KbRetrievedArticle>> FindAsync(string? text, int max, string language, CancellationToken cancellationToken);
}

public sealed class KbRetriever(IKbRetrievalRepository repository) : IKbRetriever
{
    public const int MaxTerms = 12;
    public const int MinTermLength = 3;
    private const int CandidatesPerTerm = 50;
    private const int TitleScore = 3;
    private const int BodyScore = 1;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "you", "your", "are", "was", "were", "with", "that", "this", "have", "has", "not", "but", "can",
        "cannot", "could", "would", "should", "how", "why", "what", "when", "where", "from", "our", "out", "any", "all", "its",
        "they", "them", "their", "there", "been", "will", "just", "please", "thanks", "thank", "hello", "dear", "regards",
        "من", "في", "على", "الي", "عن", "هذا", "هذه", "ذلك", "التي", "الذي", "انا", "هل", "لم", "لا", "كان", "مع", "او", "ان",
        "قد", "كيف", "لماذا", "اريد", "ابي", "ممكن", "شكرا", "مرحبا", "السلام", "عليكم",
    };

    public async Task<IReadOnlyList<KbRetrievedArticle>> FindAsync(
        string? text, int max, string language, CancellationToken cancellationToken)
    {
        var terms = Terms(text);
        if (terms.Count == 0 || max <= 0)
        {
            return [];
        }

        var found = new Dictionary<Guid, KbArticle>();
        foreach (var term in terms)
        {
            foreach (var article in await repository.FindPublishedByTermAsync(term, CandidatesPerTerm, cancellationToken))
            {
                if (article.IsPublished && !article.IsDeleted)
                {
                    found.TryAdd(article.Id, article);
                }
            }
        }

        var arabic = language == LocalizedText.Arabic;
        return [.. found.Values
            .Select(article => (Article: article, Score: Score(terms, article)))
            .Where(hit => hit.Score > 0)
            .OrderByDescending(hit => hit.Score).ThenByDescending(hit => hit.Article.PublishedAt ?? hit.Article.CreatedAt)
            .Take(max)
            .Select(hit => new KbRetrievedArticle(
                hit.Article.Id,
                arabic ? Prefer(hit.Article.TitleAr, hit.Article.TitleEn) : Prefer(hit.Article.TitleEn, hit.Article.TitleAr),
                arabic ? Prefer(hit.Article.BodyAr, hit.Article.BodyEn) : Prefer(hit.Article.BodyEn, hit.Article.BodyAr),
                hit.Score))];
    }

    private static IReadOnlyList<string> Terms(string? text) =>
        [.. KbSearchText.Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim('.', ',', ';', ':', '!', '?', '،', '؛', '؟', '"', '\'', '(', ')'))
            .Where(term => term.Length >= MinTermLength && !StopWords.Contains(term))
            .Select(term => term.Length > KbSearchText.MaxTermLength ? term[..KbSearchText.MaxTermLength] : term)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTerms)];

    private static int Score(IReadOnlyList<string> terms, KbArticle article)
    {
        var title = KbSearchText.Build(article.TitleEn, article.TitleAr);
        var body = KbSearchText.Build(article.BodyEn, article.BodyAr);
        return terms.Sum(term => (title.Contains(term, StringComparison.Ordinal) ? TitleScore : 0)
                                 + (body.Contains(term, StringComparison.Ordinal) ? BodyScore : 0));
    }

    private static string Prefer(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
}
