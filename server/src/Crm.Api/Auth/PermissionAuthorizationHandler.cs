using Crm.Application.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Auth;

/// <summary>Requirement of the policy named after <see cref="Permission"/> (one policy per permission).</summary>
public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>
/// Succeeds when one of the user's <c>role</c> claims grants the permission (<see cref="RolePermissions"/>).
/// Permissions are not stored in the token: they are derived from the roles on every request, so the code-defined
/// matrix is the single source of truth.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value);
        if (RolePermissions.HasPermission(roles, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
