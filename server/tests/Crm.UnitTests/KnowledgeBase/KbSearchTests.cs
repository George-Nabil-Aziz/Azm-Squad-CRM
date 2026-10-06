using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.KnowledgeBase;

public class KbSearchTextTests
{
    [Theory]
    [InlineData("أحمد", "احمد")]
    [InlineData("إدارة", "اداره")]
    [InlineData("آخر", "اخر")]
    [InlineData("ٱلله", "الله")]
    [InlineData("مدرسة", "مدرسه")]
    [InlineData("على", "علي")]
    public void Normalize_FoldsArabicLetterVariants(string input, string expected) =>
        Assert.Equal(expected, KbSearchText.Normalize(input));

    [Fact]
    public void Normalize_RemovesDiacriticsAndTatweel() =>
        Assert.Equal("كتاب", KbSearchText.Normalize("كِتَـــابٌ"));

    [Fact]
    public void Normalize_LowerCasesAndCollapsesWhitespace() =>
        Assert.Equal("reset your password", KbSearchText.Normalize("  Reset\t YOUR \n Password "));

    [Fact]
    public void Normalize_MapsArabicIndicDigits() =>
        Assert.Equal("order 123", KbSearchText.Normalize("Order ١٢٣"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_OfNothing_IsEmpty(string? input) => Assert.Equal(string.Empty, KbSearchText.Normalize(input));

    [Fact]
    public void ArabicWithAndWithoutHamzaAndTaMarbuta_NormalizeTheSame() =>
        Assert.Equal(KbSearchText.Normalize("إدارة الحساب"), KbSearchText.Normalize("اداره الحساب"));

    [Fact]
    public void Terms_AreSplitDeduplicatedAndLimited()
    {
        Assert.Equal(["reset", "password"], KbSearchText.Terms("Reset password reset"));
        Assert.Empty(KbSearchText.Terms("   "));
        Assert.Equal(8, KbSearchText.Terms("a b c d e f g h i j").Count);
        Assert.Equal(50, KbSearchText.Terms(new string('x', 80))[0].Length);
    }

    [Fact]
    public void Article_StoresTheNormalizedSearchText_OfBothLanguages()
    {
        var now = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

        var article = KbArticle.Create(Guid.NewGuid(), "Reset", "Body", "إعادة", "مدرسة", now);
        var faq = KbFaq.Create("Pay?", "Online", "كيف أدفع", "عبر الإنترنت", 1, true, now);

        Assert.Contains("reset", article.SearchText);
        Assert.Contains("اعاده", article.SearchText);
        Assert.Contains("مدرسه", article.SearchText);
        Assert.Contains("ادفع", faq.SearchText);
        Assert.DoesNotContain("إ", article.SearchText);
    }
}

public class KbSearchServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeKbSearchRepository _repository = new();

    private KbSearchService Service => new(_repository);

    private KbArticle Article(string? titleEn, string? bodyEn, string? titleAr = null, string? bodyAr = null)
    {
        var article = KbArticle.Create(Guid.NewGuid(), titleEn, bodyEn, titleAr, bodyAr, Now);
        article.Publish(Now);
        _repository.Articles.Add(article);
        return article;
    }

    private KbFaq Faq(string question, string answer)
    {
        var faq = KbFaq.Create(question, answer, null, null, 1, true, Now);
        _repository.Faqs.Add(faq);
        return faq;
    }

    [Fact]
    public async Task ATitleMatch_RanksAboveABodyMatch()
    {
        var inBody = Article("Account help", "You can reset it from settings.");
        var inTitle = Article("Reset your password", "Use the link.");

        var results = await Service.SearchAsync("reset", CancellationToken.None);

        Assert.Equal([inTitle.Id, inBody.Id], results.Select(r => r.Id));
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public async Task ArticlesAndFaqs_AreBothReturned_WithTheirType()
    {
        var article = Article("Refund policy", "How refunds work.");
        var faq = Faq("How long does a refund take?", "Three days.");

        var results = await Service.SearchAsync("refund", CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("article", results.Single(r => r.Id == article.Id).Type);
        Assert.Equal("faq", results.Single(r => r.Id == faq.Id).Type);
    }

    [Fact]
    public async Task EveryTermMustMatch()
    {
        var both = Article("Reset password", "Steps.");
        Article("Reset router", "Steps.");

        var results = await Service.SearchAsync("reset password", CancellationToken.None);

        Assert.Equal([both.Id], results.Select(r => r.Id));
    }

    [Fact]
    public async Task TheFullPhraseInTheTitle_ScoresHigher()
    {
        var phrase = Article("How to reset password", "Steps.");
        var split = Article("Password: how to reset it", "Steps.");

        var results = await Service.SearchAsync("reset password", CancellationToken.None);

        Assert.Equal(phrase.Id, results[0].Id);
        Assert.Equal(split.Id, results[1].Id);
    }

    [Fact]
    public async Task ArabicSearch_IgnoresHamzaAndTaMarbutaDifferences()
    {
        var article = Article(null, null, "إدارة الحساب", "كيف تدير حسابك في المدرسة");

        var plain = await Service.SearchAsync("اداره", CancellationToken.None);
        var marbuta = await Service.SearchAsync("المدرسه", CancellationToken.None);
        var original = await Service.SearchAsync("إدارة", CancellationToken.None);

        Assert.Equal([article.Id], plain.Select(r => r.Id));
        Assert.Equal([article.Id], marbuta.Select(r => r.Id));
        Assert.Equal([article.Id], original.Select(r => r.Id));
    }

    [Fact]
    public async Task ResultsFollowTheRequestLanguage_WithFallback()
    {
        Article("Reset password", "English body", "إعادة تعيين", "محتوى عربي");
        Article("Only English", "reset it");

        var arabic = await UiCulture.Use("ar", () => Service.SearchAsync("reset", CancellationToken.None));

        Assert.Equal(["Only English", "إعادة تعيين"], arabic.Select(r => r.Title).Order(StringComparer.Ordinal));
        var arabicMatch = await UiCulture.Use("ar", () => Service.SearchAsync("تعيين", CancellationToken.None));
        Assert.Equal("إعادة تعيين", arabicMatch.Single().Title);
        Assert.Equal("محتوى عربي", arabicMatch.Single().Snippet);
    }

    [Fact]
    public async Task TheSnippet_IsTheStartOfTheBody_AtMost160Characters()
    {
        Article("Long", new string('a', 400));

        var result = (await Service.SearchAsync("long", CancellationToken.None)).Single();

        Assert.Equal(160, result.Snippet.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyQuery_ReturnsNothing_WithoutTouchingTheRepository(string? query)
    {
        Article("Reset", "Body");
        _repository.Calls = 0;

        var results = await Service.SearchAsync(query, CancellationToken.None);

        Assert.Empty(results);
        Assert.Equal(0, _repository.Calls);
    }

    [Fact]
    public async Task AtMost50ResultsAreReturned()
    {
        for (var i = 0; i < 60; i++)
        {
            Article($"Reset {i}", "Body");
        }

        var results = await Service.SearchAsync("reset", CancellationToken.None);

        Assert.Equal(50, results.Count);
    }
}

public class KbSearchVisibilityTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DraftsAndUnpublishedFaqs_AreNeverReturned_EvenIfTheRepositoryHandsThemOver()
    {
        var repository = new FakeKbSearchRepository();
        var draft = KbArticle.Create(Guid.NewGuid(), "Reset draft", "Body", null, null, Now);
        var live = KbArticle.Create(Guid.NewGuid(), "Reset live", "Body", null, null, Now);
        live.Publish(Now);
        repository.Articles.Add(draft);
        repository.Articles.Add(live);
        repository.Faqs.Add(KbFaq.Create("Reset hidden?", "No", null, null, 1, false, Now));
        repository.Faqs.Add(KbFaq.Create("Reset shown?", "Yes", null, null, 2, true, Now));

        var results = await new KbSearchService(repository).SearchAsync("reset", CancellationToken.None);

        Assert.Equal(["Reset live", "Reset shown?"], results.Select(r => r.Title).Order());
    }
}
