using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace Crm.Api.IntegrationTests.Branches;

/// <summary>CRM-62 AC 1: branch administration (SuperAdmin) and the branch of a user.</summary>
public class BranchesTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact]
    public async Task SuperAdminCreatesABranch_AnyStaffUserCanListIt()
    {
        var superAdmin = await BranchArrange.SuperAdminAsync(factory);
        var agent = await factory.CreateClientWithRoleAsync(Roles.Agent);

        var created = await BranchArrange.BranchAsync(superAdmin, "Riyadh");
        var list = await agent.GetFromJsonAsync<BranchBody[]>("/api/branches?activeOnly=true");

        Assert.True(created.IsActive);
        Assert.Equal(DateTimeKind.Utc, created.CreatedAt.Kind);
        Assert.Contains(list!, b => b.Id == created.Id && b.Name == created.Name);
    }

    [Fact]
    public async Task OnlySuperAdmin_CanCreateOrEdit_AndAnonymousGets401()
    {
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);

        var create = await admin.PostAsJsonAsync("/api/branches", new { name = "Nope" });
        var update = await admin.PutAsJsonAsync($"/api/branches/{Guid.NewGuid()}", new { name = "Nope" });
        var anonymous = await factory.CreateClient().GetAsync("/api/branches");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task DuplicateName_Returns400_AndDeactivatedBranchLeavesTheActiveList()
    {
        var superAdmin = await BranchArrange.SuperAdminAsync(factory);
        var branch = await BranchArrange.BranchAsync(superAdmin);

        var duplicate = await superAdmin.PostAsJsonAsync("/api/branches", new { name = $" {branch.Name.ToUpperInvariant()} " });
        var update = await superAdmin.PutAsJsonAsync($"/api/branches/{branch.Id}", new { name = branch.Name, isActive = false });
        var active = await superAdmin.GetFromJsonAsync<BranchBody[]>("/api/branches?activeOnly=true");
        var missing = await superAdmin.PutAsJsonAsync($"/api/branches/{Guid.NewGuid()}", new { name = "X" });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(["name"], (await duplicate.Content.ReadFromJsonAsync<HttpValidationProblemDetails>())!.Errors.Keys);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.DoesNotContain(active!, b => b.Id == branch.Id);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ABranchUser_ListsOnlyTheirOwnBranch()
    {
        var superAdmin = await BranchArrange.SuperAdminAsync(factory);
        var mine = await BranchArrange.BranchAsync(superAdmin);
        await BranchArrange.BranchAsync(superAdmin);
        var supervisor = await BranchArrange.UserInBranchAsync(factory, superAdmin, mine.Id);

        var list = await supervisor.GetFromJsonAsync<BranchBody[]>("/api/branches");

        Assert.Equal([mine.Id], list!.Select(b => b.Id));
    }

    [Fact]
    public async Task SettingAUsersBranch_NeedsBranchesManage_AndAnUnknownBranchIs400()
    {
        var superAdmin = await BranchArrange.SuperAdminAsync(factory);
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var branch = await BranchArrange.BranchAsync(superAdmin);
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);

        var forbidden = await admin.PutAsJsonAsync($"/api/users/{id}/branch", new { branchId = branch.Id });
        var unknown = await superAdmin.PutAsJsonAsync($"/api/users/{id}/branch", new { branchId = Guid.NewGuid() });
        var ok = await superAdmin.PutAsJsonAsync($"/api/users/{id}/branch", new { branchId = branch.Id });
        var read = await superAdmin.GetFromJsonAsync<BranchUserBody>($"/api/users/{id}");
        var cleared = await superAdmin.PutAsJsonAsync($"/api/users/{id}/branch", new { branchId = (Guid?)null });
        var afterClear = await superAdmin.GetFromJsonAsync<BranchUserBody>($"/api/users/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(branch.Id, read!.BranchId);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null(afterClear!.BranchId);
    }

    [Fact]
    public async Task CreatingAUserWithABranch_NeedsBranchesManage()
    {
        var superAdmin = await BranchArrange.SuperAdminAsync(factory);
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var branch = await BranchArrange.BranchAsync(superAdmin);
        object Body() => new
        {
            email = $"new-{Guid.NewGuid():N}@crm.local", fullName = "New", password = "Test#User123", roles = new[] { Roles.Agent }, branchId = branch.Id,
        };

        var forbidden = await admin.PostAsJsonAsync("/api/users", Body());
        var created = await superAdmin.PostAsJsonAsync("/api/users", Body());

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(branch.Id, (await created.Content.ReadFromJsonAsync<BranchUserBody>())!.BranchId);
    }
}
