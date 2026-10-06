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

/// <summary>
/// CRM-14: GET /api/tickets. Every test tags its tickets' subjects and searches for the tag, so tickets of other tests
/// (shared database) never show up. Status, assignee and creation time are set directly in the database: no endpoint
/// changes them before CRM-16 / CRM-17.
/// </summary>
public class TicketListTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    public sealed record TicketPageBody(TicketBody[] Items, int Page, int PageSize, int TotalCount);

    public sealed record AssigneeBody(Guid Id, string FullName);

    private static string NewTag() => $"q{Guid.NewGuid():N}"[..12];

    private static async Task<TicketPageBody> ListAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/tickets?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketPageBody>())!;
    }

    /// <summary>Changes stored columns of a ticket the way later stories (assign, status) will.</summary>
    private async Task UpdateAsync(Guid ticketId, TicketStatus? status = null, Guid? assigneeId = null, DateTime? createdAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var tickets = scope.ServiceProvider.GetRequiredService<CrmDbContext>().Tickets.Where(t => t.Id == ticketId);
        if (status is { } s)
        {
            await tickets.ExecuteUpdateAsync(set => set.SetProperty(t => t.Status, s));
        }

        if (assigneeId is { } a)
        {
            await tickets.ExecuteUpdateAsync(set => set.SetProperty(t => t.AssigneeId, a));
        }

        if (createdAt is { } c)
        {
            await tickets.ExecuteUpdateAsync(set => set.SetProperty(t => t.CreatedAt, c));
        }
    }

    private async Task<(HttpClient Agent, Guid CustomerId)> ArrangeAsync()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        return (agent, await TicketArrange.CustomerAsync(agent));
    }

    [Fact]
    public async Task List_WithoutPaging_IsPagedNewestFirst_WithDefaults()
    {
        var (agent, customerId) = await ArrangeAsync();
        var tag = NewTag();
        var first = await TicketArrange.TicketAsync(agent, customerId, $"{tag} first");
        factory.Time.Advance(TimeSpan.FromSeconds(1));
        var second = await TicketArrange.TicketAsync(agent, customerId, $"{tag} second");

        var page = await ListAsync(agent, $"search={tag}");
        var everything = await ListAsync(agent, string.Empty);

        Assert.Equal([second.Id, first.Id], page.Items.Select(t => t.Id));
        Assert.Equal((1, 20, 2), (page.Page, page.PageSize, page.TotalCount));
        Assert.Equal(everything.Items.OrderByDescending(t => t.CreatedAt).Select(t => t.Id), everything.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task List_Paginates_WithTotalCountOfAllMatches()
    {
        var (agent, customerId) = await ArrangeAsync();
        var tag = NewTag();
        for (var n = 1; n <= 3; n++)
        {
            await TicketArrange.TicketAsync(agent, customerId, $"{tag} {n}");
        }

        var first = await ListAsync(agent, $"search={tag}&page=1&pageSize=2");
        var second = await ListAsync(agent, $"search={tag}&page=2&pageSize=2");

        Assert.Equal([$"{tag} 3", $"{tag} 2"], first.Items.Select(t => t.Subject)); // same time: higher number first
        Assert.Equal([$"{tag} 1"], second.Items.Select(t => t.Subject));
        Assert.All([first, second], page => Assert.Equal(3, page.TotalCount));
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var (agent, customerId) = await ArrangeAsync();
        var tag = NewTag();
        var open = await TicketArrange.TicketAsync(agent, customerId, $"{tag} open");
        await TicketArrange.TicketAsync(agent, customerId, $"{tag} new");
        await UpdateAsync(open.Id, status: TicketStatus.Open);

        var page = await ListAsync(agent, $"search={tag}&status=open");

        Assert.Equal([open.Id], page.Items.Select(t => t.Id));
        Assert.Equal("open", page.Items[0].Status);
    }

    [Fact]
    public async Task List_FiltersByPriority()
    {
        var (agent, customerId) = await ArrangeAsync();
        var tag = NewTag();
        var low = await TicketArrange.TicketAsync(agent, customerId, $"{tag} low", priority: "low");
        await TicketArrange.TicketAsync(agent, customerId, $"{tag} high", priority: "high");

        var page = await ListAsync(agent, $"search={tag}&priority=LOW");

        Assert.Equal([low.Id], page.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task List_FiltersByCategory()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await TicketArrange.CategoryAsync(admin);
        var customerId = await TicketArrange.CustomerAsync(admin);
        var tag = NewTag();
        var billing = await TicketArrange.TicketAsync(admin, customerId, $"{tag} billing", categoryId: category.Id);
        await TicketArrange.TicketAsync(admin, customerId, $"{tag} none");

        var page = await ListAsync(admin, $"search={tag}&categoryId={category.Id}");

        Assert.Equal([billing.Id], page.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task List_FiltersByAssignee_AndUnassigned()
    {
        var (agent, customerId) = await ArrangeAsync();
        var assigneeId = await factory.CreateUserAsync($"assignee-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword, Roles.Agent);
        var tag = NewTag();
        var assigned = await TicketArrange.TicketAsync(agent, customerId, $"{tag} assigned");
        var unassigned = await TicketArrange.TicketAsync(agent, customerId, $"{tag} unassigned");
        await UpdateAsync(assigned.Id, assigneeId: assigneeId);

        var byAssignee = await ListAsync(agent, $"search={tag}&assigneeId={assigneeId}");
        var withoutAssignee = await ListAsync(agent, $"search={tag}&unassigned=true");

        Assert.Equal([assigned.Id], byAssignee.Items.Select(t => t.Id));
        Assert.Equal(assigneeId, byAssignee.Items[0].AssigneeId);
        Assert.NotNull(byAssignee.Items[0].AssigneeName);
        Assert.Equal([unassigned.Id], withoutAssignee.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task List_FiltersByCreatedDateRange_Inclusive()
    {
        var (agent, customerId) = await ArrangeAsync();
        var tag = NewTag();
        var before = await TicketArrange.TicketAsync(agent, customerId, $"{tag} before");
        var firstDay = await TicketArrange.TicketAsync(agent, customerId, $"{tag} first day");
        var lastDay = await TicketArrange.TicketAsync(agent, customerId, $"{tag} last day");
        var after = await TicketArrange.TicketAsync(agent, customerId, $"{tag} after");
        await UpdateAsync(before.Id, createdAt: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc));
        await UpdateAsync(firstDay.Id, createdAt: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        await UpdateAsync(lastDay.Id, createdAt: new DateTime(2026, 10, 3, 23, 59, 0, DateTimeKind.Utc));
        await UpdateAsync(after.Id, createdAt: new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc));

        var range = await ListAsync(agent, $"search={tag}&createdFrom=2026-10-01&createdTo=2026-10-03");
        var fromOnly = await ListAsync(agent, $"search={tag}&createdFrom=2026-10-04");
        var toOnly = await ListAsync(agent, $"search={tag}&createdTo=2026-09-30");

        Assert.Equal([lastDay.Id, firstDay.Id], range.Items.Select(t => t.Id));
        Assert.Equal([after.Id], fromOnly.Items.Select(t => t.Id));
        Assert.Equal([before.Id], toOnly.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task List_SearchByTicketNumber_InAnyForm()
    {
        var (agent, customerId) = await ArrangeAsync();
        var ticket = await TicketArrange.TicketAsync(agent, customerId, $"{NewTag()} numbered");
        var number = int.Parse(ticket.Number["TKT-".Length..]);

        foreach (var search in new[] { ticket.Number, ticket.Number.ToLowerInvariant(), $"tkt-{number}" })
        {
            var page = await ListAsync(agent, $"search={Uri.EscapeDataString(search)}");
            Assert.Equal([ticket.Id], page.Items.Select(t => t.Id));
        }

        var plain = await ListAsync(agent, $"search={number}");
        Assert.Contains(plain.Items, t => t.Id == ticket.Id);
    }

    [Fact]
    public async Task List_SearchBySubject_CaseInsensitive_WildcardsLiteral()
    {
        var (agent, customerId) = await ArrangeAsync();
        var tag = NewTag();
        var printer = await TicketArrange.TicketAsync(agent, customerId, $"{tag} Printer is offline");
        await TicketArrange.TicketAsync(agent, customerId, $"{tag} 50% discount missing");

        var byWord = await ListAsync(agent, $"search={tag.ToUpperInvariant()} PRINTER");
        var wildcard = await ListAsync(agent, $"search={tag}%25");

        Assert.Equal([printer.Id], byWord.Items.Select(t => t.Id));
        Assert.Empty(wildcard.Items);
    }

    [Fact]
    public async Task List_CombinedFilters_ReturnOnlyTicketsMatchingAll()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var category = await TicketArrange.CategoryAsync(admin);
        var customerId = await TicketArrange.CustomerAsync(admin);
        var tag = NewTag();
        var match = await TicketArrange.TicketAsync(admin, customerId, $"{tag} match", category.Id, "high");
        var wrongStatus = await TicketArrange.TicketAsync(admin, customerId, $"{tag} wrong status", category.Id, "high");
        await TicketArrange.TicketAsync(admin, customerId, $"{tag} wrong priority", category.Id, "low");
        await TicketArrange.TicketAsync(admin, customerId, $"{tag} wrong category", null, "high");
        await UpdateAsync(match.Id, status: TicketStatus.Pending);
        await UpdateAsync(wrongStatus.Id, status: TicketStatus.Open);
        var today = DateOnly.FromDateTime(factory.Time.GetUtcNow().UtcDateTime);

        var page = await ListAsync(admin,
            $"search={tag}&status=pending&priority=high&categoryId={category.Id}&unassigned=true&createdFrom={today:yyyy-MM-dd}&createdTo={today:yyyy-MM-dd}");

        Assert.Equal([match.Id], page.Items.Select(t => t.Id));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task List_ShowsTicketsOfASoftDeletedCustomer()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var customerId = await TicketArrange.CustomerAsync(agent, "Closed Shop");
        var tag = NewTag();
        var ticket = await TicketArrange.TicketAsync(agent, customerId, $"{tag} old request");
        await agent.DeleteAsync($"/api/customers/{customerId}");

        var page = await ListAsync(agent, $"search={tag}");

        var listed = Assert.Single(page.Items);
        Assert.Equal((ticket.Id, "Closed Shop"), (listed.Id, listed.CustomerName));
    }

    [Theory]
    [InlineData("status=done", "status")]
    [InlineData("priority=urgent", "priority")]
    [InlineData("createdFrom=2026-10-05&createdTo=2026-10-01", "createdTo")]
    [InlineData("unassigned=true&assigneeId=6f9619ff-8b86-d011-b42d-00cf4fc964ff", "assigneeId")]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=101", "pageSize")]
    public async Task List_WithInvalidFilters_Returns400(string query, string field)
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var response = await agent.GetAsync($"/api/tickets?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal([field], problem!.Errors.Keys);
    }

    [Fact]
    public async Task Assignees_ListsActiveStaffUsers_ByName_ForAnAgent()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);
        var staffId = await factory.CreateUserAsync($"staff-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword, Roles.Supervisor);
        var noRoleId = await factory.CreateUserAsync($"norole-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword);
        var inactiveId = await factory.CreateUserAsync($"gone-{Guid.NewGuid():N}@crm.local", CrmApiFactory.TestUserPassword, Roles.Agent);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var inactive = await users.FindByIdAsync(inactiveId.ToString());
            inactive!.IsActive = false;
            await users.UpdateAsync(inactive);
        }

        var assignees = await agent.GetFromJsonAsync<AssigneeBody[]>("/api/tickets/assignees");

        Assert.Contains(assignees!, a => a.Id == staffId);
        Assert.DoesNotContain(assignees!, a => a.Id == noRoleId || a.Id == inactiveId);
        Assert.Equal(assignees!.Select(a => a.FullName).Order(StringComparer.Ordinal), assignees!.Select(a => a.FullName));
    }
}
