using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.IntegrationTests.Customers;

/// <summary>CRM-11 AC 1: notes about a customer, with author and time.</summary>
public class CustomerNotesTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    public sealed record NoteBody(Guid Id, string Text, Guid? AuthorId, string? AuthorName, DateTime CreatedAt);

    public sealed record NotePageBody(NoteBody[] Items, int Page, int PageSize, int TotalCount);

    private async Task<(HttpClient Client, Guid UserId, string Email)> AgentAsync()
    {
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        var userId = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        return (factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword)), userId, email);
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name = "Nour Trading" });
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!.Id;
    }

    [Fact]
    public async Task AddNote_ShowsItWithAuthorAndTime()
    {
        var (agent, userId, email) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);
        factory.Time.Advance(TimeSpan.FromMinutes(1));

        var response = await agent.PostAsJsonAsync($"/api/customers/{customerId}/notes", new { text = "  Prefers WhatsApp.  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = (await response.Content.ReadFromJsonAsync<NoteBody>())!;
        Assert.Equal($"/api/customers/{customerId}/notes/{note.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(("Prefers WhatsApp.", (Guid?)userId, email), (note.Text, note.AuthorId, note.AuthorName));
        Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, note.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, note.CreatedAt.Kind);

        var list = await agent.GetFromJsonAsync<NotePageBody>($"/api/customers/{customerId}/notes");
        Assert.Equal(note, Assert.Single(list!.Items));

        var timeline = await agent.GetFromJsonAsync<CustomerTimelineTests.TimelinePageBody>(
            $"/api/customers/{customerId}/timeline?type=note");
        var entry = Assert.Single(timeline!.Items);
        Assert.Equal(("noteAdded", "Prefers WhatsApp.", (Guid?)note.Id, email), (entry.Event, entry.Details, entry.SourceId, entry.ActorName));
    }

    [Fact]
    public async Task Notes_AreListedNewestFirst_AndPaginated()
    {
        var (agent, _, _) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);
        for (var i = 1; i <= 3; i++)
        {
            factory.Time.Advance(TimeSpan.FromMinutes(1));
            await agent.PostAsJsonAsync($"/api/customers/{customerId}/notes", new { text = $"Note {i}" });
        }

        var first = await agent.GetFromJsonAsync<NotePageBody>($"/api/customers/{customerId}/notes?pageSize=2");
        var second = await agent.GetFromJsonAsync<NotePageBody>($"/api/customers/{customerId}/notes?page=2&pageSize=2");

        Assert.Equal(["Note 3", "Note 2"], first!.Items.Select(n => n.Text));
        Assert.Equal(["Note 1"], second!.Items.Select(n => n.Text));
        Assert.Equal(3, first.TotalCount);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("long")]
    public async Task AddNote_WithEmptyOrTooLongText_Returns400(string? text)
    {
        var (agent, _, _) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);

        var response = await agent.PostAsJsonAsync($"/api/customers/{customerId}/notes",
            new { text = text == "long" ? new string('x', 4001) : text });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["text"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task Notes_OfUnknownOrDeletedCustomer_Return404()
    {
        var (agent, _, _) = await AgentAsync();
        var customerId = await CreateCustomerAsync(agent);
        await agent.DeleteAsync($"/api/customers/{customerId}");

        foreach (var id in new[] { customerId, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/customers/{id}/notes")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await agent.PostAsJsonAsync($"/api/customers/{id}/notes", new { text = "Note" })).StatusCode);
        }
    }
}
