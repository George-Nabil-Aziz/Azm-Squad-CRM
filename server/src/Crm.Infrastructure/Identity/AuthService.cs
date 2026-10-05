using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IAccessTokenGenerator tokenGenerator,
    IValidator<LoginRequest> validator) : IAuthService
{
    // One message for unknown email, wrong password and locked-out user: never reveal which one it was.
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);

        var user = await userManager.FindByEmailAsync(request.Email!);
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password!))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var token = tokenGenerator.Generate(
            new AccessTokenSubject(user.Id, user.Email!, user.FullName, [.. roles]));

        return new LoginResponse(token.Token, "Bearer", token.ExpiresAt);
    }
}
