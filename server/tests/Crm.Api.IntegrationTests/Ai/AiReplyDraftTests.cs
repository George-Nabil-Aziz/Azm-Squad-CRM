using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Ai;

public class AiReplyDraftTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record SourceBody(Guid Id, string Title);

    private sealed record DraftBody(string Draft, string Language, SourceBody[] Articles);

    private sealed record MessageBody(string Body);

    private static async Task<Guid> TicketAsync(HttpClient agent, string subject = "Cannot sign in to my account")
    {
        var customerId = await TicketArrange.CustomerAsync(agent);
        return (await TicketArrange.TicketAsync(agent, customerId, subject)).Id;
    }

    [Fact]
    public async Task TheDraft_IsBasedOnThePublishedArticles_NotOnDrafts()
    {
        var ai = new AiApp(factory, new FakeAiTextService { Answer = "Hi, use the reset link. [Agent name]" });
        var admin = await ai.StaffAsync("SuperAdmin");
        var agent = await ai.StaffAsync(Roles.Agent);
        var published = await AiArrange.ArticleAsync(admin, "Resetting a forgotten account password", "Press the reset link on the sign in page.");
        await AiArrange.ArticleAsync(admin, "Internal draft about account password", "Secret draft instructions.", publish: false);
        var ticketId = await TicketAsync(agent, "Forgot account password");

        var response = await agent.PostAsync($"/api/tickets/{ticketId}/ai-reply-draft", null);
        var draft = await response.Content.ReadFromJsonAsync<DraftBody>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hi, use the reset link. [Agent name]", draft!.Draft);
        Assert.Equal("en", draft.Language);
        Assert.Equal([published], draft.Articles.Select(a => a.Id));
        var prompt = ai.Fake.Requests.Single().User;
        Assert.Contains("Press the reset link on the sign in page.", prompt);
        Assert.DoesNotContain("Secret draft instructions.", prompt);
    }

    [Fact]
    public async Task TheDraft_IsNeverSent_TheThreadAndTicketStayTheSame()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);
        var ticketId = await TicketAsync(agent);
        await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/messages", new { body = "Looking into it." });
        var threadBefore = await agent.GetStringAsync($"/api/tickets/{ticketId}/messages");
        var ticketBefore = await agent.GetStringAsync($"/api/tickets/{ticketId}");

        var response = await agent.PostAsync($"/api/tickets/{ticketId}/ai-reply-draft", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(threadBefore, await agent.GetStringAsync($"/api/tickets/{ticketId}/messages"));
        Assert.Equal(ticketBefore, await agent.GetStringAsync($"/api/tickets/{ticketId}"));
    }

    [Fact]
    public async Task TheLanguage_IsThatOfTheCustomersLastMessage()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);
        var ticketId = await TicketAsync(agent);
        await AiArrange.AddCustomerMessageAsync(ai, factory, ticketId, "I still cannot sign in, please help me with this");
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await AiArrange.AddCustomerMessageAsync(ai, factory, ticketId, "ما زلت لا أستطيع تسجيل الدخول إلى حسابي، أرجو المساعدة");

        var draft = await (await agent.PostAsync($"/api/tickets/{ticketId}/ai-reply-draft", null)).Content.ReadFromJsonAsync<DraftBody>();

        Assert.Equal("ar", draft!.Language);
        Assert.Contains("Arabic", ai.Fake.Requests.Single().System);
    }

    [Fact]
    public async Task InternalNotes_AndPersonalData_NeverReachTheAi()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);
        var ticketId = await TicketAsync(agent);
        await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/messages", new { body = "Customer is a VIP, escalate.", @internal = true });
        await AiArrange.AddCustomerMessageAsync(ai, factory, ticketId, "Call me on +966 50 123 4567 or nour@corp.example");

        await agent.PostAsync($"/api/tickets/{ticketId}/ai-reply-draft", null);

        var sent = ai.Fake.Requests.Single().User + ai.Fake.Requests.Single().System;
        Assert.DoesNotContain("VIP", sent);
        Assert.DoesNotContain("123 4567", sent);
        Assert.DoesNotContain("nour@corp.example", sent);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.BadGateway)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    public async Task WhenTheAiFails_TheAgentCanStillReplyNormally(bool configured, HttpStatusCode expected)
    {
        var ai = new AiApp(factory, new FakeAiTextService { IsConfigured = configured, Fail = configured });
        var agent = await ai.StaffAsync(Roles.Agent);
        var ticketId = await TicketAsync(agent);

        var failed = await agent.PostAsync($"/api/tickets/{ticketId}/ai-reply-draft", null);
        var reply = await agent.PostAsJsonAsync($"/api/tickets/{ticketId}/messages", new { body = "Hello, I wrote this myself." });

        Assert.Equal(expected, failed.StatusCode);
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        Assert.Equal("Hello, I wrote this myself.", (await reply.Content.ReadFromJsonAsync<MessageBody>())!.Body);
    }

    [Fact]
    public async Task UnknownTicket_Is404_AndAnonymousIs401()
    {
        var ai = new AiApp(factory);
        var agent = await ai.StaffAsync(Roles.Agent);

        Assert.Equal(HttpStatusCode.NotFound, (await agent.PostAsync($"/api/tickets/{Guid.NewGuid()}/ai-reply-draft", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ai.Anonymous().PostAsync($"/api/tickets/{Guid.NewGuid()}/ai-reply-draft", null)).StatusCode);
    }
}
