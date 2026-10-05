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
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateOrThrowAsync(request, cancellationToken);

        var user = await userManager.FindByEmailAsync(request.Email!);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(AuthText.InvalidCredentials);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password!))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException(AuthText.InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        var token = tokenGenerator.Generate(
            new AccessTokenSubject(user.Id, user.Email!, user.FullName, [.. roles]));

        return new LoginResponse(token.Token, "Bearer", token.ExpiresAt);
    }
}
