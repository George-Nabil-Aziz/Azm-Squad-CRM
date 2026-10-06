using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Application.Customers.Timeline;
using Crm.Domain.Customers;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Customers;

/// <summary>CRM-10: the customer's interaction timeline (newest first, filter by type, paging).</summary>
public class CustomerTimelineTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    public sealed record EntryBody(
        long Id, string Type, string Event, string? Details, Guid? SourceId, Guid? ActorId, string? ActorName, DateTime OccurredAt);

    public sealed record TimelinePageBody(EntryBody[] Items, int Page, int PageSize, int TotalCount);

    private async Task<(HttpClient Client, string Email)> AgentAsync()
    {
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        return (factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword)), email);
    }

    private static async Task<CustomerBody> CreateCustomerAsync(HttpClient client, string name = "Nour Trading")
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!;
    }

    private static async Task<TimelinePageBody> TimelineAsync(HttpClient client, Guid customerId, string query = "")
    {
        var response = await client.GetAsync($"/api/customers/{customerId}/timeline{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TimelinePageBody>())!;
    }

    /// <summary>Writes entries the way later features will (tickets, messages, notes): through the recorder.</summary>
    private async Task RecordAsync(Guid customerId, params (InteractionType Type, string Event, string Details)[] entries)
    {
        using var scope = factory.Services.CreateScope();
        var recorder = scope.ServiceProvider.GetRequiredService<IInteractionRecorder>();
        foreach (var (type, @event, details) in entries)
        {
            recorder.Record(customerId, type, @event, details, Guid.NewGuid(), factory.Time.GetUtcNow().UtcDateTime);
        }

        await scope.ServiceProvider.GetRequiredService<CrmDbContext>().SaveChangesAsync();
    }

    [Fact]
    public async Task Timeline_ShowsWhatHappened_NewestFirst_WithActorAndTime()
    {
        var (agent, email) = await AgentAsync();
        var customer = await CreateCustomerAsync(agent);
        var created = factory.Time.GetUtcNow().UtcDateTime;
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        var phone = TestPhones.NewMobile();
        await agent.PostAsJsonAsync($"/api/customers/{customer.Id}/contacts", new { type = "phone", value = phone });
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await agent.PutAsJsonAsync($"/api/customers/{customer.Id}", new { name = "Nour Trading Co", phone });

        var timeline = await TimelineAsync(agent, customer.Id);

        Assert.Equal(
            [("customerUpdated", "Nour Trading Co"), ("contactAdded", phone), ("customerCreated", "Nour Trading")],
            timeline.Items.Select(e => (e.Event, e.Details)));
        Assert.All(timeline.Items, e => Assert.Equal(("customer", email), (e.Type, e.ActorName)));
        Assert.Equal(created, timeline.Items[^1].OccurredAt);
        Assert.Equal(DateTimeKind.Utc, timeline.Items[^1].OccurredAt.Kind);
        Assert.Equal(created.AddMinutes(2), timeline.Items[0].OccurredAt);
        Assert.Equal(3, timeline.TotalCount);
    }

    [Theory]
    [InlineData("note", "noteAdded")]
    [InlineData("ticket", "ticketCreated")]
    [InlineData("message", "messageReceived")]
    public async Task Timeline_FilteredByType_ReturnsOnlyThatType(string type, string @event)
    {
        var (agent, _) = await AgentAsync();
        var customer = await CreateCustomerAsync(agent);
        await RecordAsync(customer.Id,
            (InteractionType.Note, "noteAdded", "Prefers WhatsApp"),
            (InteractionType.Ticket, "ticketCreated", "T-1 Printer broken"),
            (InteractionType.Message, "messageReceived", "Hello"));

        var timeline = await TimelineAsync(agent, customer.Id, $"?type={type}");

        var entry = Assert.Single(timeline.Items);
        Assert.Equal((type, @event), (entry.Type, entry.Event));
        Assert.Null(entry.ActorName); // written outside a request: a system entry
        Assert.Equal(1, timeline.TotalCount);
    }

    [Fact]
    public async Task Timeline_IsPaginated()
    {
        var (agent, _) = await AgentAsync();
        var customer = await CreateCustomerAsync(agent); // entry 1: customerCreated
        await RecordAsync(customer.Id, [.. Enumerable.Range(1, 24)
            .Select(i => (InteractionType.Note, "noteAdded", $"Note {i}"))]);

        var first = await TimelineAsync(agent, customer.Id, "?pageSize=10");
        var second = await TimelineAsync(agent, customer.Id, "?page=2&pageSize=10");
        var third = await TimelineAsync(agent, customer.Id, "?page=3&pageSize=10");

        Assert.Equal([10, 10, 5], new[] { first.Items.Length, second.Items.Length, third.Items.Length });
        Assert.All(new[] { first, second, third }, page => Assert.Equal(25, page.TotalCount));
        Assert.Equal("Note 24", first.Items[0].Details); // same time: the last written comes first
        Assert.Equal("customerCreated", third.Items[^1].Event);
        Assert.Equal(25, first.Items.Concat(second.Items).Concat(third.Items).Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public async Task RecordedTicketEntry_ShowsInTheTimeline()
    {
        // CRM-10 AC 2 mechanism: CRM-13 records "ticketCreated" through IInteractionRecorder when a ticket is created.
        var (agent, _) = await AgentAsync();
        var customer = await CreateCustomerAsync(agent);
        factory.Time.Advance(TimeSpan.FromMinutes(1));
        await RecordAsync(customer.Id, (InteractionType.Ticket, "ticketCreated", "T-7 Cannot log in"));

        var timeline = await TimelineAsync(agent, customer.Id);

        Assert.Equal(("ticket", "ticketCreated", "T-7 Cannot log in"),
            (timeline.Items[0].Type, timeline.Items[0].Event, timeline.Items[0].Details));
        Assert.NotNull(timeline.Items[0].SourceId);
    }

    [Theory]
    [InlineData("?type=foo")]
    [InlineData("?type=Note")]
    [InlineData("?type=1")]
    public async Task Timeline_WithUnknownType_Returns400(string query)
    {
        var (agent, _) = await AgentAsync();
        var customer = await CreateCustomerAsync(agent);

        var response = await agent.GetAsync($"/api/customers/{customer.Id}/timeline{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["type"], problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    public async Task Timeline_WithInvalidPaging_Returns400(string query)
    {
        var (agent, _) = await AgentAsync();
        var customer = await CreateCustomerAsync(agent);

        var response = await agent.GetAsync($"/api/customers/{customer.Id}/timeline{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Timeline_OfUnknownOrDeletedCustomer_Returns404()
    {
        var (agent, _) = await AgentAsync();
        var customer = await CreateCustomerAsync(agent);
        await agent.DeleteAsync($"/api/customers/{customer.Id}");

        var deleted = await agent.GetAsync($"/api/customers/{customer.Id}/timeline");
        var unknown = await agent.GetAsync($"/api/customers/{Guid.NewGuid()}/timeline");

        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
