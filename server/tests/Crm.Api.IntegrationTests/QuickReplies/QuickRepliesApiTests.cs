using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.QuickReplies;

/// <summary>CRM-32: quick replies through the real API and database.</summary>
public class QuickRepliesApiTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record ReplyBody(Guid Id, string Title, string? Shortcut, string Body, bool IsShared, bool IsMine);

    private sealed record RenderedBody(string Text);

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static async Task<ReplyBody> CreateAsync(HttpClient client, string title, string body = "Hi", bool shared = false, string? shortcut = null)
    {
        var response = await client.PostAsJsonAsync("/api/quick-replies", new { title, shortcut, body, isShared = shared });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReplyBody>())!;
    }

    [Fact]
    public async Task PersonalReplies_AreOnlyVisibleToTheirOwner()
    {
        var mine = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var other = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var title = Unique("Mine");
        var reply = await CreateAsync(mine, title);

        Assert.Contains((await mine.GetFromJsonAsync<ReplyBody[]>("/api/quick-replies"))!, r => r.Id == reply.Id && r.IsMine);
        Assert.DoesNotContain((await other.GetFromJsonAsync<ReplyBody[]>("/api/quick-replies"))!, r => r.Id == reply.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/quick-replies/{reply.Id}")).StatusCode);
    }

    [Fact]
    public async Task SharedReplies_AreVisibleToEveryone_ButOnlyManagedWithPermission()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var forbiddenCreate = await agent.PostAsJsonAsync("/api/quick-replies", new { title = Unique("Nope"), body = "x", isShared = true });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        var shared = await CreateAsync(supervisor, Unique("Shared"), shared: true);
        Assert.Contains((await agent.GetFromJsonAsync<ReplyBody[]>("/api/quick-replies"))!, r => r.Id == shared.Id && !r.IsMine);

        var edit = await agent.PutAsJsonAsync($"/api/quick-replies/{shared.Id}", new { title = "Hacked", body = "x", isShared = true });
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode); // AC 3
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.DeleteAsync($"/api/quick-replies/{shared.Id}")).StatusCode);

        var ok = await supervisor.PutAsJsonAsync($"/api/quick-replies/{shared.Id}", new { title = "Renamed", body = "y", isShared = true });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await supervisor.DeleteAsync($"/api/quick-replies/{shared.Id}")).StatusCode);
    }

    [Fact]
    public async Task Search_FindsByTitleOrShortcut()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var key = Guid.NewGuid().ToString("N")[..8];
        var byTitle = await CreateAsync(agent, $"Refund policy {key}");
        var byShortcut = await CreateAsync(agent, Unique("Other title"), shortcut: $"/zz{key}");
        await CreateAsync(agent, Unique("Unrelated"));

        var titleHits = (await agent.GetFromJsonAsync<ReplyBody[]>($"/api/quick-replies?search=refund%20policy%20{key}"))!;
        var shortcutHits = (await agent.GetFromJsonAsync<ReplyBody[]>($"/api/quick-replies?search=/zz{key}"))!;

        Assert.Equal([byTitle.Id], titleHits.Select(r => r.Id)); // AC 4
        Assert.Equal([byShortcut.Id], shortcutHits.Select(r => r.Id));
    }

    [Fact]
    public async Task Render_ReplacesThePlaceholdersWithTheTicketData()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent, "Nour Trading");
        var ticket = await TicketArrange.TicketAsync(agent, customerId, "Printer is down");
        var reply = await CreateAsync(agent, Unique("Greeting"), "Hello {{customer.name}}, about {{ticket.number}}: {{ticket.subject}}.");

        var response = await agent.PostAsJsonAsync($"/api/quick-replies/{reply.Id}/render", new { ticketId = ticket.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"Hello Nour Trading, about {ticket.Number}: Printer is down.", (await response.Content.ReadFromJsonAsync<RenderedBody>())!.Text); // AC 2
        Assert.Equal(HttpStatusCode.NotFound,
            (await agent.PostAsJsonAsync($"/api/quick-replies/{reply.Id}/render", new { ticketId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task Create_WithoutTitleOrBody_Returns400_AndAnonymousIs401()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostAsJsonAsync("/api/quick-replies", new { title = "", body = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/quick-replies")).StatusCode);
    }
}
