using FluentValidation;

namespace Crm.Application.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // FluentValidation translates its built-in messages (CurrentUICulture); WithName translates the field name.
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256).WithName(_ => AuthText.EmailField);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128).WithName(_ => AuthText.PasswordField);
    }
}
