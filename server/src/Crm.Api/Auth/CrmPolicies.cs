namespace Crm.Api.Auth;

/// <summary>
/// Authorization policy names used by the endpoints. Endpoints only name a policy; what the policy requires
/// is defined once in <see cref="AuthenticationExtensions.AddCrmAuthentication"/> (roles now, permissions in CRM-7).
/// </summary>
public static class CrmPolicies
{
    /// <summary>Create, edit, deactivate and list staff users (/api/users).</summary>
    public const string ManageUsers = "ManageUsers";
}
