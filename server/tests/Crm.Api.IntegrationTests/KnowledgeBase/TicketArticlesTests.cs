using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace Crm.Api.IntegrationTests.KnowledgeBase;

public class TicketArticlesTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private const string PortalBaseUrl = "https://help.example.com";

    internal sealed record LinkedBody(Guid Id, Guid ArticleId, string Title, string Summary, string Url, string InsertText, DateTime LinkedAt);

    internal sealed record ListedBody(Guid Id, Guid ArticleId, string Title, DateTime LinkedAt, Guid? LinkedById, string? LinkedByName);

    private sealed record IdBody(Guid Id);

    private sealed record ArticleCountBody(Guid Id, int LinkedCount);

    private WebApplicationFactory<Program> App => factory.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Portal:BaseUrl"] = PortalBaseUrl })));

    private static async Task<Guid> PublishedArticleAsync(HttpClient admin, bool publish = true, string title = "Reset your password", string body = "Use the reset link on the sign-in page.")
    {
        var category = await (await admin.PostAsJsonAsync("/api/kb/categories", new { nameEn = $"Cat {Guid.NewGuid():N}" })).Content.ReadFromJsonAsync<IdBody>();
        var article = await (await admin.PostAsJsonAsync("/api/kb/articles", new { categoryId = category!.Id, titleEn = title, bodyEn = body }))
            .Content.ReadFromJsonAsync<IdBody>();
        if (publish)
        {
            Assert.True((await admin.PostAsync($"/api/kb/articles/{article!.Id}/publish", null)).IsSuccessStatusCode);
        }

        return article!.Id;
    }

    private static HttpClient Authenticated(WebApplicationFactory<Program> app, string token)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(HttpClient Admin, HttpClient Agent, Guid TicketId)> ArrangeAsync(WebApplicationFactory<Program>? app = null)
    {
        app ??= App;
        var admin = Authenticated(app, await factory.LoginAsync());
        var agentEmail = $"agent-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(agentEmail, CrmApiFactory.TestUserPassword, Roles.Agent);
        var agent = Authenticated(app, await factory.LoginAsync(agentEmail, CrmApiFactory.TestUserPassword));
        var customerId = await TicketArrange.CustomerAsync(agent);
        return (admin, agent, (await TicketArrange.TicketAsync(agent, customerId)).Id);
    }

    [Fact]
    public async Task InsertingAnArticle_ReturnsItsLinkAndSummary_ForTheReply()
    {
        var (admin, agent, ticketId) = await ArrangeAsync();
        var articleId = await PublishedArticleAsync(admin);

        var response = await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId });
        var linked = await response.Content.ReadFromJsonAsync<LinkedBody>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Reset your password", linked!.Title);
        Assert.Equal("Use the reset link on the sign-in page.", linked.Summary);
        Assert.Equal($"{PortalBaseUrl}/portal/kb/articles/{articleId}", linked.Url);
        Assert.Contains(linked.Title, linked.InsertText);
        Assert.Contains(linked.Summary, linked.InsertText);
        Assert.Contains(linked.Url, linked.InsertText);
    }

    [Fact]
    public async Task TheTicketRecordsWhichArticlesWereLinked()
    {
        var (admin, agent, ticketId) = await ArrangeAsync();
        var first = await PublishedArticleAsync(admin, title: "First article");
        var second = await PublishedArticleAsync(admin, title: "Second article");
        await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId = first });
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId = second });

        var list = await agent.GetFromJsonAsync<ListedBody[]>($"/api/tickets/{ticketId}/articles");

        Assert.Equal(["Second article", "First article"], list!.Select(l => l.Title));
        Assert.Equal([second, first], list!.Select(l => l.ArticleId));
        Assert.All(list!, l => Assert.NotNull(l.LinkedById));
        Assert.All(list!, l => Assert.Equal(DateTimeKind.Utc, l.LinkedAt.Kind));
    }

    [Fact]
    public async Task EachArticleStoresHowOftenItWasLinked()
    {
        var (admin, agent, ticketId) = await ArrangeAsync();
        var articleId = await PublishedArticleAsync(admin);
        var (_, _, otherTicket) = await ArrangeAsync();

        await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId });
        await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId });
        await agent.PostAsJsonAsync($"/api/tickets/{otherTicket}/articles", new { articleId });

        var article = await admin.GetFromJsonAsync<ArticleCountBody>($"/api/kb/articles/{articleId}");
        Assert.Equal(3, article!.LinkedCount);
    }

    [Fact]
    public async Task LinkingAnUnpublishedArticle_Returns400_AndCountsNothing()
    {
        var (admin, agent, ticketId) = await ArrangeAsync();
        var draft = await PublishedArticleAsync(admin, publish: false);

        var response = await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId = draft });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("articleId", out _));
        Assert.Equal(0, (await admin.GetFromJsonAsync<ArticleCountBody>($"/api/kb/articles/{draft}"))!.LinkedCount);
        Assert.Empty((await agent.GetFromJsonAsync<ListedBody[]>($"/api/tickets/{ticketId}/articles"))!);
    }

    [Fact]
    public async Task MissingArticle_UnknownArticle_AndUnknownTicket_AreRejected()
    {
        var (admin, agent, ticketId) = await ArrangeAsync();
        var articleId = await PublishedArticleAsync(admin);

        var missing = await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { });
        var unknownArticle = await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId = Guid.NewGuid() });
        var unknownTicket = await agent.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/articles", new { articleId });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownArticle.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownTicket.StatusCode);
    }

    [Fact]
    public async Task Anonymous_Gets401()
    {
        var (admin, _, ticketId) = await ArrangeAsync();
        var articleId = await PublishedArticleAsync(admin);

        var anonymous = await factory.CreateClient().PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/tickets/{ticketId}/articles")).StatusCode);
    }

    [Fact]
    public async Task TheLanguageCanBeChosen()
    {
        var (admin, agent, ticketId) = await ArrangeAsync();
        var category = await (await admin.PostAsJsonAsync("/api/kb/categories", new { nameEn = $"Cat {Guid.NewGuid():N}" })).Content.ReadFromJsonAsync<IdBody>();
        var article = await (await admin.PostAsJsonAsync("/api/kb/articles",
            new { categoryId = category!.Id, titleEn = "Reset", bodyEn = "English body", titleAr = "إعادة", bodyAr = "محتوى عربي" })).Content.ReadFromJsonAsync<IdBody>();
        await admin.PostAsync($"/api/kb/articles/{article!.Id}/publish", null);

        var arabic = await (await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId = article.Id, language = "ar" }))
            .Content.ReadFromJsonAsync<LinkedBody>();
        var invalid = await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId = article.Id, language = "fr" });

        Assert.Equal("إعادة", arabic!.Title);
        Assert.Equal("محتوى عربي", arabic.Summary);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
