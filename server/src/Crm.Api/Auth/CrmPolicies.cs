using Crm.Application.Auth;

namespace Crm.Api.Auth;

/// <summary>
/// Authorization policy names. Since CRM-7 every permission in <see cref="Permissions.All"/> is a policy with the
/// same name (registered in <see cref="AuthenticationExtensions.AddCrmAuthentication"/>), so new endpoints call
/// <c>RequireAuthorization(Permissions.X)</c> directly. The alias below keeps the CRM-6 endpoints unchanged.
/// </summary>
public static class CrmPolicies
{
    /// <summary>Create, edit, deactivate and list staff users (/api/users).</summary>
    public const string ManageUsers = Permissions.UsersManage;
}
