using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Ai;

public class AiSummaryTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    internal sealed record SummaryBody(string? Text, string? Language, DateTime? GeneratedAt);

    private sealed record StatusBody(bool Enabled);

    private static async Task<Guid> TicketWithMessagesAsync(HttpClient agent, params string[] customerMessages)
    {
        var customerId = await TicketArrange.CustomerAsync(agent);
        var ticket = await TicketArrange.TicketAsync(agent, customerId, "Invoice is wrong");
        foreach (var body in customerMessages)
        {
            Assert.Equal(HttpStatusCode.Created, (await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages", new { body })).StatusCode);
        }

        return ticket.Id;
    }

    [Fact]
    public async Task OneClick_GeneratesTheSummary_AndItIsSavedWithATimestamp()
    {
        var ai = new AiApp(factory, new FakeAiTextService { Answer = "- Duplicate charge" });
        var agent = await ai.StaffAsync(Roles.Agent);
        var ticketId = await TicketWithMessagesAsync(agent, "We are checking line 3.");

        var empty = await agent.GetFromJsonAsync<SummaryBody>($"/api/tickets/{ticketId}/ai-summary");
        var response = await agent.PostAsync($"/api/tickets/{ticketId}/ai-summary", null);
        var created = await response.Content.ReadFromJsonAsync<SummaryBody>();
        var saved = await agent.GetFromJsonAsync<SummaryBody>($"/api/tickets/{ticketId}/ai-summary");

        Assert.Equal(new SummaryBody(null, null, null), empty);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("- Duplicate charge", created!.Text);
        Assert.Equal(created, saved);
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, saved!.GeneratedAt);
        Assert.Contains("Line 3 is charged twice.", ai.Fake.Requests.Last().User);
    }

    [Fact]
    public async Task Regenerating_ReplacesTheSummary_AndMovesTheTimestamp()
    {
        var ai = new AiApp(factory);
        ai.Fake.Answers.AddRange(["first", "second"]);
        var agent = await ai.StaffAsync(Roles.Agent);
        var ticketId = await TicketWithMessagesAsync(agent);
        await agent.PostAsync($"/api/tickets/{ticketId}/ai-summary", null);
        factory.Time.Advance(TimeSpan.FromMinutes(30));

        await agent.PostAsync($"/api/tickets/{ticketId}/ai-summary", null);
        var saved = await agent.GetFromJsonAsync<SummaryBody>($"/api/tickets/{ticketId}/ai-summary");

        Assert.Equal("second", saved!.Text);
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, saved.GeneratedAt);
    }

    [Fact]
    public async Task TheSummary_FollowsTheTicketLanguage()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        var response = await agent.PostAsJsonAsync("/api/tickets",
            new { customerId, subject = "مشكلة في الفاتورة", description = "الفاتورة فيها خطأ في البند الثالث وأحتاج مساعدة", priority = "mid" });
        var ticketId = (await response.Content.ReadFromJsonAsync<TicketBody>())!.Id;

        await agent.PostAsync($"/api/tickets/{ticketId}/ai-summary", null);

        Assert.Contains("Arabic", ai.Fake.Requests.Last().System);
        Assert.Equal("ar", (await agent.GetFromJsonAsync<SummaryBody>($"/api/tickets/{ticketId}/ai-summary"))!.Language);
    }

    [Fact]
    public async Task WhenTheAiFails_ItIs502_TheOldSummaryStays_AndTheTicketIsUntouched()
    {
        var ai = new AiApp(factory, new FakeAiTextService { Answer = "kept" });
        var agent = await ai.StaffAsync(Roles.Agent);
        var ticketId = await TicketWithMessagesAsync(agent);
        await agent.PostAsync($"/api/tickets/{ticketId}/ai-summary", null);
        var ticketBefore = await agent.GetStringAsync($"/api/tickets/{ticketId}");
        ai.Fake.Fail = true;
        factory.Time.Advance(TimeSpan.FromMinutes(20));

        var response = await agent.PostAsync($"/api/tickets/{ticketId}/ai-summary", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(502, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("kept", (await agent.GetFromJsonAsync<SummaryBody>($"/api/tickets/{ticketId}/ai-summary"))!.Text);
        Assert.Equal(ticketBefore, await agent.GetStringAsync($"/api/tickets/{ticketId}"));
    }

    [Fact]
    public async Task WithoutAnApiKey_ItIs503_AndStatusSaysDisabled()
    {
        var ai = new AiApp(factory, new FakeAiTextService { IsConfigured = false });
        var agent = await ai.StaffAsync(Roles.Agent);
        var ticketId = await TicketWithMessagesAsync(agent);

        var response = await agent.PostAsync($"/api/tickets/{ticketId}/ai-summary", null);
        var status = await agent.GetFromJsonAsync<StatusBody>("/api/ai/status");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.False(status!.Enabled);
        Assert.Empty(ai.Fake.Requests);
    }

    [Fact]
    public async Task Status_IsEnabledWhenAKeyIsConfigured_AndNeedsSignIn()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);

        Assert.True((await agent.GetFromJsonAsync<StatusBody>("/api/ai/status"))!.Enabled);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ai.Anonymous().GetAsync("/api/ai/status")).StatusCode);
    }

    [Fact]
    public async Task EmailsAndPhones_AreMaskedInThePromptSentToTheAi()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);
        var ticketId = await TicketWithMessagesAsync(agent, "Reach me at nour@corp.example or +966 50 123 4567.");

        await agent.PostAsync($"/api/tickets/{ticketId}/ai-summary", null);

        var prompt = ai.Fake.Requests.Last();
        var sent = prompt.System + prompt.User;
        Assert.DoesNotContain("nour@corp.example", sent);
        Assert.DoesNotContain("123 4567", sent);
        Assert.Contains("[email]", sent);
        Assert.Contains("[phone]", sent);
    }

    [Fact]
    public async Task UnknownTicket_Is404_AndAnonymousIs401()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);

        Assert.Equal(HttpStatusCode.NotFound, (await agent.PostAsync($"/api/tickets/{Guid.NewGuid()}/ai-summary", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/tickets/{Guid.NewGuid()}/ai-summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ai.Anonymous().PostAsync($"/api/tickets/{Guid.NewGuid()}/ai-summary", null)).StatusCode);
    }
}
