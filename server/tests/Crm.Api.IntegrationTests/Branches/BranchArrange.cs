using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Branches;

public sealed record BranchBody(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record BranchCustomerBody(Guid Id, string Name, Guid? BranchId);

public sealed record BranchCustomerPage(BranchCustomerBody[] Items, int Page, int PageSize, int TotalCount);

public sealed record BranchTicketBody(Guid Id, string Number, Guid CustomerId, Guid? BranchId);

public sealed record BranchTicketPage(BranchTicketBody[] Items, int Page, int PageSize, int TotalCount);

public sealed record BranchUserBody(Guid Id, string Email, string FullName, string[] Roles, bool IsActive, Guid[] DepartmentIds, Guid? BranchId);

/// <summary>Arranges branches, branch users, customers and tickets through the real API.</summary>
public static class BranchArrange
{
    public static async Task<HttpClient> SuperAdminAsync(CrmApiFactory factory) =>
        factory.CreateAuthenticatedClient(await factory.LoginAsync());

    public static async Task<BranchBody> BranchAsync(HttpClient superAdmin, string prefix = "Branch")
    {
        var response = await superAdmin.PostAsJsonAsync("/api/branches", new { name = $"{prefix} {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BranchBody>())!;
    }

    /// <summary>A signed-in user with the role who belongs to the branch (assigned by the SuperAdmin).</summary>
    public static async Task<HttpClient> UserInBranchAsync(CrmApiFactory factory, HttpClient superAdmin, Guid branchId, string role = Roles.Supervisor)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, role);
        var response = await superAdmin.PutAsJsonAsync($"/api/users/{id}/branch", new { branchId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));
    }

    public static async Task<BranchCustomerBody> CustomerAsync(HttpClient client, Guid? branchId, string name = "Nour Trading")
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name, branchId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BranchCustomerBody>())!;
    }

    public static async Task<BranchTicketBody> TicketAsync(HttpClient client, Guid customerId, string priority = "high")
    {
        var response = await client.PostAsJsonAsync("/api/tickets", new { customerId, subject = "Invoice is wrong", priority });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BranchTicketBody>())!;
    }
}
