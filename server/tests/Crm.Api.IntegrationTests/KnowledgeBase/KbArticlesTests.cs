using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.KnowledgeBase;

public class KbArticlesTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    internal sealed record CategoryBody(Guid Id, string? NameEn, string? NameAr, string Name, int ArticleCount);

    internal sealed record ArticleBody(
        Guid Id, Guid CategoryId, string CategoryName, string? TitleEn, string? BodyEn, string? TitleAr, string? BodyAr,
        string Title, string Status, DateTime? PublishedAt, int HelpfulCount, int NotHelpfulCount, int LinkedCount);

    internal sealed record PageBody(ArticleBody[] Items, int Page, int PageSize, int TotalCount);

    private async Task<CategoryBody> CreateCategoryAsync(HttpClient editor, string? name = null)
    {
        var response = await editor.PostAsJsonAsync("/api/kb/categories", new { nameEn = name ?? $"Cat {Guid.NewGuid():N}", nameAr = "فئة" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoryBody>())!;
    }

    private static async Task<ArticleBody> CreateArticleAsync(HttpClient editor, Guid categoryId, string title = "Reset your password")
    {
        var response = await editor.PostAsJsonAsync("/api/kb/articles", new { categoryId, titleEn = title, bodyEn = "Use the reset link." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ArticleBody>())!;
    }

    [Fact]
    public async Task CreatingAnArticle_SavesItAsDraft()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await CreateCategoryAsync(editor);

        var response = await editor.PostAsJsonAsync("/api/kb/articles",
            new { categoryId = category.Id, titleEn = "Reset your password", bodyEn = "Use the reset link." });
        var article = await response.Content.ReadFromJsonAsync<ArticleBody>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/kb/articles/{article!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("draft", article.Status);
        Assert.Null(article.PublishedAt);
        Assert.Equal(category.Id, article.CategoryId);
        Assert.Equal(category.NameEn, article.CategoryName);
    }

    [Fact]
    public async Task Publishing_MakesItVisibleToAgents_AndDraftsStayHidden()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var category = await CreateCategoryAsync(editor);
        var draft = await CreateArticleAsync(editor, category.Id, "Draft only");
        var live = await CreateArticleAsync(editor, category.Id, "Published one");

        var draftResponse = await agent.GetAsync($"/api/kb/articles/{draft.Id}");
        var beforePublish = await agent.GetAsync($"/api/kb/articles/{live.Id}");
        var published = await (await editor.PostAsync($"/api/kb/articles/{live.Id}/publish", null)).Content.ReadFromJsonAsync<ArticleBody>();
        var afterPublish = await agent.GetFromJsonAsync<ArticleBody>($"/api/kb/articles/{live.Id}");
        var list = await agent.GetFromJsonAsync<PageBody>($"/api/kb/articles?categoryId={category.Id}&status=draft");

        Assert.Equal(HttpStatusCode.NotFound, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, beforePublish.StatusCode);
        Assert.Equal("published", published!.Status);
        Assert.NotNull(published.PublishedAt);
        Assert.Equal("published", afterPublish!.Status);
        Assert.Equal([live.Id], list!.Items.Select(a => a.Id));
    }

    [Fact]
    public async Task Editors_SeeDrafts_InTheList()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await CreateCategoryAsync(editor);
        var draft = await CreateArticleAsync(editor, category.Id);

        var list = await editor.GetFromJsonAsync<PageBody>($"/api/kb/articles?categoryId={category.Id}&status=draft");

        Assert.Equal([draft.Id], list!.Items.Select(a => a.Id));
        Assert.Equal(1, list.TotalCount);
    }

    [Fact]
    public async Task Unpublishing_HidesTheArticleAgain()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var article = await CreateArticleAsync(editor, (await CreateCategoryAsync(editor)).Id);
        await editor.PostAsync($"/api/kb/articles/{article.Id}/publish", null);

        var unpublished = await (await editor.PostAsync($"/api/kb/articles/{article.Id}/unpublish", null)).Content.ReadFromJsonAsync<ArticleBody>();

        Assert.Equal("draft", unpublished!.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/kb/articles/{article.Id}")).StatusCode);
    }

    [Fact]
    public async Task AnArticle_CanHaveArabicAndEnglishVersions_AndTheTitleFollowsTheLanguage()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await CreateCategoryAsync(editor);

        var created = await (await editor.PostAsJsonAsync("/api/kb/articles", new
        {
            categoryId = category.Id,
            titleEn = "Reset your password",
            bodyEn = "Use the link.",
            titleAr = "إعادة تعيين كلمة المرور",
            bodyAr = "استخدم الرابط.",
        })).Content.ReadFromJsonAsync<ArticleBody>();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/kb/articles/{created!.Id}");
        request.Headers.AcceptLanguage.ParseAdd("ar");
        var arabic = await (await editor.SendAsync(request)).Content.ReadFromJsonAsync<ArticleBody>();
        var english = await editor.GetFromJsonAsync<ArticleBody>($"/api/kb/articles/{created.Id}");

        Assert.Equal("إعادة تعيين كلمة المرور", created.TitleAr);
        Assert.Equal("استخدم الرابط.", created.BodyAr);
        Assert.Equal("إعادة تعيين كلمة المرور", arabic!.Title);
        Assert.Equal("Reset your password", english!.Title);
    }

    [Fact]
    public async Task AnArabicOnlyArticle_IsAccepted_AndEnglishReadersGetTheArabicTitle()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await CreateCategoryAsync(editor);

        var created = await (await editor.PostAsJsonAsync("/api/kb/articles",
            new { categoryId = category.Id, titleAr = "عنوان", bodyAr = "محتوى" })).Content.ReadFromJsonAsync<ArticleBody>();

        Assert.Null(created!.TitleEn);
        Assert.Equal("عنوان", created.Title);
    }

    [Fact]
    public async Task MissingTitle_Returns400_WithTheTitleField()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await CreateCategoryAsync(editor);

        var response = await editor.PostAsJsonAsync("/api/kb/articles", new { categoryId = category.Id, bodyEn = "Body only" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("title", out _));
    }

    [Fact]
    public async Task UnknownCategory_Returns400_OnCategoryId()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var response = await editor.PostAsJsonAsync("/api/kb/articles",
            new { categoryId = Guid.NewGuid(), titleEn = "T", bodyEn = "B" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("categoryId", out _));
    }

    [Fact]
    public async Task UpdatingAndDeletingAnArticle_Works_AndADeletedArticleIsGone()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await CreateCategoryAsync(editor);
        var article = await CreateArticleAsync(editor, category.Id);

        var updated = await (await editor.PutAsJsonAsync($"/api/kb/articles/{article.Id}",
            new { categoryId = category.Id, titleEn = "New title", bodyEn = "New body" })).Content.ReadFromJsonAsync<ArticleBody>();
        var deleted = await editor.DeleteAsync($"/api/kb/articles/{article.Id}");
        var after = await editor.GetAsync($"/api/kb/articles/{article.Id}");

        Assert.Equal("New title", updated!.TitleEn);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task Categories_CanBeEditedAndDeleted_OnlyWhenEmpty()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await CreateCategoryAsync(editor);
        await CreateArticleAsync(editor, category.Id);

        var renamed = await (await editor.PutAsJsonAsync($"/api/kb/categories/{category.Id}",
            new { nameEn = "Renamed", nameAr = "اسم جديد" })).Content.ReadFromJsonAsync<CategoryBody>();
        var blocked = await editor.DeleteAsync($"/api/kb/categories/{category.Id}");
        var empty = await CreateCategoryAsync(editor);
        var removed = await editor.DeleteAsync($"/api/kb/categories/{empty.Id}");
        var list = await editor.GetFromJsonAsync<CategoryBody[]>("/api/kb/categories");

        Assert.Equal("Renamed", renamed!.NameEn);
        Assert.Equal(1, renamed.ArticleCount);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Contains(list!, c => c.Id == category.Id);
        Assert.DoesNotContain(list!, c => c.Id == empty.Id);
    }

    [Fact]
    public async Task CategoryWithoutAName_Returns400()
    {
        var editor = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var response = await editor.PostAsJsonAsync("/api/kb/categories", new { nameEn = " ", nameAr = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class KbAuthorizationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact]
    public async Task WithoutAToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/kb/articles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnAgent_CanRead_ButNotWrite()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var read = await agent.GetAsync("/api/kb/articles");
        var categories = await agent.GetAsync("/api/kb/categories");
        var create = await agent.PostAsJsonAsync("/api/kb/articles", new { categoryId = Guid.NewGuid(), titleEn = "T", bodyEn = "B" });
        var createCategory = await agent.PostAsJsonAsync("/api/kb/categories", new { nameEn = "X" });
        var publish = await agent.PostAsync($"/api/kb/articles/{Guid.NewGuid()}/publish", null);
        var delete = await agent.DeleteAsync($"/api/kb/articles/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Admins_CanWrite()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var response = await admin.PostAsJsonAsync("/api/kb/categories", new { nameEn = $"Cat {Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
