using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Ai;

public class AiSuggestionsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record SuggestionBody(Guid ArticleId, string Title, string Summary, bool? Useful);

    private sealed record LinkedBody(string InsertText);

    [Fact]
    public async Task TheTopThreePublishedArticles_AreSuggested_NeverDrafts()
    {
        var ai = new AiApp(factory, new FakeAiTextService { IsConfigured = false });
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var published = new List<Guid>();
        for (var i = 1; i <= 5; i++)
        {
            published.Add(await AiArrange.ArticleAsync(admin, $"Password reset guide {i}", $"How to reset a password, part {i}."));
        }

        await AiArrange.ArticleAsync(admin, "Password reset draft", "Draft text about password reset.", publish: false);
        var ticketId = (await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent), "Password reset needed")).Id;

        var list = await agent.GetFromJsonAsync<SuggestionBody[]>($"/api/tickets/{ticketId}/ai-suggestions");

        Assert.Equal(3, list!.Length);
        Assert.All(list, s => Assert.Contains(s.ArticleId, published));
        Assert.DoesNotContain(list, s => s.Title.Contains("draft", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WithAi_ItsPickIsUsed()
    {
        var ai = new AiApp(factory);
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var first = await AiArrange.ArticleAsync(admin, "Invoice correction first", "Fix an invoice charge.");
        var second = await AiArrange.ArticleAsync(admin, "Invoice correction second", "Fix an invoice line.");
        var ticketId = (await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent), "Invoice correction")).Id;
        ai.Fake.Answer = $"{{\"articleIds\": [\"{second}\"]}}"; // also used (and ignored as unparsable) for the classification call

        var list = await agent.GetFromJsonAsync<SuggestionBody[]>($"/api/tickets/{ticketId}/ai-suggestions");

        Assert.Equal([second], list!.Select(s => s.ArticleId));
        Assert.NotEqual(first, list![0].ArticleId);
    }

    [Fact]
    public async Task Feedback_IsStored_ShownNextTime_AndReplacedByANewVote()
    {
        var ai = new AiApp(factory, new FakeAiTextService { IsConfigured = false });
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var other = await ai.StaffAsync(Roles.Agent);
        var article = await AiArrange.ArticleAsync(admin, "Refund policy overview", "Refunds take five days.");
        var ticketId = (await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent), "Refund policy question")).Id;

        var vote = await agent.PutAsJsonAsync($"/api/tickets/{ticketId}/ai-suggestions/{article}/feedback", new { useful = true });
        var shown = await agent.GetFromJsonAsync<SuggestionBody[]>($"/api/tickets/{ticketId}/ai-suggestions");
        await agent.PutAsJsonAsync($"/api/tickets/{ticketId}/ai-suggestions/{article}/feedback", new { useful = false });
        var replaced = await agent.GetFromJsonAsync<SuggestionBody[]>($"/api/tickets/{ticketId}/ai-suggestions");
        var others = await other.GetFromJsonAsync<SuggestionBody[]>($"/api/tickets/{ticketId}/ai-suggestions");

        Assert.Equal(HttpStatusCode.OK, vote.StatusCode);
        Assert.Equal(true, shown!.Single(s => s.ArticleId == article).Useful);
        Assert.Equal(false, replaced!.Single(s => s.ArticleId == article).Useful);
        Assert.Null(others!.Single(s => s.ArticleId == article).Useful);
    }

    [Fact]
    public async Task Feedback_Validation()
    {
        var ai = new AiApp(factory, new FakeAiTextService { IsConfigured = false });
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var draft = await AiArrange.ArticleAsync(admin, "Hidden draft article", "Not public.", publish: false);
        var published = await AiArrange.ArticleAsync(admin, "Public article", "Public text.");
        var ticketId = (await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent))).Id;

        var onDraft = await agent.PutAsJsonAsync($"/api/tickets/{ticketId}/ai-suggestions/{draft}/feedback", new { useful = true });
        var noVote = await agent.PutAsJsonAsync($"/api/tickets/{ticketId}/ai-suggestions/{published}/feedback", new { });
        var noTicket = await agent.PutAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/ai-suggestions/{published}/feedback", new { useful = true });

        Assert.Equal(HttpStatusCode.BadRequest, onDraft.StatusCode);
        using (var problem = JsonDocument.Parse(await onDraft.Content.ReadAsStringAsync()))
        {
            Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("articleId", out _));
        }

        Assert.Equal(HttpStatusCode.BadRequest, noVote.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noTicket.StatusCode);
    }

    [Fact]
    public async Task ASuggestionCanBeInsertedIntoTheReply_ThroughTheArticleLink()
    {
        var ai = new AiApp(factory, new FakeAiTextService { IsConfigured = false });
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var article = await AiArrange.ArticleAsync(admin, "Warranty terms overview", "Warranty lasts one year.");
        var ticketId = (await TicketArrange.TicketAsync(agent, await TicketArrange.CustomerAsync(agent), "Warranty terms")).Id;
        var suggestion = (await agent.GetFromJsonAsync<SuggestionBody[]>($"/api/tickets/{ticketId}/ai-suggestions"))!.Single(s => s.ArticleId == article);

        var linked = await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/articles", new { articleId = suggestion.ArticleId });

        Assert.Equal(HttpStatusCode.Created, linked.StatusCode);
        Assert.Contains("Warranty terms overview", (await linked.Content.ReadFromJsonAsync<LinkedBody>())!.InsertText);
    }

    [Fact]
    public async Task UnknownTicket_Is404_AndAnonymousIs401()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);

        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/tickets/{Guid.NewGuid()}/ai-suggestions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ai.Anonymous().GetAsync($"/api/tickets/{Guid.NewGuid()}/ai-suggestions")).StatusCode);
    }
}
