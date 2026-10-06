using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>CRM-13: creating tickets (POST /api/tickets) and reading one (GET /api/tickets/{id}).</summary>
public class TicketCreationTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    public sealed record TimelineEntryBody(string Type, string Event, string? Details, Guid? SourceId);

    public sealed record TimelinePageBody(TimelineEntryBody[] Items, int TotalCount);

    [Fact]
    public async Task CreateTicket_WithAllFields_Returns201_WithNumberAndStatusNew()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var category = await TicketArrange.CategoryAsync(admin);
        var customerId = await TicketArrange.CustomerAsync(agent);

        var response = await agent.PostAsJsonAsync("/api/tickets", new
        {
            customerId, subject = "Invoice is wrong", description = "Line 3 is charged twice.", categoryId = category.Id,
            priority = "high",
        });
        var ticket = await response.Content.ReadFromJsonAsync<TicketBody>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/tickets/{ticket!.Id}", response.Headers.Location?.OriginalString);
        Assert.Matches(@"^TKT-\d{6}$", ticket.Number);
        Assert.Equal(("new", "high", "manual"), (ticket.Status, ticket.Priority, ticket.Channel));
        Assert.Equal(("Invoice is wrong", "Line 3 is charged twice."), (ticket.Subject, ticket.Description));
        Assert.Equal((customerId, "Nour Trading"), (ticket.CustomerId, ticket.CustomerName));
        Assert.Equal((category.Id, category.Name), (ticket.CategoryId!.Value, ticket.CategoryName));
        Assert.Null(ticket.AssigneeId);
    }

    [Fact]
    public async Task CreateTicket_StoresCreatedAtInUtc()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        factory.Time.Advance(TimeSpan.FromMinutes(3));
        var now = factory.Time.GetUtcNow().UtcDateTime;

        var ticket = await TicketArrange.TicketAsync(agent, customerId);

        Assert.Equal(now, ticket.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, ticket.CreatedAt.Kind);
        using var scope = factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(now, row.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, row.CreatedAt.Kind);
    }

    [Fact]
    public async Task CreateTicket_WithoutCustomerOrSubject_Returns400()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var response = await agent.PostAsJsonAsync("/api/tickets", new { subject = " ", priority = "low" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["customerId", "subject"], problem!.Errors.Keys.Order());
    }

    [Fact]
    public async Task CreateTicket_ForADeletedCustomer_Returns400()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        await agent.DeleteAsync($"/api/customers/{customerId}");

        var response = await agent.PostAsJsonAsync("/api/tickets", new { customerId, subject = "Hello" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["customerId"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateTicket_WithAnInactiveCategory_Returns400()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await TicketArrange.CategoryAsync(admin, "Legacy");
        await admin.PutAsJsonAsync($"/api/ticket-categories/{category.Id}", new { name = category.Name, isActive = false });
        var customerId = await TicketArrange.CustomerAsync(admin);

        var response = await admin.PostAsJsonAsync("/api/tickets", new { customerId, subject = "Hello", categoryId = category.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["categoryId"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateTicket_WithAnUnknownPriority_Returns400()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);

        var response = await agent.PostAsJsonAsync("/api/tickets", new { customerId, subject = "Hello", priority = "urgent" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["priority"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateTicket_WithoutPriority_IsMid()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);

        var ticket = await TicketArrange.TicketAsync(agent, customerId, priority: null);

        Assert.Equal("mid", ticket.Priority);
    }

    [Fact]
    public async Task TicketNumbers_AreUniqueAndSequential()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);

        var numbers = new List<int>();
        for (var n = 0; n < 3; n++)
        {
            numbers.Add(int.Parse((await TicketArrange.TicketAsync(agent, customerId, $"Ticket {n}")).Number["TKT-".Length..]));
        }

        Assert.Equal([numbers[0], numbers[0] + 1, numbers[0] + 2], numbers);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        Assert.Equal(await db.Tickets.CountAsync(), await db.Tickets.Select(t => t.Number).Distinct().CountAsync());
    }

    [Fact]
    public async Task CreateTicket_AddsATimelineEntry()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);

        var ticket = await TicketArrange.TicketAsync(agent, customerId, "Cannot log in");
        var timeline = await agent.GetFromJsonAsync<TimelinePageBody>($"/api/customers/{customerId}/timeline?type=ticket");

        var entry = Assert.Single(timeline!.Items);
        Assert.Equal(("ticket", "ticketCreated", $"{ticket.Number} Cannot log in", (Guid?)ticket.Id),
            (entry.Type, entry.Event, entry.Details, entry.SourceId));
    }

    [Fact]
    public async Task GetTicket_OfASoftDeletedCustomer_StillShowsTheCustomer()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent, "Gone Trading");
        var ticket = await TicketArrange.TicketAsync(agent, customerId);
        Assert.Equal(HttpStatusCode.NoContent, (await agent.DeleteAsync($"/api/customers/{customerId}")).StatusCode);

        var found = await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}");

        Assert.Equal((customerId, "Gone Trading"), (found!.CustomerId, found.CustomerName));
    }

    [Fact]
    public async Task GetTicket_KeepsADeactivatedCategoryName()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await TicketArrange.CategoryAsync(admin, "Retired");
        var customerId = await TicketArrange.CustomerAsync(admin);
        var ticket = await TicketArrange.TicketAsync(admin, customerId, categoryId: category.Id);
        await admin.PutAsJsonAsync($"/api/ticket-categories/{category.Id}", new { name = category.Name, isActive = false });

        var found = await admin.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}");

        Assert.Equal((category.Id, category.Name), (found!.CategoryId!.Value, found.CategoryName));
    }

    [Fact]
    public async Task GetTicket_Unknown_Returns404()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var response = await agent.GetAsync($"/api/tickets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
