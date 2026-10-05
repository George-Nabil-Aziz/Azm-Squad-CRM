using System.Security.Claims;
using Crm.Application.Auth;
using Crm.Application.Common.Security;

namespace Crm.Api.Auth;

/// <summary>The current request's user, read from the validated JWT claims.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(AuthClaimTypes.UserId), out var id) ? id : null;

    // RoleClaimType is "role" (AddCrmAuthentication), so IsInRole reads our role claims.
    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public bool HasPermission(string permission) =>
        Principal is not null
        && RolePermissions.HasPermission(Principal.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value), permission);
}
