using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Departments;

public sealed record DepartmentBody(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record DepartmentSlaBody(Guid DepartmentId, string Priority, int ResponseMinutes, int ResolutionMinutes, DateTime UpdatedAt);

public sealed record DepartmentTicketBody(
    Guid Id, string Number, string Subject, Guid? DepartmentId, string? DepartmentName, DateTime CreatedAt, DateTime? ResponseDueAt, DateTime? ResolutionDueAt);

public sealed record DepartmentPage(DepartmentTicketBody[] Items, int Page, int PageSize, int TotalCount);

/// <summary>Arranges departments, restricted agents and department tickets through the real API.</summary>
public static class DepartmentArrange
{
    public static async Task<HttpClient> SuperAdminAsync(CrmApiFactory factory) =>
        factory.CreateAuthenticatedClient(await factory.LoginAsync());

    public static async Task<DepartmentBody> DepartmentAsync(HttpClient admin, string prefix = "Dept")
    {
        var response = await admin.PostAsJsonAsync("/api/departments", new { name = $"{prefix} {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DepartmentBody>())!;
    }

    /// <summary>A signed-in Agent (only the Agent role) who is a member of the given departments.</summary>
    public static async Task<HttpClient> AgentInAsync(CrmApiFactory factory, HttpClient superAdmin, params Guid[] departmentIds)
    {
        var email = $"agent-{Guid.NewGuid():N}@crm.local";
        var id = await factory.CreateUserAsync(email, CrmApiFactory.TestUserPassword, Roles.Agent);
        var response = await superAdmin.PutAsJsonAsync($"/api/users/{id}",
            new { email, fullName = email, roles = new[] { Roles.Agent }, departmentIds });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return factory.CreateAuthenticatedClient(await factory.LoginAsync(email, CrmApiFactory.TestUserPassword));
    }

    public static async Task<DepartmentTicketBody> TicketAsync(
        HttpClient client, Guid customerId, Guid? departmentId, string priority = "high", string subject = "Invoice is wrong")
    {
        var response = await client.PostAsJsonAsync("/api/tickets", new { customerId, subject, priority, departmentId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DepartmentTicketBody>())!;
    }
}
