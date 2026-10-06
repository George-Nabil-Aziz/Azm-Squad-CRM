using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>CRM-30: the customer panel of a ticket against the real database.</summary>
public class TicketCustomerContextTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record ContactBody(Guid Id, string Type, string Value, bool IsPrimary);

    private sealed record CustomerBody(Guid Id, string Name, string? Email, string? Phone, ContactBody[] Contacts);

    private sealed record SummaryBody(Guid Id, string Number, string Subject, string Status, string Priority, DateTime CreatedAt, bool IsCurrent);

    private sealed record ContextBody(CustomerBody Customer, bool CustomerDeleted, int TotalTickets, SummaryBody[] RecentTickets);

    private static async Task<Guid> CustomerAsync(HttpClient client, string name, string email, string phone)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name, email, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerBody>())!.Id;
    }

    [Fact]
    public async Task ShowsTheCustomerTheTotalAndTheFiveNewestTickets_OfThisCustomerOnly()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await CustomerAsync(agent, "Nour Trading", $"nour-{Guid.NewGuid():N}@example.com", "+966501234567");
        var other = await CustomerAsync(agent, "Someone Else", $"else-{Guid.NewGuid():N}@example.com", "+966507654321");
        var tickets = new List<TicketBody>();
        for (var i = 1; i <= 7; i++)
        {
            tickets.Add(await TicketArrange.TicketAsync(agent, customerId, $"Ticket {i}"));
            factory.Time.Advance(TimeSpan.FromMinutes(1));
        }

        await TicketArrange.TicketAsync(agent, other, "Not mine");
        await agent.PutAsJsonAsync($"/api/tickets/{tickets[5].Id}/status", new { status = "open" });

        var context = (await agent.GetFromJsonAsync<ContextBody>($"/api/tickets/{tickets[2].Id}/customer-context"))!;

        Assert.Equal("Nour Trading", context.Customer.Name);
        Assert.Equal(2, context.Customer.Contacts.Length); // AC 1: contact details
        Assert.Equal(7, context.TotalTickets);
        Assert.Equal(["Ticket 7", "Ticket 6", "Ticket 5", "Ticket 4", "Ticket 3"], context.RecentTickets.Select(t => t.Subject)); // AC 2
        Assert.Equal("open", context.RecentTickets[1].Status);
        Assert.Equal("new", context.RecentTickets[0].Status);
        Assert.Equal(tickets[2].Id, Assert.Single(context.RecentTickets, t => t.IsCurrent).Id);
        Assert.False(context.CustomerDeleted);
    }

    [Fact]
    public async Task ADeletedCustomer_IsStillShown_Flagged()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await CustomerAsync(agent, "Gone Soon", $"gone-{Guid.NewGuid():N}@example.com", "+966500000001");
        var ticket = await TicketArrange.TicketAsync(agent, customerId);
        Assert.Equal(HttpStatusCode.NoContent, (await agent.DeleteAsync($"/api/customers/{customerId}")).StatusCode);

        var context = (await agent.GetFromJsonAsync<ContextBody>($"/api/tickets/{ticket.Id}/customer-context"))!;

        Assert.True(context.CustomerDeleted);
        Assert.Equal("Gone Soon", context.Customer.Name);
    }

    [Fact]
    public async Task UnknownTicket_Is404_AndAnonymousIs401()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/tickets/{Guid.NewGuid()}/customer-context")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/tickets/{Guid.NewGuid()}/customer-context")).StatusCode);
    }
}
