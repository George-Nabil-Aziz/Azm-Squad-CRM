using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>CRM-15: the ticket thread (GET / POST /api/tickets/{id}/messages) and FirstResponseAt.</summary>
public class TicketMessagesTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    public sealed record MessageBody(
        Guid Id, string Direction, bool IsInternal, string Body, string Channel, Guid? AuthorId, string? AuthorName,
        DateTime CreatedAt, string? DeliveryStatus);

    private async Task<(HttpClient Agent, Guid CustomerId, TicketBody Ticket)> ArrangeAsync()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent);
        return (agent, customerId, await TicketArrange.TicketAsync(agent, customerId));
    }

    private static async Task<MessageBody> PostAsync(HttpClient client, Guid ticketId, string body, bool? isInternal = null)
    {
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/messages", new { body, @internal = isInternal });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MessageBody>())!;
    }

    private static async Task<TicketBody> GetTicketAsync(HttpClient client, Guid ticketId) =>
        (await client.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticketId}"))!;

    [Fact]
    public async Task Reply_AppearsInTheThread_WithAuthorAndTime_Chronologically()
    {
        var (agent, _, ticket) = await ArrangeAsync();
        var posted = await PostAsync(agent, ticket.Id, "We are looking into it.");
        factory.Time.Advance(TimeSpan.FromMinutes(5));
        await PostAsync(agent, ticket.Id, "Fixed, please check.");

        var thread = await agent.GetFromJsonAsync<MessageBody[]>($"/api/tickets/{ticket.Id}/messages");

        Assert.Equal(["We are looking into it.", "Fixed, please check."], thread!.Select(m => m.Body));
        Assert.Equal(("outbound", false, "manual", null), (posted.Direction, posted.IsInternal, posted.Channel, posted.DeliveryStatus));
        Assert.NotNull(thread![0].AuthorId);
        Assert.False(string.IsNullOrWhiteSpace(thread[0].AuthorName));
        Assert.Equal(DateTimeKind.Utc, thread[0].CreatedAt.Kind);
        Assert.True(thread[0].CreatedAt < thread[1].CreatedAt);
    }

    [Fact]
    public async Task Note_IsFlaggedInternal_NotVisibleToTheCustomer_AndNotInTheCustomerTimeline()
    {
        var (agent, customerId, ticket) = await ArrangeAsync();
        await PostAsync(agent, ticket.Id, "Public answer.");
        var note = await PostAsync(agent, ticket.Id, "Customer is a VIP, be careful.", isInternal: true);

        var thread = await agent.GetFromJsonAsync<MessageBody[]>($"/api/tickets/{ticket.Id}/messages");
        using var scope = factory.Services.CreateScope();
        var customerView = await scope.ServiceProvider.GetRequiredService<ITicketMessageService>()
            .ListCustomerVisibleAsync(ticket.Id, CancellationToken.None);
        var timeline = await agent.GetFromJsonAsync<Customers.CustomerTimelineTests.TimelinePageBody>(
            $"/api/customers/{customerId}/timeline?type=message");

        Assert.Equal(("internal", true), (note.Direction, note.IsInternal));
        Assert.Equal(2, thread!.Length);
        Assert.Equal(["Public answer."], customerView.Select(m => m.Body));
        Assert.DoesNotContain(timeline!.Items, entry => entry.Details?.Contains("VIP") == true);
        Assert.Single(timeline.Items);
    }

    [Fact]
    public async Task FirstAgentReply_SetsFirstResponseAt_AndTheSecondDoesNotChangeIt()
    {
        var (agent, _, ticket) = await ArrangeAsync();
        Assert.Null((await GetTicketAsync(agent, ticket.Id)).FirstResponseAt);
        await PostAsync(agent, ticket.Id, "Private thoughts.", isInternal: true);
        Assert.Null((await GetTicketAsync(agent, ticket.Id)).FirstResponseAt);

        await PostAsync(agent, ticket.Id, "First answer.");
        var afterFirst = (await GetTicketAsync(agent, ticket.Id)).FirstResponseAt;
        factory.Time.Advance(TimeSpan.FromHours(1));
        await PostAsync(agent, ticket.Id, "Second answer.");
        var afterSecond = (await GetTicketAsync(agent, ticket.Id)).FirstResponseAt;

        Assert.NotNull(afterFirst);
        Assert.Equal(DateTimeKind.Utc, afterFirst.Value.Kind);
        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task Reply_ToAClosedTicket_Returns400_AndNothingIsSaved()
    {
        var (agent, _, ticket) = await ArrangeAsync();
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets.Where(t => t.Id == ticket.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.Status, TicketStatus.Closed));
        }

        var response = await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages", new { body = "Too late." });
        var note = await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages", new { body = "Too late.", @internal = true });
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, note.StatusCode);
        Assert.Contains("status", problem!.Errors.Keys);
        Assert.Empty((await agent.GetFromJsonAsync<MessageBody[]>($"/api/tickets/{ticket.Id}/messages"))!);
        Assert.Null((await GetTicketAsync(agent, ticket.Id)).FirstResponseAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reply_WithBlankBody_Returns400_OnBody(string body)
    {
        var (agent, _, ticket) = await ArrangeAsync();

        var response = await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/messages", new { body });
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("body", problem!.Errors.Keys);
    }

    [Fact]
    public async Task UnknownTicket_Returns404()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var post = await agent.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/messages", new { body = "Hi" });
        var get = await agent.GetAsync($"/api/tickets/{Guid.NewGuid()}/messages");

        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
