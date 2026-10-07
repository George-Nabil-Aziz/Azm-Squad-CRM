using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Portal;
using Crm.Application.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.IntegrationTests.Ai;

public class PortalChatbotTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record SourceBody(Guid Id, string Title);

    private sealed record TicketBody(Guid Id, string Number);

    private sealed record ReplyBody(string Answer, string Language, SourceBody[] Sources, string Outcome, bool OfferAgent, bool SignInRequired, TicketBody? Ticket);

    private sealed record StatusBody(bool Enabled);

    private sealed record PortalTicketBody(Guid Id, string Subject, string? Description);

    private PortalApp Create(FakeAiTextService fake) =>
        PortalApp.Create(factory, null, services =>
        {
            services.RemoveAll<IAiTextService>();
            services.AddSingleton<IAiTextService>(fake);
        });

    private static string Json(bool canAnswer, string answer, Guid[] ids, string confidence) =>
        $"{{\"canAnswer\": {canAnswer.ToString().ToLowerInvariant()}, \"answer\": \"{answer}\", \"articleIds\": [{string.Join(",", ids.Select(i => $"\"{i}\""))}], \"confidence\": {confidence}}}";

    private static object Chat(params (string Role, string Content)[] messages) =>
        new { messages = messages.Select(m => new { role = m.Role, content = m.Content }) };

    private static async Task<ReplyBody> PostAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/portal/chatbot/messages", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReplyBody>())!;
    }

    [Fact]
    public async Task Status_ReportsWhetherAnAiKeyIsConfigured_WithoutSignIn()
    {
        var on = Create(new FakeAiTextService());
        var off = Create(new FakeAiTextService { IsConfigured = false });

        Assert.True((await on.Anonymous().GetFromJsonAsync<StatusBody>("/api/portal/chatbot/status"))!.Enabled);
        Assert.False((await off.Anonymous().GetFromJsonAsync<StatusBody>("/api/portal/chatbot/status"))!.Enabled);
    }

    [Fact]
    public async Task AVisitor_GetsAnAnswerFromAPublishedArticle_WithItsCitation_NotFromDrafts()
    {
        var fake = new FakeAiTextService();
        var portal = Create(fake);
        var admin = await portal.StaffAsync();
        var published = await AiArrange.ArticleAsync(admin, "Resetting a forgotten password", "Press the reset link on the sign in page.");
        await AiArrange.ArticleAsync(admin, "Forgotten password draft notes", "Secret draft about a forgotten password.", publish: false);
        fake.Answer = Json(true, "Press the reset link.", [published], "0.95");

        var reply = await PostAsync(portal.Anonymous(), Chat(("user", "I forgot my password")));

        Assert.Equal("answered", reply.Outcome);
        Assert.Equal("Press the reset link.", reply.Answer);
        Assert.Equal([published], reply.Sources.Select(s => s.Id));
        Assert.DoesNotContain("Secret draft", fake.Requests.Single().User);
    }

    [Fact]
    public async Task WhenItDoesNotKnow_ItSaysSo_AndOffersAnAgent()
    {
        var fake = new FakeAiTextService();
        var portal = Create(fake);

        var reply = await PostAsync(portal.Anonymous(), Chat(("user", "Do you sell zzqxywidgets?")));

        Assert.Equal(("unknown", true), (reply.Outcome, reply.OfferAgent));
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task AskingForAnAgent_AsASignedInCustomer_CreatesAPortalTicketWithTheTranscript()
    {
        var portal = Create(new FakeAiTextService());
        var (token, _) = await portal.SignInAsync();
        var customer = portal.Authenticated(token);

        var reply = await PostAsync(customer, Chat(("user", "My invoice is wrong"), ("assistant", "I could not find this."), ("user", "I want to talk to a human")));

        Assert.Equal("handoff", reply.Outcome);
        Assert.NotNull(reply.Ticket);
        var ticket = await customer.GetFromJsonAsync<PortalTicketBody>($"/api/portal/tickets/{reply.Ticket!.Id}");
        Assert.Equal("My invoice is wrong", ticket!.Subject);
        Assert.Contains("Customer: My invoice is wrong", ticket.Description);
        Assert.Contains("Chatbot: I could not find this.", ticket.Description);
        Assert.Contains("Customer: I want to talk to a human", ticket.Description);
    }

    [Fact]
    public async Task LowConfidence_HandsOver_ForASignedInCustomer()
    {
        var fake = new FakeAiTextService();
        var portal = Create(fake);
        var admin = await portal.StaffAsync();
        var article = await AiArrange.ArticleAsync(admin, "Warranty terms explained", "The warranty lasts one year.");
        fake.Answer = Json(true, "Maybe one year.", [article], "0.3");
        var (token, _) = await portal.SignInAsync();

        var reply = await PostAsync(portal.Authenticated(token), Chat(("user", "How long is the warranty?")));

        Assert.Equal("handoff", reply.Outcome);
        Assert.NotNull(reply.Ticket);
    }

    [Fact]
    public async Task AVisitor_AskingForAnAgent_IsToldToSignIn_AndNoTicketIsCreated()
    {
        var portal = Create(new FakeAiTextService());

        var reply = await PostAsync(portal.Anonymous(), Chat(("user", "I need a human agent")));

        Assert.Equal("handoff", reply.Outcome);
        Assert.True(reply.SignInRequired);
        Assert.Null(reply.Ticket);
    }

    [Fact]
    public async Task ArabicQuestions_GetArabicReplies()
    {
        var portal = Create(new FakeAiTextService());

        var reply = await PostAsync(portal.Anonymous(), Chat(("user", "هل تبيعون منتجات غير موجودة؟")));

        Assert.Equal("ar", reply.Language);
        Assert.Matches("[؀-ۿ]", reply.Answer);
    }

    [Fact]
    public async Task WithoutAKey_Is503_AndBadInputIs400()
    {
        var off = Create(new FakeAiTextService { IsConfigured = false });
        var on = Create(new FakeAiTextService());

        var unavailable = await off.Anonymous().PostAsJsonAsync("/api/portal/chatbot/messages", Chat(("user", "hello")));
        var empty = await on.Anonymous().PostAsJsonAsync("/api/portal/chatbot/messages", new { messages = Array.Empty<object>() });
        var assistantLast = await on.Anonymous().PostAsJsonAsync("/api/portal/chatbot/messages", Chat(("user", "hi"), ("assistant", "hello")));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, assistantLast.StatusCode);
        using var problem = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("messages", out _));
    }

    [Fact]
    public async Task AProviderFailure_Is502()
    {
        var fake = new FakeAiTextService();
        var portal = Create(fake);
        var admin = await portal.StaffAsync();
        await AiArrange.ArticleAsync(admin, "Shipping times overview", "Orders ship in two days.");
        fake.Fail = true;

        var response = await portal.Anonymous().PostAsJsonAsync("/api/portal/chatbot/messages", Chat(("user", "What are the shipping times?")));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
