using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Departments;

/// <summary>CRM-61 AC 1: department administration and user memberships.</summary>
public class DepartmentsTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    private sealed record UserBody(Guid Id, string Email, string FullName, string[] Roles, bool IsActive, Guid[] DepartmentIds);

    [Fact]
    public async Task AdminCreatesADepartment_ItIsListedForAnAgent()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var created = await DepartmentArrange.DepartmentAsync(admin, "Billing");
        var list = await agent.GetFromJsonAsync<DepartmentBody[]>("/api/departments?activeOnly=true");

        Assert.True(created.IsActive);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
        Assert.Contains(list!, d => d.Id == created.Id && d.Name == created.Name);
    }

    [Fact]
    public async Task AnAgent_CannotCreateOrEditDepartments_And_AnonymousGets401()
    {
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var create = await agent.PostAsJsonAsync("/api/departments", new { name = "Nope" });
        var update = await agent.PutAsJsonAsync($"/api/departments/{Guid.NewGuid()}", new { name = "Nope" });
        var anonymous = await factory.CreateClient().GetAsync("/api/departments");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task DuplicateName_Returns400_OnName()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var first = await DepartmentArrange.DepartmentAsync(admin);

        var response = await admin.PostAsJsonAsync("/api/departments", new { name = $" {first.Name.ToUpperInvariant()} " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal(["name"], problem!.Errors.Keys);
    }

    [Fact]
    public async Task Deactivated_DisappearsFromTheActiveList_AndUnknownIs404()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var created = await DepartmentArrange.DepartmentAsync(admin);

        var update = await admin.PutAsJsonAsync($"/api/departments/{created.Id}", new { name = created.Name, isActive = false });
        var active = await admin.GetFromJsonAsync<DepartmentBody[]>("/api/departments?activeOnly=true");
        var missing = await admin.PutAsJsonAsync($"/api/departments/{Guid.NewGuid()}", new { name = "X" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.DoesNotContain(active!, d => d.Id == created.Id);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task AUser_CanBeInSeveralDepartments_AndTheyRoundTrip()
    {
        var superAdmin = await DepartmentArrange.SuperAdminAsync(factory);
        var a = await DepartmentArrange.DepartmentAsync(superAdmin);
        var b = await DepartmentArrange.DepartmentAsync(superAdmin);
        var email = $"multi-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);

        var put = await superAdmin.PutAsJsonAsync($"/api/users/{id}",
            new { email, fullName = "Multi", roles = new[] { Roles.Agent }, departmentIds = new[] { a.Id, b.Id } });
        var read = await superAdmin.GetFromJsonAsync<UserBody>($"/api/users/{id}");
        var keep = await superAdmin.PutAsJsonAsync($"/api/users/{id}",
            new { email, fullName = "Multi 2", roles = new[] { Roles.Agent } }); // null = unchanged
        var afterKeep = await superAdmin.GetFromJsonAsync<UserBody>($"/api/users/{id}");
        var clear = await superAdmin.PutAsJsonAsync($"/api/users/{id}",
            new { email, fullName = "Multi 2", roles = new[] { Roles.Agent }, departmentIds = Array.Empty<Guid>() });
        var afterClear = await superAdmin.GetFromJsonAsync<UserBody>($"/api/users/{id}");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(new[] { a.Id, b.Id }.Order(), read!.DepartmentIds.Order());
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
        Assert.Equal(2, afterKeep!.DepartmentIds.Length);
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        Assert.Empty(afterClear!.DepartmentIds);
    }

    [Fact]
    public async Task UnknownDepartmentOnAUser_Returns400_OnDepartmentIds()
    {
        var superAdmin = await DepartmentArrange.SuperAdminAsync(factory);
        var email = $"bad-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);

        var response = await superAdmin.PutAsJsonAsync($"/api/users/{id}",
            new { email, fullName = "Bad", roles = new[] { Roles.Agent }, departmentIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Contains("departmentIds", problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreatingAUserWithDepartments_StoresThem()
    {
        var superAdmin = await DepartmentArrange.SuperAdminAsync(factory);
        var a = await DepartmentArrange.DepartmentAsync(superAdmin);

        var response = await superAdmin.PostAsJsonAsync("/api/users", new
        {
            email = $"new-{Guid.NewGuid():N}@crm.local", fullName = "New Agent", password = "Test#User123",
            roles = new[] { Roles.Agent }, departmentIds = new[] { a.Id },
        });
        var body = await response.Content.ReadFromJsonAsync<UserBody>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([a.Id], body!.DepartmentIds);
    }

    [Fact]
    public async Task TicketsCanBeFilteredByDepartment_AndShowTheDepartmentName()
    {
        var superAdmin = await DepartmentArrange.SuperAdminAsync(factory);
        var a = await DepartmentArrange.DepartmentAsync(superAdmin);
        var b = await DepartmentArrange.DepartmentAsync(superAdmin);
        var customerId = await TicketArrange.CustomerAsync(superAdmin);
        var inA = await DepartmentArrange.TicketAsync(superAdmin, customerId, a.Id);
        await DepartmentArrange.TicketAsync(superAdmin, customerId, b.Id);

        var page = await superAdmin.GetFromJsonAsync<DepartmentPage>($"/api/tickets?departmentId={a.Id}&pageSize=100");

        Assert.Equal([inA.Id], page!.Items.Select(t => t.Id));
        Assert.Equal(a.Name, page.Items[0].DepartmentName);
    }
}
