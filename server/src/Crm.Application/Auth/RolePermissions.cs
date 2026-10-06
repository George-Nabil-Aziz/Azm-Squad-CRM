namespace Crm.Application.Auth;

/// <summary>
/// Which permissions each seeded role has (code-defined; there is no permission editing). A user's permissions
/// are the union over their roles. SuperAdmin has every permission, so it can call every endpoint.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyList<string> AgentPermissions =
    [
        Permissions.CustomersView, Permissions.CustomersManage,
        Permissions.TicketsView, Permissions.TicketsManage,
    ];

    private static readonly Dictionary<string, IReadOnlyList<string>> ByRole = new(StringComparer.Ordinal)
    {
        [Roles.SuperAdmin] = Permissions.All,
        [Roles.Admin] = [.. Permissions.All.Except([Permissions.UsersManageSuperAdmins, Permissions.SlaManage, Permissions.SettingsManage])],
        [Roles.Supervisor] = InCatalogueOrder([.. AgentPermissions, Permissions.TicketsAssign, Permissions.ReportsView]),
        [Roles.Agent] = AgentPermissions,
    };

    /// <summary>The permissions of one role (case-sensitive name); an unknown role has none.</summary>
    public static IReadOnlyList<string> ForRole(string role) =>
        ByRole.TryGetValue(role, out var permissions) ? permissions : [];

    /// <summary>The permissions of a user with these roles, without duplicates, in catalogue order.</summary>
    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles) =>
        InCatalogueOrder(roles.SelectMany(ForRole));

    public static bool HasPermission(IEnumerable<string> roles, string permission) =>
        roles.Any(role => ForRole(role).Contains(permission, StringComparer.Ordinal));

    private static IReadOnlyList<string> InCatalogueOrder(IEnumerable<string> permissions)
    {
        var set = permissions.ToHashSet(StringComparer.Ordinal);
        return [.. Permissions.All.Where(set.Contains)];
    }
}
