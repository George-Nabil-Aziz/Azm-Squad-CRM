using System.Net;
using System.Net.Http.Json;
using Crm.Api.IntegrationTests.Infrastructure;
using Crm.Api.IntegrationTests.Tickets;
using Crm.Application.Auth;

namespace Crm.Api.IntegrationTests.Departments;

/// <summary>CRM-61 AC 4: an SLA policy can optionally be set per department.</summary>
public class DepartmentSlaTests(CrmApiFactory factory) : IClassFixture<CrmApiFactory>
{
    [Fact]
    public async Task ADepartmentOverride_IsUsedForItsNewTickets_OtherDepartmentsKeepTheGlobalPolicy()
    {
        var superAdmin = await DepartmentArrange.SuperAdminAsync(factory);
        var vip = await DepartmentArrange.DepartmentAsync(superAdmin, "Vip");
        var normal = await DepartmentArrange.DepartmentAsync(superAdmin, "Normal");
        var customerId = await TicketArrange.CustomerAsync(superAdmin);

        var put = await superAdmin.PutAsJsonAsync($"/api/departments/{vip.Id}/sla-policies/high", new { responseMinutes = 15, resolutionMinutes = 60 });
        var inVip = await DepartmentArrange.TicketAsync(superAdmin, customerId, vip.Id, "high");
        var inNormal = await DepartmentArrange.TicketAsync(superAdmin, customerId, normal.Id, "high");
        var vipLow = await DepartmentArrange.TicketAsync(superAdmin, customerId, vip.Id, "low");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(inVip.CreatedAt.AddMinutes(15), inVip.ResponseDueAt);
        Assert.Equal(inVip.CreatedAt.AddMinutes(60), inVip.ResolutionDueAt);
        Assert.Equal(inNormal.CreatedAt.AddMinutes(120), inNormal.ResponseDueAt); // global High: 2 h
        Assert.Equal(vipLow.CreatedAt.AddMinutes(480), vipLow.ResponseDueAt); // no Low override: global Low 8 h
    }

    [Fact]
    public async Task ChangingThePriority_UsesTheDepartmentOverride()
    {
        var superAdmin = await DepartmentArrange.SuperAdminAsync(factory);
        var vip = await DepartmentArrange.DepartmentAsync(superAdmin, "Vip");
        var customerId = await TicketArrange.CustomerAsync(superAdmin);
        await superAdmin.PutAsJsonAsync($"/api/departments/{vip.Id}/sla-policies/high", new { responseMinutes = 10, resolutionMinutes = 20 });
        var ticket = await DepartmentArrange.TicketAsync(superAdmin, customerId, vip.Id, "low");

        var response = await superAdmin.PutAsJsonAsync($"/api/tickets/{ticket.Id}/priority", new { priority = "high" });
        var updated = await response.Content.ReadFromJsonAsync<DepartmentTicketBody>();

        Assert.Equal(ticket.CreatedAt.AddMinutes(10), updated!.ResponseDueAt);
    }

    [Fact]
    public async Task DeletingTheOverride_FallsBackToTheGlobalPolicy()
    {
        var superAdmin = await DepartmentArrange.SuperAdminAsync(factory);
        var vip = await DepartmentArrange.DepartmentAsync(superAdmin, "Vip");
        var customerId = await TicketArrange.CustomerAsync(superAdmin);
        await superAdmin.PutAsJsonAsync($"/api/departments/{vip.Id}/sla-policies/mid", new { responseMinutes = 5, resolutionMinutes = 10 });

        var listed = await superAdmin.GetFromJsonAsync<DepartmentSlaBody[]>($"/api/departments/{vip.Id}/sla-policies");
        var delete = await superAdmin.DeleteAsync($"/api/departments/{vip.Id}/sla-policies/mid");
        var after = await superAdmin.GetFromJsonAsync<DepartmentSlaBody[]>($"/api/departments/{vip.Id}/sla-policies");
        var ticket = await DepartmentArrange.TicketAsync(superAdmin, customerId, vip.Id, "mid");

        Assert.Equal(["mid"], listed!.Select(p => p.Priority));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(after!);
        Assert.Equal(ticket.CreatedAt.AddMinutes(240), ticket.ResponseDueAt); // global Mid: 4 h
    }

    [Fact]
    public async Task InvalidMinutes_Return400_UnknownPriorityOrDepartment404_AndOnlySuperAdminMaySet()
    {
        var superAdmin = await DepartmentArrange.SuperAdminAsync(factory);
        var admin = await factory.CreateClientWithRoleAsync(Roles.Admin);
        var vip = await DepartmentArrange.DepartmentAsync(superAdmin, "Vip");

        var invalid = await superAdmin.PutAsJsonAsync($"/api/departments/{vip.Id}/sla-policies/high", new { responseMinutes = 100, resolutionMinutes = 50 });
        var badPriority = await superAdmin.PutAsJsonAsync($"/api/departments/{vip.Id}/sla-policies/urgent", new { responseMinutes = 10, resolutionMinutes = 50 });
        var badDepartment = await superAdmin.PutAsJsonAsync($"/api/departments/{Guid.NewGuid()}/sla-policies/high", new { responseMinutes = 10, resolutionMinutes = 50 });
        var forbidden = await admin.PutAsJsonAsync($"/api/departments/{vip.Id}/sla-policies/high", new { responseMinutes = 10, resolutionMinutes = 50 });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badPriority.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badDepartment.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
