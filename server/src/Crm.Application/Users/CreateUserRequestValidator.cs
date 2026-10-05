using FluentValidation;

namespace Crm.Application.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FullName).ValidFullName();
        // Same policy as ASP.NET Identity in Crm.Infrastructure (8+ chars, upper, lower, digit, symbol), checked
        // here so the message is translated; Identity checks it again when the user is created.
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128).WithName(_ => UserText.PasswordField)
            .Must(BeStrongPassword).WithMessage(_ => UserText.WeakPassword);
        RuleFor(x => x.Roles).ValidRoles();
    }

    private static bool BeStrongPassword(string? password) =>
        string.IsNullOrEmpty(password) // reported by NotEmpty
        || (password.Length >= 8
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(c => !char.IsLetterOrDigit(c)));
}
