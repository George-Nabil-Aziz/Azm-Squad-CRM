using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.KnowledgeBase;

public class KbSearchTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    internal sealed record Hit(string Type, Guid Id, string Title, string Snippet, int Score);

    private sealed record IdBody(Guid Id);

    private async Task<(HttpClient Admin, Guid CategoryId)> SetUpAsync()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await (await admin.PostAsJsonAsync("/api/kb/categories", new { nameEn = $"Cat {Guid.NewGuid():N}" }))
            .Content.ReadFromJsonAsync<IdBody>();
        return (admin, category!.Id);
    }

    private static async Task<Guid> ArticleAsync(
        HttpClient admin, Guid categoryId, string? titleEn, string? bodyEn, bool publish = true, string? titleAr = null, string? bodyAr = null)
    {
        var article = await (await admin.PostAsJsonAsync("/api/kb/articles", new { categoryId, titleEn, bodyEn, titleAr, bodyAr }))
            .Content.ReadFromJsonAsync<IdBody>();
        if (publish)
        {
            Assert.True((await admin.PostAsync($"/api/kb/articles/{article!.Id}/publish", null)).IsSuccessStatusCode);
        }

        return article!.Id;
    }

    private static async Task<Guid> FaqAsync(HttpClient admin, string question, string answer, bool published = true, string? questionAr = null, string? answerAr = null)
    {
        var faq = await (await admin.PostAsJsonAsync("/api/kb/faqs",
            new { questionEn = question, answerEn = answer, questionAr, answerAr, isPublished = published })).Content.ReadFromJsonAsync<IdBody>();
        return faq!.Id;
    }

    [Fact]
    public async Task AKeyword_ReturnsMatchingArticlesAndFaqs_RankedByRelevance()
    {
        var (admin, categoryId) = await SetUpAsync();
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var word = $"zebra{Guid.NewGuid():N}";
        var inBody = await ArticleAsync(admin, categoryId, "General help", $"Contact us about {word} anytime.");
        var inTitle = await ArticleAsync(admin, categoryId, $"All about {word}", "Short text.");
        var faq = await FaqAsync(admin, $"What is {word}?", "An animal.");

        var hits = await agent.GetFromJsonAsync<Hit[]>($"/api/kb/search?q={word}");

        Assert.Equal(3, hits!.Length);
        Assert.Equal(new[] { inTitle, faq }.Order(), hits.Take(2).Select(h => h.Id).Order());
        Assert.Equal(inBody, hits[2].Id);
        Assert.Equal("faq", hits.Single(h => h.Id == faq).Type);
        Assert.Equal("article", hits.Single(h => h.Id == inTitle).Type);
        Assert.True(hits[0].Score >= hits[1].Score && hits[1].Score >= hits[2].Score);
    }

    [Fact]
    public async Task ArabicSearch_Works_WithNormalization()
    {
        var (admin, categoryId) = await SetUpAsync();
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var tag = Guid.NewGuid().ToString("N");
        var article = await ArticleAsync(admin, categoryId, null, null, titleAr: $"إدارة المدرسة {tag}", bodyAr: "كيف تدير الحساب");

        var withoutHamza = await agent.GetFromJsonAsync<Hit[]>($"/api/kb/search?q={Uri.EscapeDataString("اداره المدرسه " + tag)}");
        var original = await agent.GetFromJsonAsync<Hit[]>($"/api/kb/search?q={Uri.EscapeDataString("إدارة المدرسة")}");

        Assert.Equal([article], withoutHamza!.Select(h => h.Id));
        Assert.Contains(original!, h => h.Id == article);
    }

    [Fact]
    public async Task Drafts_AreNeverReturned_NotEvenToEditors()
    {
        var (admin, categoryId) = await SetUpAsync();
        var word = $"quokka{Guid.NewGuid():N}";
        var draft = await ArticleAsync(admin, categoryId, $"Draft {word}", "Body", publish: false);
        var hiddenFaq = await FaqAsync(admin, $"Hidden {word}?", "No.", published: false);
        var live = await ArticleAsync(admin, categoryId, $"Live {word}", "Body");

        var forEditor = await admin.GetFromJsonAsync<Hit[]>($"/api/kb/search?q={word}");
        var forPortal = await factory.CreateClient().GetFromJsonAsync<Hit[]>($"/api/portal/kb/search?q={word}");

        Assert.Equal([live], forEditor!.Select(h => h.Id));
        Assert.Equal([live], forPortal!.Select(h => h.Id));
        Assert.DoesNotContain(forEditor!, h => h.Id == draft || h.Id == hiddenFaq);
    }

    [Fact]
    public async Task AnUnpublishedArticle_DisappearsFromTheSearch()
    {
        var (admin, categoryId) = await SetUpAsync();
        var word = $"okapi{Guid.NewGuid():N}";
        var article = await ArticleAsync(admin, categoryId, $"About {word}", "Body");
        Assert.Single((await admin.GetFromJsonAsync<Hit[]>($"/api/kb/search?q={word}"))!);

        await admin.PostAsync($"/api/kb/articles/{article}/unpublish", null);

        Assert.Empty((await admin.GetFromJsonAsync<Hit[]>($"/api/kb/search?q={word}"))!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyQuery_Returns200WithNoResults(string query)
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var response = await agent.GetAsync($"/api/kb/search?q={Uri.EscapeDataString(query)}");
        var missing = await agent.GetAsync("/api/kb/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<Hit[]>())!);
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Empty((await missing.Content.ReadFromJsonAsync<Hit[]>())!);
    }

    [Fact]
    public async Task WildcardCharacters_AreMatchedLiterally()
    {
        var (admin, categoryId) = await SetUpAsync();
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var tag = Guid.NewGuid().ToString("N");
        await ArticleAsync(admin, categoryId, $"Discount {tag}", "Body");

        var hits = await agent.GetFromJsonAsync<Hit[]>($"/api/kb/search?q={Uri.EscapeDataString("%")}");

        Assert.DoesNotContain(hits!, h => h.Title.Contains(tag, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheStaffSearch_NeedsASignIn_ThePortalSearchDoesNot()
    {
        var staff = await factory.CreateClient().GetAsync("/api/kb/search?q=anything");
        var portal = await factory.CreateClient().GetAsync("/api/portal/kb/search?q=anything");

        Assert.Equal(HttpStatusCode.Unauthorized, staff.StatusCode);
        Assert.Equal(HttpStatusCode.OK, portal.StatusCode);
    }
}
