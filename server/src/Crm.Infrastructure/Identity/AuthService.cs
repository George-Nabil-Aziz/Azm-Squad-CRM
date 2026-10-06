using Crm.Application.Audit;
using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Audit;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IAccessTokenGenerator tokenGenerator,
    IValidator<LoginRequest> validator,
    IAuditLogger audit) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);

        var email = request.Email!.Trim();
        var user = await userManager.FindByEmailAsync(request.Email!);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            var reason = user is null ? "unknown-user" : !user.IsActive ? "inactive-user" : "locked-out";
            await LogFailureAsync(user, email, reason, cancellationToken);
            throw new UnauthorizedException(AuthText.InvalidCredentials);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password!))
        {
            await userManager.AccessFailedAsync(user);
            await LogFailureAsync(user, email, "wrong-password", cancellationToken);
            throw new UnauthorizedException(AuthText.InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var token = tokenGenerator.Generate(
            new AccessTokenSubject(user.Id, user.Email!, user.FullName, [.. roles]));
        await audit.LogAsync(
            new AuditEvent(AuditActions.LoginSucceeded, "User", user.Id.ToString(), UserId: user.Id, UserEmail: user.Email),
            cancellationToken);

        return new LoginResponse(token.Token, "Bearer", token.ExpiresAt);
    }

    // The reason is for administrators only: the HTTP answer stays the same generic message (AuthText.InvalidCredentials).
    private Task LogFailureAsync(ApplicationUser? user, string email, string reason, CancellationToken cancellationToken) =>
        audit.LogAsync(
            new AuditEvent(AuditActions.LoginFailed, "User", user?.Id.ToString(), NewValues: new { reason },
                UserId: user?.Id, UserEmail: email),
            cancellationToken);
}
