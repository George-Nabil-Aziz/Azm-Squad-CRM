using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests.Tickets;

/// <summary>CRM-16: POST /api/tickets/{id}/assign.</summary>
public class TicketAssignmentTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record MeBody(Guid Id, string FullName);

    private static async Task<MeBody> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<MeBody>("/api/auth/me"))!;

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid ticketId, Guid? assigneeId) =>
        client.PostAsJsonAsync($"/api/tickets/{ticketId}/assign", new { assigneeId });

    private async Task<TicketBody> ArrangeTicketAsync(HttpClient creator) =>
        await TicketArrange.TicketAsync(creator, await TicketArrange.CustomerAsync(creator));

    private async Task<List<TicketHistoryEntry>> HistoryAsync(Guid ticketId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CrmDbContext>().TicketHistory
            .AsNoTracking().Where(h => h.TicketId == ticketId).OrderBy(h => h.Id).ToListAsync();
    }

    [Fact]
    public async Task Supervisor_Assigns_TheTicket_AndAHistoryEntryIsRecorded()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var agentMe = await MeAsync(agent);
        var supervisorMe = await MeAsync(supervisor);
        var ticket = await ArrangeTicketAsync(supervisor);

        var response = await AssignAsync(supervisor, ticket.Id, agentMe.Id);
        var updated = await response.Content.ReadFromJsonAsync<TicketBody>();
        var reloaded = await supervisor.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}");
        var history = await HistoryAsync(ticket.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((agentMe.Id, agentMe.FullName), (updated!.AssigneeId, updated.AssigneeName));
        Assert.Equal(agentMe.Id, reloaded!.AssigneeId);
        var entry = Assert.Single(history);
        Assert.Equal((TicketHistoryField.Assignee, (string?)null, (string?)agentMe.FullName, (Guid?)supervisorMe.Id),
            (entry.Field, entry.OldValue, entry.NewValue, entry.ChangedById));
        Assert.Equal(DateTimeKind.Utc, entry.ChangedAt.Kind);
    }

    [Fact]
    public async Task AgentWithoutAssignPermission_AssigningSomeoneElse_Returns403()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var colleague = await MeAsync(await factory.CreateClientWithRoleAsync(Roles.Agent));
        var ticket = await ArrangeTicketAsync(agent);

        var response = await AssignAsync(agent, ticket.Id, colleague.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null((await agent.GetFromJsonAsync<TicketBody>($"/api/tickets/{ticket.Id}"))!.AssigneeId);
        Assert.Empty(await HistoryAsync(ticket.Id));
    }

    [Fact]
    public async Task Agent_CanTakeATicketForThemselves()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var me = await MeAsync(agent);
        var ticket = await ArrangeTicketAsync(agent);

        var response = await AssignAsync(agent, ticket.Id, me.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(me.Id, (await response.Content.ReadFromJsonAsync<TicketBody>())!.AssigneeId);
    }

    [Fact]
    public async Task AssigningAnInactiveUser_Returns400_OnAssigneeId()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var inactiveId = await factory.CreateUserAsync($"gone-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword, Roles.Agent);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(inactiveId.ToString());
            user!.IsActive = false;
            await users.UpdateAsync(user);
        }

        var ticket = await ArrangeTicketAsync(supervisor);

        var response = await AssignAsync(supervisor, ticket.Id, inactiveId);
        var unknown = await AssignAsync(supervisor, ticket.Id, Guid.NewGuid());
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("assigneeId", problem!.Errors.Keys);
        Assert.Empty(await HistoryAsync(ticket.Id));
    }

    [Fact]
    public async Task TheAssignedAgent_SeesTheTicketInTheirList()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var me = await MeAsync(agent);
        var ticket = await ArrangeTicketAsync(supervisor);
        Assert.Equal(HttpStatusCode.OK, (await AssignAsync(supervisor, ticket.Id, me.Id)).StatusCode);

        var mine = await agent.GetFromJsonAsync<TicketListTests.TicketPageBody>($"/api/tickets?assigneeId={me.Id}");
        var unassigned = await agent.GetFromJsonAsync<TicketListTests.TicketPageBody>($"/api/tickets?unassigned=true&search={ticket.Number}");

        Assert.Contains(mine!.Items, t => t.Id == ticket.Id);
        Assert.DoesNotContain(unassigned!.Items, t => t.Id == ticket.Id);
    }

    [Fact]
    public async Task Supervisor_CanReassign_AndUnassign_EachRecordingHistory()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);
        var first = await MeAsync(await factory.CreateClientWithRoleAsync(Roles.Agent));
        var second = await MeAsync(await factory.CreateClientWithRoleAsync(Roles.Agent));
        var ticket = await ArrangeTicketAsync(supervisor);

        await AssignAsync(supervisor, ticket.Id, first.Id);
        await AssignAsync(supervisor, ticket.Id, first.Id); // same assignee: no new entry
        await AssignAsync(supervisor, ticket.Id, second.Id);
        var unassigned = await AssignAsync(supervisor, ticket.Id, null);
        var history = await HistoryAsync(ticket.Id);

        Assert.Null((await unassigned.Content.ReadFromJsonAsync<TicketBody>())!.AssigneeId);
        Assert.Equal(
            [((string?)null, (string?)first.FullName), (first.FullName, second.FullName), (second.FullName, null)],
            history.Select(h => (h.OldValue, h.NewValue)));
    }

    [Fact]
    public async Task UnknownTicket_Returns404()
    {
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);

        var response = await AssignAsync(supervisor, Guid.NewGuid(), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
