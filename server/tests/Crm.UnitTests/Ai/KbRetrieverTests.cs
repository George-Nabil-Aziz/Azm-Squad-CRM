using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;

namespace Crm.UnitTests.Ai;

/// <summary>The contract of the EF repository: published, non-deleted articles whose search text contains the term.</summary>
internal sealed class FakeKbRetrievalRepository : IKbRetrievalRepository
{
    public List<KbArticle> Articles { get; } = [];

    public List<string> Terms { get; } = [];

    public Task<IReadOnlyList<KbArticle>> FindPublishedByTermAsync(string term, int max, CancellationToken cancellationToken)
    {
        Terms.Add(term);
        return Task.FromResult<IReadOnlyList<KbArticle>>([.. Articles
            .Where(a => a.IsPublished && !a.IsDeleted && a.SearchText.Contains(term, StringComparison.Ordinal)).Take(max)]);
    }
}

public class KbRetrieverTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CategoryId = Guid.NewGuid();

    private readonly FakeKbRetrievalRepository _repository = new();
    private readonly KbRetriever _retriever;

    public KbRetrieverTests() => _retriever = new KbRetriever(_repository);

    private KbArticle Add(string? titleEn, string? bodyEn, string? titleAr = null, string? bodyAr = null, bool publish = true, DateTime? at = null)
    {
        var article = KbArticle.Create(CategoryId, titleEn, bodyEn, titleAr, bodyAr, at ?? T0);
        if (publish)
        {
            article.Publish(at ?? T0);
        }

        _repository.Articles.Add(article);
        return article;
    }

    [Fact]
    public async Task OnlyPublishedAndNotDeletedArticles_AreReturned()
    {
        var published = Add("Reset your password", "Use the reset link on the sign-in page.");
        Add("Reset draft", "Draft about password reset.", publish: false);
        var deleted = Add("Reset deleted", "Deleted password article.");
        deleted.Delete(T0);

        var result = await _retriever.FindAsync("I cannot reset my password", 5, "en", CancellationToken.None);

        Assert.Equal([published.Id], result.Select(r => r.Id));
    }

    [Fact]
    public async Task AnyTermMatches_AndTitleHitsOutrankBodyHits()
    {
        var bodyOnly = Add("Account help", "You can change your invoice address here.");
        var titleHit = Add("Invoice payment", "Pay through the portal.");
        Add("Shipping", "We deliver in 3 days.");

        var result = await _retriever.FindAsync("my invoice payment failed", 5, "en", CancellationToken.None);

        Assert.Equal([titleHit.Id, bodyOnly.Id], result.Select(r => r.Id));
        Assert.True(result[0].Score > result[1].Score);
    }

    [Fact]
    public async Task StopWordsAndShortWords_AreNotSearched()
    {
        Add("The guide", "A guide for you and the team.");

        await _retriever.FindAsync("the and for you of it a", 5, "en", CancellationToken.None);

        Assert.Empty(_repository.Terms);
    }

    [Fact]
    public async Task BlankText_FindsNothing()
    {
        Assert.Empty(await _retriever.FindAsync("   ", 3, "en", CancellationToken.None));
        Assert.Empty(await _retriever.FindAsync(null, 3, "en", CancellationToken.None));
    }

    [Fact]
    public async Task ArabicIsNormalized_SoVariantsMatch()
    {
        var article = Add(null, null, "إعادة تعيين كلمة المرور", "استخدم رابط إعادة التعيين في صفحة الدخول.");

        var result = await _retriever.FindAsync("اعاده تعيين كلمه المرور", 3, "ar", CancellationToken.None);

        Assert.Equal([article.Id], result.Select(r => r.Id));
        Assert.Equal("إعادة تعيين كلمة المرور", result[0].Title);
    }

    [Fact]
    public async Task ReturnsAtMostMax_BestFirst_TiesNewestFirst()
    {
        var older = Add("Password reset", "How to reset.", at: T0);
        var newer = Add("Password reset steps", "How to reset.", at: T0.AddDays(1));
        Add("Password policy", "Rules.");
        Add("Password manager", "Tips.");

        var result = await _retriever.FindAsync("password reset", 2, "en", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal([newer.Id, older.Id], result.Select(r => r.Id));
    }

    [Fact]
    public async Task TheRequestedLanguageIsUsed_WithTheOtherAsFallback()
    {
        var both = Add("Refund policy", "Refunds take 5 days.", "سياسة الاسترجاع", "يستغرق الاسترجاع 5 أيام.");
        var englishOnly = Add("Refund steps", "Open the order and press refund.");

        var arabic = await _retriever.FindAsync("refund", 5, "ar", CancellationToken.None);
        var english = await _retriever.FindAsync("refund", 5, "en", CancellationToken.None);

        Assert.Equal("سياسة الاسترجاع", arabic.Single(r => r.Id == both.Id).Title);
        Assert.Equal("Refund steps", arabic.Single(r => r.Id == englishOnly.Id).Title);
        Assert.Equal("Refund policy", english.Single(r => r.Id == both.Id).Title);
    }

    [Fact]
    public async Task AtMostTwelveTerms_AreSearched()
    {
        await _retriever.FindAsync(string.Join(' ', Enumerable.Range(0, 30).Select(i => $"word{i:D2}x")), 3, "en", CancellationToken.None);

        Assert.Equal(KbRetriever.MaxTerms, _repository.Terms.Count);
    }
}
