using System.Reflection;
using Crm.Application.Auth;

namespace Crm.UnitTests.Auth;

public class RolePermissionsTests
{
    [Fact]
    public void Catalogue_ListsEveryPermissionConstantOnce()
    {
        var constants = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(constants.Order(StringComparer.Ordinal), Permissions.All.Order(StringComparer.Ordinal));
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(Permissions.All, permission => Assert.Matches(@"^[a-z]+(-[a-z]+)*\.[a-z]+(-[a-z]+)*$", permission));
    }

    [Fact]
    public void EverySeededRole_HasPermissions()
    {
        Assert.All(Roles.All, role => Assert.NotEmpty(RolePermissions.ForRole(role)));
    }

    [Fact]
    public void SuperAdmin_HasEveryPermission()
    {
        Assert.Equal(Permissions.All, RolePermissions.ForRole(Roles.SuperAdmin));
    }

    [Fact]
    public void Admin_HasEverythingExceptTheSuperAdminOnlyPermissions()
    {
        Assert.Equal(
            Permissions.All.Except([Permissions.UsersManageSuperAdmins, Permissions.SlaManage, Permissions.SettingsManage]),
            RolePermissions.ForRole(Roles.Admin));
    }

    [Fact]
    public void Supervisor_WorksTicketsAndCustomers_AssignsTickets_AndSeesReports()
    {
        Assert.Equal(
            [Permissions.CustomersView, Permissions.CustomersManage, Permissions.TicketsView, Permissions.TicketsManage,
             Permissions.TicketsAssign, Permissions.NotificationsView, Permissions.TasksManage, Permissions.QuickRepliesManageShared, Permissions.ReportsView, Permissions.KbView],
            RolePermissions.ForRole(Roles.Supervisor));
    }

    [Fact]
    public void SettingsManage_IsForSuperAdminOnly()
    {
        Assert.True(RolePermissions.HasPermission([Roles.SuperAdmin], Permissions.SettingsManage));
        Assert.False(RolePermissions.HasPermission([Roles.Admin], Permissions.SettingsManage));
        Assert.False(RolePermissions.HasPermission([Roles.Supervisor], Permissions.SettingsManage));
        Assert.False(RolePermissions.HasPermission([Roles.Agent], Permissions.SettingsManage));
    }

    [Fact]
    public void DepartmentsManage_IsForSuperAdminAndAdminOnly()
    {
        Assert.True(RolePermissions.HasPermission([Roles.SuperAdmin], Permissions.DepartmentsManage));
        Assert.True(RolePermissions.HasPermission([Roles.Admin], Permissions.DepartmentsManage));
        Assert.False(RolePermissions.HasPermission([Roles.Supervisor], Permissions.DepartmentsManage));
        Assert.False(RolePermissions.HasPermission([Roles.Agent], Permissions.DepartmentsManage));
    }

    [Fact]
    public void AuditView_IsForSuperAdminAndAdminOnly()
    {
        Assert.True(RolePermissions.HasPermission([Roles.SuperAdmin], Permissions.AuditView));
        Assert.True(RolePermissions.HasPermission([Roles.Admin], Permissions.AuditView));
        Assert.False(RolePermissions.HasPermission([Roles.Supervisor], Permissions.AuditView));
        Assert.False(RolePermissions.HasPermission([Roles.Agent], Permissions.AuditView));
    }

    [Fact]
    public void Agent_WorksTicketsAndCustomersOnly()
    {
        Assert.Equal(
            [Permissions.CustomersView, Permissions.CustomersManage, Permissions.TicketsView, Permissions.TicketsManage, Permissions.NotificationsView, Permissions.TasksManage,
             Permissions.KbView],
            RolePermissions.ForRole(Roles.Agent));
    }

    [Fact]
    public void KnowledgeBase_AgentsRead_OnlyAdminsWrite()
    {
        Assert.True(RolePermissions.HasPermission([Roles.Agent], Permissions.KbView));
        Assert.False(RolePermissions.HasPermission([Roles.Agent], Permissions.KbManage));
        Assert.False(RolePermissions.HasPermission([Roles.Supervisor], Permissions.KbManage));
        Assert.True(RolePermissions.HasPermission([Roles.Admin], Permissions.KbManage));
        Assert.True(RolePermissions.HasPermission([Roles.SuperAdmin], Permissions.KbManage));
    }

    [Theory]
    [InlineData(Permissions.UsersManage)]
    [InlineData(Permissions.CategoriesManage)]
    [InlineData(Permissions.SlaManage)]
    [InlineData(Permissions.ChannelsManage)]
    [InlineData(Permissions.AuditView)]
    [InlineData(Permissions.SettingsManage)]
    public void Agent_HasNoAdminSettingsPermission(string permission)
    {
        Assert.False(RolePermissions.HasPermission([Roles.Agent], permission));
    }

    [Theory]
    [InlineData("admin")] // role names are case-sensitive
    [InlineData("Customer")]
    [InlineData("")]
    public void UnknownRole_HasNoPermissions(string role)
    {
        Assert.Empty(RolePermissions.ForRole(role));
        Assert.False(RolePermissions.HasPermission([role], Permissions.TicketsView));
    }

    [Fact]
    public void ForRoles_CombinesRoles_WithoutDuplicates_InCatalogueOrder()
    {
        var combined = RolePermissions.ForRoles([Roles.Supervisor, Roles.Agent, Roles.Supervisor]);

        Assert.Equal(RolePermissions.ForRole(Roles.Supervisor), combined);
        Assert.Empty(RolePermissions.ForRoles([]));
    }
}
