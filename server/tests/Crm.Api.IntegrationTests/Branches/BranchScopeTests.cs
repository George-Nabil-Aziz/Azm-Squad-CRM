using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Branches;

/// <summary>CRM-62 AC 1, 2 and 4: customers and tickets are linked to branches; branch users see only their branch.</summary>
public class BranchScopeTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private async Task<(HttpClient SuperAdmin, BranchBody A, BranchBody B, BranchCustomerBody CustomerA, BranchCustomerBody CustomerB, BranchCustomerBody NoBranch,
        BranchTicketBody TicketA, BranchTicketBody TicketB)> ArrangeAsync()
    {
        var superAdmin = await BranchArrange.SuperAdminAsync(factory);
        var a = await BranchArrange.BranchAsync(superAdmin, "A");
        var b = await BranchArrange.BranchAsync(superAdmin, "B");
        var customerA = await BranchArrange.CustomerAsync(superAdmin, a.Id, "Customer A");
        var customerB = await BranchArrange.CustomerAsync(superAdmin, b.Id, "Customer B");
        var none = await BranchArrange.CustomerAsync(superAdmin, null, "Customer none");
        return (superAdmin, a, b, customerA, customerB, none,
            await BranchArrange.TicketAsync(superAdmin, customerA.Id), await BranchArrange.TicketAsync(superAdmin, customerB.Id));
    }

    [Fact]
    public async Task ACustomerAndItsTickets_CarryTheBranch()
    {
        var (superAdmin, a, _, customerA, _, none, ticketA, _) = await ArrangeAsync();

        var noneTicket = await BranchArrange.TicketAsync(superAdmin, none.Id);

        Assert.Equal(a.Id, customerA.BranchId);
        Assert.Equal(a.Id, ticketA.BranchId);
        Assert.Null(noneTicket.BranchId);
    }

    [Fact]
    public async Task ABranchUser_SeesOnlyTheirBranchCustomers_And404ForOthers()
    {
        var (superAdmin, a, _, customerA, customerB, none, _, _) = await ArrangeAsync();
        var supervisor = await BranchArrange.UserInBranchAsync(factory, superAdmin, a.Id);

        var page = await supervisor.GetFromJsonAsync<BranchCustomerPage>("/api/customers?pageSize=100");

        Assert.Equal([customerA.Id], page!.Items.Select(c => c.Id));
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync($"/api/customers/{customerA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisor.GetAsync($"/api/customers/{customerB.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisor.GetAsync($"/api/customers/{none.Id}")).StatusCode);
    }

    [Fact]
    public async Task ABranchUser_SeesOnlyTheirBranchTickets_And404ForOthers()
    {
        var (superAdmin, a, _, _, _, _, ticketA, ticketB) = await ArrangeAsync();
        var supervisor = await BranchArrange.UserInBranchAsync(factory, superAdmin, a.Id);

        var page = await supervisor.GetFromJsonAsync<BranchTicketPage>("/api/tickets?pageSize=100");

        Assert.Equal([ticketA.Id], page!.Items.Select(t => t.Id));
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync($"/api/tickets/{ticketA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisor.GetAsync($"/api/tickets/{ticketB.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisor.GetAsync($"/api/tickets/{ticketB.Id}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisor.GetAsync($"/api/tickets/{ticketB.Id}/history")).StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_AndUsersWithoutABranch_SeeEveryBranch()
    {
        var (superAdmin, _, _, customerA, customerB, _, ticketA, ticketB) = await ArrangeAsync();
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        foreach (var client in new[] { superAdmin, admin })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/customers/{customerA.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/customers/{customerB.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/tickets/{ticketA.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/tickets/{ticketB.Id}")).StatusCode);
        }
    }

    [Fact]
    public async Task ABranchUserCreatingACustomer_GetsTheirBranch_AndCannotNameAnother()
    {
        var (superAdmin, a, b, _, _, _, _, _) = await ArrangeAsync();
        var supervisor = await BranchArrange.UserInBranchAsync(factory, superAdmin, a.Id);

        var own = await BranchArrange.CustomerAsync(supervisor, null, "Mine");
        var other = await supervisor.PostAsJsonAsync("/api/customers", new { name = "Other", branchId = b.Id });
        var ticket = await BranchArrange.TicketAsync(supervisor, own.Id);

        Assert.Equal(a.Id, own.BranchId);
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
        Assert.Equal(a.Id, ticket.BranchId);
    }

    [Fact]
    public async Task MovingACustomerToAnotherBranch_MovesItsTickets()
    {
        var (superAdmin, a, b, customerA, _, _, ticketA, _) = await ArrangeAsync();
        var supervisorA = await BranchArrange.UserInBranchAsync(factory, superAdmin, a.Id);
        var supervisorB = await BranchArrange.UserInBranchAsync(factory, superAdmin, b.Id);

        var move = await superAdmin.PutAsJsonAsync($"/api/customers/{customerA.Id}", new { name = customerA.Name, branchId = b.Id });
        var ticket = await superAdmin.GetFromJsonAsync<BranchTicketBody>($"/api/tickets/{ticketA.Id}");

        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        Assert.Equal(b.Id, ticket!.BranchId);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisorA.GetAsync($"/api/tickets/{ticketA.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await supervisorB.GetAsync($"/api/tickets/{ticketA.Id}")).StatusCode);
    }

    [Fact]
    public async Task ABranchUser_CannotMoveACustomerToAnotherBranch()
    {
        var (superAdmin, a, b, customerA, _, _, _, _) = await ArrangeAsync();
        var supervisor = await BranchArrange.UserInBranchAsync(factory, superAdmin, a.Id);

        var response = await supervisor.PutAsJsonAsync($"/api/customers/{customerA.Id}", new { name = customerA.Name, branchId = b.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ASuperAdminWhoIsAssignedABranch_IsNotRestricted()
    {
        var (superAdmin, a, _, _, customerB, _, _, _) = await ArrangeAsync();
        var email = $"super-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.SuperAdmin);
        await superAdmin.PutAsJsonAsync($"/api/users/{id}/branch", new { branchId = a.Id });
        var client = factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/customers/{customerB.Id}")).StatusCode);
    }
}
