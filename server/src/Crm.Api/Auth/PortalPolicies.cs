using System.Security.Claims;

namespace Crm.Api.Auth;

/// <summary>
/// Authorization of the customer portal (CRM-40). A portal token has the role <c>Customer</c> and no staff permission,
/// so every staff endpoint answers 403 to it; the portal endpoints require the role, so a staff token gets 403 there.
/// </summary>
public static class PortalPolicies
{
    /// <summary>A signed-in portal customer.</summary>
    public const string Customer = "Portal";
}

public static class PortalUser
{
    /// <summary>The customer id (<c>sub</c>) of a portal token.</summary>
    public static Guid CustomerId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(AuthClaimTypes.UserId), out var id)
            ? id
            : throw new InvalidOperationException("The token has no customer id.");

    public static string Email(ClaimsPrincipal user) => user.FindFirstValue(AuthClaimTypes.Email) ?? string.Empty;
}
