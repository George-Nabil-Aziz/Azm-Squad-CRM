using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Portal;

public class PortalKbTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record IdBody(Guid Id);

    private sealed record CategoryBody(Guid Id, string Name, int ArticleCount);

    private sealed record ListBody(ArticleSummary[] Items, int TotalCount);

    private sealed record ArticleSummary(Guid Id, string Title, string Summary, string CategoryName);

    private sealed record ArticleBody(Guid Id, string Title, string Body, string CategoryName, int HelpfulCount, int NotHelpfulCount);

    private sealed record FeedbackBody(int HelpfulCount, int NotHelpfulCount);

    private async Task<(HttpClient Admin, Guid CategoryId)> SetUpAsync(string? nameAr = null)
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await (await admin.PostAsJsonAsync("/api/kb/categories", new { nameEn = $"Cat {Guid.NewGuid():N}", nameAr }))
            .Content.ReadFromJsonAsync<IdBody>();
        return (admin, category!.Id);
    }

    private static async Task<Guid> ArticleAsync(
        HttpClient admin, Guid categoryId, string title, bool publish = true, string? titleAr = null, string? bodyAr = null)
    {
        var article = await (await admin.PostAsJsonAsync("/api/kb/articles",
            new { categoryId, titleEn = title, bodyEn = $"Body of {title}", titleAr, bodyAr })).Content.ReadFromJsonAsync<IdBody>();
        if (publish)
        {
            await admin.PostAsync($"/api/kb/articles/{article!.Id}/publish", null);
        }

        return article!.Id;
    }

    [Fact]
    public async Task FaqsAndPublishedArticles_AreVisibleWithoutSigningIn()
    {
        var (admin, categoryId) = await SetUpAsync();
        var live = await ArticleAsync(admin, categoryId, $"Live {Guid.NewGuid():N}");
        var draft = await ArticleAsync(admin, categoryId, $"Draft {Guid.NewGuid():N}", publish: false);
        var visitor = factory.CreateClient();

        var categories = await visitor.GetFromJsonAsync<CategoryBody[]>("/api/portal/kb/categories");
        var list = await visitor.GetFromJsonAsync<ListBody>($"/api/portal/kb/articles?categoryId={categoryId}");
        var article = await visitor.GetFromJsonAsync<ArticleBody>($"/api/portal/kb/articles/{live}");
        var faqs = await visitor.GetAsync("/api/portal/kb/faqs");

        Assert.Equal(1, categories!.Single(c => c.Id == categoryId).ArticleCount);
        Assert.Equal([live], list!.Items.Select(a => a.Id));
        Assert.Contains("Body of Live", article!.Body);
        Assert.Equal(HttpStatusCode.OK, faqs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/portal/kb/articles/{draft}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/portal/kb/articles/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task ACategoryWithOnlyDrafts_IsNotListed()
    {
        var (admin, categoryId) = await SetUpAsync();
        await ArticleAsync(admin, categoryId, "Draft only", publish: false);

        var categories = await factory.CreateClient().GetFromJsonAsync<CategoryBody[]>("/api/portal/kb/categories");

        Assert.DoesNotContain(categories!, c => c.Id == categoryId);
    }

    [Fact]
    public async Task ContentFollowsThePortalLanguage()
    {
        var (admin, categoryId) = await SetUpAsync("الفواتير");
        var id = await ArticleAsync(admin, categoryId, "Reset", titleAr: "إعادة التعيين", bodyAr: "المحتوى بالعربية");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/portal/kb/articles/{id}");
        request.Headers.AcceptLanguage.ParseAdd("ar");

        var arabic = await (await factory.CreateClient().SendAsync(request)).Content.ReadFromJsonAsync<ArticleBody>();
        var english = await factory.CreateClient().GetFromJsonAsync<ArticleBody>($"/api/portal/kb/articles/{id}");

        Assert.Equal(("إعادة التعيين", "المحتوى بالعربية", "الفواتير"), (arabic!.Title, arabic.Body, arabic.CategoryName));
        Assert.Equal("Reset", english!.Title);
    }

    [Fact]
    public async Task SearchWorksInThePortal_ForPublishedContentOnly()
    {
        var (admin, categoryId) = await SetUpAsync();
        var word = $"giraffe{Guid.NewGuid():N}";
        var live = await ArticleAsync(admin, categoryId, $"About {word}");
        await ArticleAsync(admin, categoryId, $"Draft {word}", publish: false);

        var hits = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/portal/kb/search?q={word}");

        Assert.Equal([live], hits.EnumerateArray().Select(h => h.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task WasThisHelpful_CountsVotes_AndTheStaffListShowsThem()
    {
        var (admin, categoryId) = await SetUpAsync();
        var id = await ArticleAsync(admin, categoryId, "Invoices");
        var visitor = factory.CreateClient();

        await visitor.PostAsJsonAsync($"/api/portal/kb/articles/{id}/feedback", new { helpful = true });
        await visitor.PostAsJsonAsync($"/api/portal/kb/articles/{id}/feedback", new { helpful = true });
        var response = await visitor.PostAsJsonAsync($"/api/portal/kb/articles/{id}/feedback", new { helpful = false });
        var counts = await response.Content.ReadFromJsonAsync<FeedbackBody>();
        var staff = await admin.GetFromJsonAsync<JsonElement>($"/api/kb/articles/{id}");
        var shown = await visitor.GetFromJsonAsync<ArticleBody>($"/api/portal/kb/articles/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((2, 1), (counts!.HelpfulCount, counts.NotHelpfulCount));
        Assert.Equal(2, staff.GetProperty("helpfulCount").GetInt32());
        Assert.Equal(1, staff.GetProperty("notHelpfulCount").GetInt32());
        Assert.Equal((2, 1), (shown!.HelpfulCount, shown.NotHelpfulCount));
    }

    [Fact]
    public async Task Feedback_WithoutAValue_Returns400_AndOnADraftReturns404()
    {
        var (admin, categoryId) = await SetUpAsync();
        var live = await ArticleAsync(admin, categoryId, "Live");
        var draft = await ArticleAsync(admin, categoryId, "Draft", publish: false);
        var visitor = factory.CreateClient();

        var missing = await visitor.PostAsJsonAsync($"/api/portal/kb/articles/{live}/feedback", new { });
        var onDraft = await visitor.PostAsJsonAsync($"/api/portal/kb/articles/{draft}/feedback", new { helpful = true });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var problem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("helpful", out _));
        Assert.Equal(HttpStatusCode.NotFound, onDraft.StatusCode);
    }
}
