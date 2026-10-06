using Crm.Application.Common.Exceptions;
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;
using Crm.UnitTests.Localization;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.KnowledgeBase;

public class KbArticleRequestValidatorTests
{
    private readonly KbArticleRequestValidator _validator = new();

    [Fact]
    public void MissingTitle_IsInvalid_OnTitle()
    {
        var result = _validator.Validate(new KbArticleRequest(Guid.NewGuid(), null, null, " ", null));

        Assert.Contains(result.Errors, e => e.PropertyName == "title");
    }

    [Fact]
    public void TitleWithoutBody_IsInvalid_OnTheBody()
    {
        var result = _validator.Validate(new KbArticleRequest(Guid.NewGuid(), "Title", null, null, null));

        Assert.Contains(result.Errors, e => e.PropertyName == "bodyEn");
    }

    [Fact]
    public void BodyWithoutTitle_IsInvalid_OnTheTitle()
    {
        var result = _validator.Validate(new KbArticleRequest(Guid.NewGuid(), "T", "B", null, "محتوى"));

        Assert.Contains(result.Errors, e => e.PropertyName == "titleAr");
    }

    [Fact]
    public void MissingCategory_IsInvalid()
    {
        var result = _validator.Validate(new KbArticleRequest(null, "T", "B", null, null));

        Assert.Contains(result.Errors, e => e.PropertyName == "CategoryId");
    }

    [Fact]
    public void TooLongTitle_IsInvalid()
    {
        var result = _validator.Validate(new KbArticleRequest(Guid.NewGuid(), new string('x', 201), "B", null, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ArabicOnlyArticle_IsValid() =>
        Assert.True(_validator.Validate(new KbArticleRequest(Guid.NewGuid(), null, null, "عنوان", "محتوى")).IsValid);

    [Fact]
    public void MessagesAreLocalized()
    {
        var request = new KbArticleRequest(Guid.NewGuid(), null, null, null, null);

        var english = UiCulture.Use("en", () => _validator.Validate(request).Errors.Single(e => e.PropertyName == "title").ErrorMessage);
        var arabic = UiCulture.Use("ar", () => _validator.Validate(request).Errors.Single(e => e.PropertyName == "title").ErrorMessage);

        Assert.NotEqual(english, arabic);
    }
}

public class KbArticleServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeKbArticleRepository _articles = new();
    private readonly TestClock _clock = new(Start);
    private readonly KbCategory _category = KbCategory.Create("Billing", "الفواتير", Start.UtcDateTime);

    public KbArticleServiceTests()
    {
        _articles.Categories.Add(_category);
    }

    private KbArticleService Service(KbUser user) => new(
        _articles, new FakeKbCategoryRepository(_articles) { Categories = { _category } }, user, _clock,
        new KbArticleRequestValidator(), new ListKbArticlesQueryValidator());

    private KbArticleRequest Request(string? title = "Reset password", string? titleAr = null) =>
        new(_category.Id, title, title is null ? null : "Body", titleAr, titleAr is null ? null : "محتوى");

    [Fact]
    public async Task Create_SavesADraft_WithTheCategoryName()
    {
        var response = await Service(KbUser.Editor).CreateAsync(Request(), CancellationToken.None);

        Assert.Equal("draft", response.Status);
        Assert.Equal("Billing", response.CategoryName);
        Assert.Equal("Reset password", response.Title);
        Assert.Null(response.PublishedAt);
        Assert.Equal(Start.UtcDateTime, response.CreatedAt);
        Assert.Single(_articles.Articles);
    }

    [Fact]
    public async Task Create_WithoutATitle_ThrowsValidation_OnTitle()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service(KbUser.Editor).CreateAsync(Request(title: null), CancellationToken.None));

        Assert.Contains("title", error.Errors.Keys);
        Assert.Empty(_articles.Articles);
    }

    [Fact]
    public async Task Create_WithAnUnknownCategory_ThrowsValidation_OnCategoryId()
    {
        var request = Request() with { CategoryId = Guid.NewGuid() };

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => Service(KbUser.Editor).CreateAsync(request, CancellationToken.None));

