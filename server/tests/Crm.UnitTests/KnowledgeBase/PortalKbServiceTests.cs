using Crm.Application.Common.Exceptions;
using Crm.Application.Portal;
using Crm.Domain.KnowledgeBase;
using Crm.UnitTests.Localization;

namespace Crm.UnitTests.KnowledgeBase;

public class KbArticleFeedbackTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Feedback_RaisesTheMatchingCounter()
    {
        var article = KbArticle.Create(Guid.NewGuid(), "t", "b", null, null, Now);
        article.Publish(Now);

        article.RecordFeedback(true);
        article.RecordFeedback(true);
        article.RecordFeedback(false);

        Assert.Equal(2, article.HelpfulCount);
        Assert.Equal(1, article.NotHelpfulCount);
    }

    [Fact]
    public void Feedback_OnADraft_Throws() =>
        Assert.Throws<InvalidOperationException>(() => KbArticle.Create(Guid.NewGuid(), "t", "b", null, null, Now).RecordFeedback(true));
}

public class PortalKbServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeKbArticleRepository _articles = new();
    private readonly FakeKbCategoryRepository _categories;
    private readonly KbCategory _billing = KbCategory.Create("Billing", "الفواتير", Now);
    private readonly KbCategory _empty = KbCategory.Create("Only drafts", null, Now);

    public PortalKbServiceTests()
    {
        _categories = new FakeKbCategoryRepository(_articles) { Categories = { _billing, _empty } };
        _articles.Categories.AddRange([_billing, _empty]);
    }

    private PortalKbService Service => new(_articles, _categories);

    private KbArticle Add(KbCategory category, string title, string body, bool publish = true, string? titleAr = null, string? bodyAr = null)
    {
        var article = KbArticle.Create(category.Id, title, body, titleAr, bodyAr, Now);
        if (publish)
        {
            article.Publish(Now);
        }

        _articles.Articles.Add(article);
        return article;
    }

    [Fact]
    public async Task Categories_ListOnlyThoseWithPublishedArticles_WithTheirCount()
    {
        Add(_billing, "Invoices", "b");
        Add(_billing, "Refunds", "b");
        Add(_billing, "Hidden", "b", publish: false);
        Add(_empty, "Draft only", "b", publish: false);

        var list = await Service.ListCategoriesAsync(CancellationToken.None);

        var category = Assert.Single(list);
        Assert.Equal(_billing.Id, category.Id);
        Assert.Equal(2, category.ArticleCount);
        Assert.Equal("Billing", category.Name);
    }

    [Fact]
    public async Task Articles_ListPublishedOnly_WithSummary_AndFilterByCategory()
    {
        var live = Add(_billing, "Invoices", new string('x', 300));
        Add(_billing, "Hidden", "b", publish: false);
        var other = Add(_empty, "Elsewhere", "b");

        var all = await Service.ListArticlesAsync(null, 1, 20, CancellationToken.None);
        var filtered = await Service.ListArticlesAsync(_billing.Id, 1, 20, CancellationToken.None);

        Assert.Equal(2, all.TotalCount);
        Assert.Equal([live.Id], filtered.Items.Select(a => a.Id));
        Assert.Equal(160, filtered.Items[0].Summary.Length);
        Assert.Equal("Billing", filtered.Items[0].CategoryName);
        Assert.Contains(all.Items, a => a.Id == other.Id);
    }

    [Fact]
    public async Task Article_Draft_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service.GetArticleAsync(Add(_billing, "Hidden", "b", publish: false).Id, CancellationToken.None));

    [Fact]
    public async Task Article_Unknown_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => Service.GetArticleAsync(Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task Article_FollowsTheRequestLanguage_WithFallback()
    {
        var both = Add(_billing, "Reset", "English body", titleAr: "إعادة", bodyAr: "محتوى عربي");
        var englishOnly = Add(_billing, "Only English", "Body");
        var service = Service;

        var arabic = await UiCulture.Use("ar", () => service.GetArticleAsync(both.Id, CancellationToken.None));
        var english = await UiCulture.Use("en", () => service.GetArticleAsync(both.Id, CancellationToken.None));
        var fallback = await UiCulture.Use("ar", () => service.GetArticleAsync(englishOnly.Id, CancellationToken.None));

        Assert.Equal(("إعادة", "محتوى عربي", "الفواتير"), (arabic.Title, arabic.Body, arabic.CategoryName));
        Assert.Equal(("Reset", "English body", "Billing"), (english.Title, english.Body, english.CategoryName));
        Assert.Equal(("Only English", "Body"), (fallback.Title, fallback.Body));
    }

    [Fact]
    public async Task Feedback_CountsHelpfulAndNotHelpful_AndReturnsTheCounts()
    {
        var article = Add(_billing, "Invoices", "b");
        var service = Service;

        await service.RecordFeedbackAsync(article.Id, new PortalFeedbackRequest(true), CancellationToken.None);
        await service.RecordFeedbackAsync(article.Id, new PortalFeedbackRequest(true), CancellationToken.None);
        var result = await service.RecordFeedbackAsync(article.Id, new PortalFeedbackRequest(false), CancellationToken.None);

        Assert.Equal((2, 1), (result.HelpfulCount, result.NotHelpfulCount));
        Assert.Equal((2, 1), (article.HelpfulCount, article.NotHelpfulCount));
    }

    [Fact]
    public async Task Feedback_WithoutAValue_ThrowsValidation_OnHelpful()
    {
        var article = Add(_billing, "Invoices", "b");

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service.RecordFeedbackAsync(article.Id, new PortalFeedbackRequest(null), CancellationToken.None));

        Assert.Contains("helpful", error.Errors.Keys);
        Assert.Equal(0, article.HelpfulCount);
    }

    [Fact]
    public async Task Feedback_OnADraftOrUnknownArticle_IsNotFound()
    {
        var draft = Add(_billing, "Hidden", "b", publish: false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => Service.RecordFeedbackAsync(draft.Id, new PortalFeedbackRequest(true), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(
            () => Service.RecordFeedbackAsync(Guid.NewGuid(), new PortalFeedbackRequest(true), CancellationToken.None));
        Assert.Equal(0, draft.HelpfulCount);
    }
}
