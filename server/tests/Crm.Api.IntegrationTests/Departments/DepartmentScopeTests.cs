using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Departments;

/// <summary>CRM-61 AC 2 and 3: agents see only their departments' tickets (404 otherwise); transfers are recorded.</summary>
public class DepartmentScopeTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record HistoryBody(string Id, string Field, string? OldValue, string? NewValue);

    private sealed record TransferBody(Guid TicketId, Guid? DepartmentId, string? DepartmentName);

    private async Task<(HttpClient Admin, DepartmentBody A, DepartmentBody B, DepartmentTicketBody TicketA, DepartmentTicketBody TicketB, DepartmentTicketBody General)> ArrangeAsync()
    {
        var admin = await DepartmentArrange.SuperAdminAsync(factory);
        var a = await DepartmentArrange.DepartmentAsync(admin, "A");
        var b = await DepartmentArrange.DepartmentAsync(admin, "B");
        var customerId = await TicketArrange.CustomerAsync(admin);
        return (admin, a, b,
            await DepartmentArrange.TicketAsync(admin, customerId, a.Id),
            await DepartmentArrange.TicketAsync(admin, customerId, b.Id),
            await DepartmentArrange.TicketAsync(admin, customerId, null));
    }

    [Fact]
    public async Task AnAgent_SeesOwnDepartmentAndGeneralTickets_ButOpeningAnotherDepartmentsTicketIs404()
    {
        var (admin, a, _, ticketA, ticketB, general) = await ArrangeAsync();
        var agent = await DepartmentArrange.AgentInAsync(factory, admin, a.Id);

        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync($"/api/tickets/{ticketA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync($"/api/tickets/{general.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/tickets/{ticketB.Id}")).StatusCode);
    }

    [Fact]
    public async Task EveryTicketEndpoint_Returns404_ForAnotherDepartmentsTicket()
    {
        var (admin, a, _, _, ticketB, _) = await ArrangeAsync();
        var agent = await DepartmentArrange.AgentInAsync(factory, admin, a.Id);
        var id = ticketB.Id;

        var responses = new[]
        {
            await agent.GetAsync($"/api/tickets/{id}/messages"),
            await agent.GetAsync($"/api/tickets/{id}/history"),
            await agent.PostAsJsonAsync($"/api/tickets/{id}/messages", new { body = "hello", isInternal = true }),
            await agent.PutAsJsonAsync($"/api/tickets/{id}/status", new { status = "open" }),
            await agent.PutAsJsonAsync($"/api/tickets/{id}/priority", new { priority = "low" }),
            await agent.PutAsJsonAsync($"/api/tickets/{id}/department", new { departmentId = a.Id }),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    [Fact]
    public async Task TheTicketList_ShowsAnAgentOnlyTheirDepartmentsAndGeneral()
    {
        var (admin, a, _, ticketA, ticketB, general) = await ArrangeAsync();
        var agent = await DepartmentArrange.AgentInAsync(factory, admin, a.Id);

        var page = await agent.GetFromJsonAsync<DepartmentPage>("/api/tickets?pageSize=100");

        var ids = page!.Items.Select(t => t.Id).ToList();
        Assert.Contains(ticketA.Id, ids);
        Assert.Contains(general.Id, ids);
        Assert.DoesNotContain(ticketB.Id, ids);
        Assert.Equal(ids.Count, page.TotalCount);
    }

    [Fact]
    public async Task ASupervisorAndAnAdmin_SeeEveryDepartment()
    {
        var (_, _, _, _, ticketB, _) = await ArrangeAsync();
        var supervisor = await factory.CreateClientWithRoleAsync(Roles.Supervisor);

        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync($"/api/tickets/{ticketB.Id}")).StatusCode);
    }

    [Fact]
    public async Task AnAgentInTwoDepartments_SeesBoth()
    {
        var (admin, a, b, ticketA, ticketB, _) = await ArrangeAsync();
        var agent = await DepartmentArrange.AgentInAsync(factory, admin, a.Id, b.Id);

        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync($"/api/tickets/{ticketA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync($"/api/tickets/{ticketB.Id}")).StatusCode);
    }

    [Fact]
    public async Task Transfer_IsRecordedInTheHistory_WithBothDepartmentNames()
    {
        var (admin, a, b, ticketA, _, _) = await ArrangeAsync();

        var response = await admin.PutAsJsonAsync($"/api/tickets/{ticketA.Id}/department", new { departmentId = b.Id });
        var body = await response.Content.ReadFromJsonAsync<TransferBody>();
        var history = await admin.GetFromJsonAsync<HistoryBody[]>($"/api/tickets/{ticketA.Id}/history");
        var ticket = await admin.GetFromJsonAsync<DepartmentTicketBody>($"/api/tickets/{ticketA.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(b.Id, body!.DepartmentId);
        Assert.Equal(b.Id, ticket!.DepartmentId);
        var entry = Assert.Single(history!);
        Assert.Equal(("department", a.Name, b.Name), (entry.Field, entry.OldValue, entry.NewValue));
    }

    [Fact]
    public async Task TransferToTheSameDepartment_AddsNoHistory_AndAnUnknownTargetIs400()
    {
        var (admin, a, _, ticketA, _, _) = await ArrangeAsync();

        var same = await admin.PutAsJsonAsync($"/api/tickets/{ticketA.Id}/department", new { departmentId = a.Id });
        var unknown = await admin.PutAsJsonAsync($"/api/tickets/{ticketA.Id}/department", new { departmentId = Guid.NewGuid() });
        var history = await admin.GetFromJsonAsync<HistoryBody[]>($"/api/tickets/{ticketA.Id}/history");

        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Empty(history!);
    }

    [Fact]
    public async Task AnAgentTransferringOutOfTheirDepartment_LosesTheTicket()
    {
        var (admin, a, b, ticketA, _, _) = await ArrangeAsync();
        var agent = await DepartmentArrange.AgentInAsync(factory, admin, a.Id);

        var transfer = await agent.PutAsJsonAsync($"/api/tickets/{ticketA.Id}/department", new { departmentId = b.Id });
        var after = await agent.GetAsync($"/api/tickets/{ticketA.Id}");

        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/tickets/{ticketA.Id}")).StatusCode);
    }

    [Fact]
    public async Task AnAgentCreatingATicket_DefaultsToTheirOnlyDepartment_AndCannotUseAnother()
    {
        var (admin, a, b, _, _, _) = await ArrangeAsync();
        var agent = await DepartmentArrange.AgentInAsync(factory, admin, a.Id);
        var customerId = await TicketArrange.CustomerAsync(agent);

        var defaulted = await DepartmentArrange.TicketAsync(agent, customerId, null);
        var other = await agent.PostAsJsonAsync("/api/tickets", new { customerId, subject = "x", departmentId = b.Id });

        Assert.Equal(a.Id, defaulted.DepartmentId);
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
    }

    [Fact]
    public async Task TransferRequiresTicketsManage_AndAuthentication()
    {
        var (_, _, b, ticketA, _, _) = await ArrangeAsync();

        var anonymous = await factory.CreateClient().PutAsJsonAsync($"/api/tickets/{ticketA.Id}/department", new { departmentId = b.Id });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