        Assert.Contains("categoryId", error.Errors.Keys);
    }

    [Fact]
    public async Task Create_KeepsBothLanguageVersions()
    {
        var response = await Service(KbUser.Editor).CreateAsync(Request(titleAr: "إعادة التعيين"), CancellationToken.None);

        Assert.Equal("Reset password", response.TitleEn);
        Assert.Equal("إعادة التعيين", response.TitleAr);
        Assert.Equal("محتوى", response.BodyAr);
    }

    [Fact]
    public async Task Title_FollowsTheRequestLanguage_WithFallback()
    {
        var response = await Service(KbUser.Editor).CreateAsync(Request(titleAr: "إعادة التعيين"), CancellationToken.None);
        var service = Service(KbUser.Editor);

        var arabic = await UiCulture.Use("ar", () => service.GetAsync(response.Id, CancellationToken.None));
        var english = await UiCulture.Use("en", () => service.GetAsync(response.Id, CancellationToken.None));

        Assert.Equal("إعادة التعيين", arabic.Title);
        Assert.Equal("Reset password", english.Title);
    }

    [Fact]
    public async Task Publish_MakesTheArticleVisibleToAgents()
    {
        var created = await Service(KbUser.Editor).CreateAsync(Request(), CancellationToken.None);
        var agent = Service(KbUser.Agent);

        await Assert.ThrowsAsync<NotFoundException>(() => agent.GetAsync(created.Id, CancellationToken.None));
        _clock.UtcNow = Start.AddHours(1);
        var published = await Service(KbUser.Editor).PublishAsync(created.Id, CancellationToken.None);
        var seen = await agent.GetAsync(created.Id, CancellationToken.None);

        Assert.Equal("published", published.Status);
        Assert.Equal(Start.AddHours(1).UtcDateTime, published.PublishedAt);
        Assert.Equal(created.Id, seen.Id);
    }

    [Fact]
    public async Task List_HidesDrafts_FromUsersWithoutManage_EvenWhenAskedFor()
    {
        var editor = Service(KbUser.Editor);
        var draft = await editor.CreateAsync(Request("Draft article"), CancellationToken.None);
        var live = await editor.CreateAsync(Request("Live article"), CancellationToken.None);
        await editor.PublishAsync(live.Id, CancellationToken.None);

        var asAgent = await Service(KbUser.Agent).ListAsync(new ListKbArticlesQuery(null, "draft", null, null, null), CancellationToken.None);
        var asEditor = await editor.ListAsync(new ListKbArticlesQuery(null, null, null, null, null), CancellationToken.None);
        var drafts = await editor.ListAsync(new ListKbArticlesQuery(null, "draft", null, null, null), CancellationToken.None);

        Assert.Equal([live.Id], asAgent.Items.Select(a => a.Id));
        Assert.Equal(2, asEditor.TotalCount);
        Assert.Equal([draft.Id], drafts.Items.Select(a => a.Id));
    }

    [Fact]
    public async Task List_WithAnUnknownStatus_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(KbUser.Editor).ListAsync(new ListKbArticlesQuery(null, "nope", null, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Unpublish_HidesTheArticleAgain()
    {
        var editor = Service(KbUser.Editor);
        var created = await editor.CreateAsync(Request(), CancellationToken.None);
        await editor.PublishAsync(created.Id, CancellationToken.None);

        await editor.UnpublishAsync(created.Id, CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => Service(KbUser.Agent).GetAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Update_ChangesTheContent_AndAnUnknownArticleIsNotFound()
    {
        var editor = Service(KbUser.Editor);
        var created = await editor.CreateAsync(Request(), CancellationToken.None);

        var updated = await editor.UpdateAsync(created.Id, Request("Changed"), CancellationToken.None);

        Assert.Equal("Changed", updated.TitleEn);
        await Assert.ThrowsAsync<NotFoundException>(() => editor.UpdateAsync(Guid.NewGuid(), Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_HidesTheArticle()
    {
        var editor = Service(KbUser.Editor);
        var created = await editor.CreateAsync(Request(), CancellationToken.None);

        await editor.DeleteAsync(created.Id, CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => editor.GetAsync(created.Id, CancellationToken.None));
    }
}

public class KbCategoryServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeKbArticleRepository _articles = new();
    private readonly FakeKbCategoryRepository _categories;
    private readonly KbCategoryService _service;

    public KbCategoryServiceTests()
    {
        _categories = new FakeKbCategoryRepository(_articles);
        _service = new KbCategoryService(_categories, new TestClock(Start), new KbCategoryRequestValidator());
    }

    [Fact]
    public async Task Create_SavesBothNames()
    {
        var response = await _service.CreateAsync(new KbCategoryRequest("Billing", "الفواتير"), CancellationToken.None);

        Assert.Equal("Billing", response.NameEn);
        Assert.Equal("الفواتير", response.NameAr);
        Assert.Equal(0, response.ArticleCount);
    }

    [Fact]
    public async Task Create_WithoutAName_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new KbCategoryRequest(" ", null), CancellationToken.None));
    }

    [Fact]
    public async Task Delete_WithArticles_ThrowsConflict()
    {
        var category = await _service.CreateAsync(new KbCategoryRequest("Billing", null), CancellationToken.None);
        _articles.Articles.Add(KbArticle.Create(category.Id, "t", "b", null, null, Start.UtcDateTime));

        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(category.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_AnEmptyCategory_RemovesItFromTheList()
    {
        var category = await _service.CreateAsync(new KbCategoryRequest("Billing", null), CancellationToken.None);

        await _service.DeleteAsync(category.Id, CancellationToken.None);

        Assert.Empty(await _service.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task List_CountsArticles()
    {
        var category = await _service.CreateAsync(new KbCategoryRequest("Billing", null), CancellationToken.None);
        _articles.Articles.Add(KbArticle.Create(category.Id, "t", "b", null, null, Start.UtcDateTime));

        var list = await _service.ListAsync(CancellationToken.None);

        Assert.Equal(1, Assert.Single(list).ArticleCount);
    }
}
